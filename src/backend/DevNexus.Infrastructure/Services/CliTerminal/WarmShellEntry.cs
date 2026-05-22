using System.Diagnostics;
using DevNexus.Core.Abstractions;

namespace DevNexus.Infrastructure.Services.CliTerminal;

/// <summary>
/// 预热 Shell 条目。
/// </summary>
internal sealed class WarmShellEntry
{
    public string WorkingDirectory { get; init; } = string.Empty;

    public CliSandboxSessionLease Lease { get; init; } = new();

    public Process Process { get; init; } = null!;

    public DateTime WarmedAt { get; init; }
}
