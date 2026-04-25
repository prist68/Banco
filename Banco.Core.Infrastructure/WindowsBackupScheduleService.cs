using System.Diagnostics;
using Banco.Vendita.Abstractions;
using Banco.Vendita.Configuration;

namespace Banco.Core.Infrastructure;

public sealed class WindowsBackupScheduleService : IBackupScheduleService
{
    private const string TaskNamePrefix = "Banco Backup Automatico";
    private readonly IPosProcessLogService _logService;

    public WindowsBackupScheduleService(IPosProcessLogService logService)
    {
        _logService = logService;
    }

    public async Task<BackupScheduleSynchronizationResult> SynchronizeAsync(
        BackupConfigurationSettings settings,
        CancellationToken cancellationToken = default)
    {
        var desiredTasks = BuildDesiredTasks(settings);
        var existingTasks = await LoadExistingTaskNamesAsync(cancellationToken);

        foreach (var existingTask in existingTasks.Except(desiredTasks.Select(item => item.Name), StringComparer.OrdinalIgnoreCase))
        {
            await DeleteTaskAsync(existingTask, cancellationToken);
        }

        if (desiredTasks.Count == 0)
        {
            var disabledMessage = settings.ScheduledBackupEnabled
                ? "Programmazione Windows non attivata: seleziona almeno un giorno valido."
                : "Programmazione Windows disattivata: task automatici rimossi.";
            _logService.Info(nameof(WindowsBackupScheduleService), disabledMessage);

            return new BackupScheduleSynchronizationResult(
                SchedulingEnabled: false,
                TaskNames: [],
                Message: disabledMessage);
        }

        foreach (var task in desiredTasks)
        {
            await CreateOrUpdateTaskAsync(task, cancellationToken);
        }

        var configuredNames = desiredTasks
            .Select(item => item.Name)
            .ToList();
        var message = configuredNames.Count == 1
            ? $"Programmazione Windows aggiornata: task '{configuredNames[0]}' attivo."
            : $"Programmazione Windows aggiornata: {configuredNames.Count} task attivi.";

        _logService.Info(
            nameof(WindowsBackupScheduleService),
            $"Task scheduler sincronizzato. Task={string.Join(" | ", configuredNames)}, Giorni={string.Join(",", GetSelectedDays(settings))}, Orari={string.Join(" | ", desiredTasks.Select(item => item.Time))}.");

        return new BackupScheduleSynchronizationResult(
            SchedulingEnabled: true,
            TaskNames: configuredNames,
            Message: message);
    }

    private static List<ScheduledTaskDefinition> BuildDesiredTasks(BackupConfigurationSettings settings)
    {
        if (!settings.ScheduledBackupEnabled)
        {
            return [];
        }

        var selectedDays = GetSelectedDays(settings);
        if (selectedDays.Count == 0)
        {
            return [];
        }

        var times = BuildScheduledTimes(settings);
        var dayArgument = string.Join(",", selectedDays);
        var tasks = new List<ScheduledTaskDefinition>(times.Count);

        for (var index = 0; index < times.Count; index++)
        {
            var taskName = index == 0
                ? TaskNamePrefix
                : $"{TaskNamePrefix} {index + 1}";
            tasks.Add(new ScheduledTaskDefinition(taskName, times[index], dayArgument));
        }

        return tasks;
    }

    private static List<string> BuildScheduledTimes(BackupConfigurationSettings settings)
    {
        var requestedRuns = settings.ScheduledRunsPerDay <= 0
            ? 1
            : settings.ScheduledRunsPerDay;

        var rawTimes = new List<string> { NormalizeTime(settings.ScheduledStartTime) };
        if (settings.ScheduledAdditionalTimes is not null)
        {
            rawTimes.AddRange(settings.ScheduledAdditionalTimes.Select(NormalizeTime));
        }

        return rawTimes
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(requestedRuns)
            .ToList();
    }

    private static List<string> GetSelectedDays(BackupConfigurationSettings settings)
    {
        var days = new List<string>(7);
        if (settings.ScheduledOnMonday) days.Add("MON");
        if (settings.ScheduledOnTuesday) days.Add("TUE");
        if (settings.ScheduledOnWednesday) days.Add("WED");
        if (settings.ScheduledOnThursday) days.Add("THU");
        if (settings.ScheduledOnFriday) days.Add("FRI");
        if (settings.ScheduledOnSaturday) days.Add("SAT");
        if (settings.ScheduledOnSunday) days.Add("SUN");
        return days;
    }

    private static string NormalizeTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "13:00";
        }

        return TimeSpan.TryParse(value, out var time)
            ? $"{time.Hours:00}:{time.Minutes:00}"
            : "13:00";
    }

    private async Task<List<string>> LoadExistingTaskNamesAsync(CancellationToken cancellationToken)
    {
        var result = await RunSchtasksAsync(
            [
                "/Query",
                "/FO",
                "CSV",
                "/NH"
            ],
            cancellationToken);

        return result.StandardOutput
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseFirstCsvField)
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Select(static item => item.Trim().TrimStart('\\'))
            .Where(item => item.StartsWith(TaskNamePrefix, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task CreateOrUpdateTaskAsync(ScheduledTaskDefinition task, CancellationToken cancellationToken)
    {
        var executablePath = ResolveExecutablePath();
        var taskAction = $"\"{executablePath}\" {BackupCommandLineArguments.RunScheduledBackup}";

        await RunSchtasksAsync(
            [
                "/Create",
                "/F",
                "/TN",
                task.Name,
                "/SC",
                "WEEKLY",
                "/D",
                task.Days,
                "/ST",
                task.Time,
                "/TR",
                taskAction,
                "/RL",
                "LIMITED",
                "/IT"
            ],
            cancellationToken);

        _logService.Info(
            nameof(WindowsBackupScheduleService),
            $"Task automatico aggiornato. Nome={task.Name}, Giorni={task.Days}, Orario={task.Time}, Azione={taskAction}.");
    }

    private async Task DeleteTaskAsync(string taskName, CancellationToken cancellationToken)
    {
        await RunSchtasksAsync(
            [
                "/Delete",
                "/F",
                "/TN",
                taskName
            ],
            cancellationToken);

        _logService.Info(nameof(WindowsBackupScheduleService), $"Task automatico rimosso: {taskName}.");
    }

    private static string ResolveExecutablePath()
    {
        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            executablePath = Process.GetCurrentProcess().MainModule?.FileName;
        }

        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            throw new InvalidOperationException("Impossibile determinare l'eseguibile Banco per il task pianificato.");
        }

        return executablePath;
    }

    private async Task<ShellExecutionResult> RunSchtasksAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        if (!process.Start())
        {
            throw new InvalidOperationException("Avvio di schtasks.exe non riuscito.");
        }

        var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var standardOutput = await standardOutputTask;
        var standardError = await standardErrorTask;
        if (process.ExitCode == 0)
        {
            return new ShellExecutionResult(standardOutput, standardError);
        }

        throw new InvalidOperationException(
            $"schtasks.exe ha restituito errore {process.ExitCode}: {standardError}{Environment.NewLine}{standardOutput}".Trim());
    }

    private static string ParseFirstCsvField(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return string.Empty;
        }

        var index = 0;
        var inQuotes = false;
        var buffer = new List<char>(line.Length);

        while (index < line.Length)
        {
            var current = line[index];

            if (current == '"')
            {
                if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    buffer.Add('"');
                    index += 2;
                    continue;
                }

                inQuotes = !inQuotes;
                index++;
                continue;
            }

            if (current == ',' && !inQuotes)
            {
                break;
            }

            buffer.Add(current);
            index++;
        }

        return new string([.. buffer]);
    }

    private sealed record ScheduledTaskDefinition(string Name, string Time, string Days);

    private sealed record ShellExecutionResult(string StandardOutput, string StandardError);
}
