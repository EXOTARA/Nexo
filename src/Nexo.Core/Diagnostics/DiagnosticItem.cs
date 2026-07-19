namespace Nexo.Core.Diagnostics;

public sealed record DiagnosticItem(
    string Name,
    DiagnosticStatus Status,
    string Detail);
