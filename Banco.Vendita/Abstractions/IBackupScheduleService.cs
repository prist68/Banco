using Banco.Vendita.Configuration;

namespace Banco.Vendita.Abstractions;

public interface IBackupScheduleService
{
    Task<BackupScheduleSynchronizationResult> SynchronizeAsync(
        BackupConfigurationSettings settings,
        CancellationToken cancellationToken = default);
}

public sealed record BackupScheduleSynchronizationResult(
    bool SchedulingEnabled,
    IReadOnlyList<string> TaskNames,
    string Message);
