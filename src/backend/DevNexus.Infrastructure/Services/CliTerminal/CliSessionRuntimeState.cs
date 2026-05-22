using DevNexus.Core.Models.Cli;

namespace DevNexus.Infrastructure.Services.CliTerminal;

/// <summary>
/// CLI 会话运行态。
/// </summary>
internal sealed class CliSessionRuntimeState
{
    public string SessionKey { get; set; } = string.Empty;

    public string WorkingDirectory { get; set; } = string.Empty;

    public string LockKey { get; set; } = string.Empty;

    public string LeaseSessionKey { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime LastActivityAt { get; set; }

    public bool WaitingForInput { get; set; }

    public DateTime? WaitingForInputSince { get; set; }

    public CliSessionExecutionState State { get; set; }

    public CliSessionTerminationReason TerminationReason { get; set; }
}
