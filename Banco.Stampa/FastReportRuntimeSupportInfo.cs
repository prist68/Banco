namespace Banco.Stampa;

public sealed class FastReportRuntimeSupportInfo
{
    public bool CanOpenDesigner { get; init; }

    public bool CanAttemptPreview { get; init; }

    public bool CanAttemptPrint { get; init; }

    public FastReportDesignerMode DesignerMode { get; init; }

    public string RuntimeDescription { get; init; } = string.Empty;

    public string BlockingReason { get; init; } = string.Empty;

    public string LayoutsDirectory { get; init; } = string.Empty;

    public string RuntimeAssemblyPath { get; init; } = string.Empty;

    public string DesignerAssemblyPath { get; init; } = string.Empty;

    public string DetectedVersion { get; init; } = string.Empty;
}
