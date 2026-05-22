using DevNexus.Shared.Enums;

namespace DevNexus.Core.Services.Swarm;

/// <summary>
/// Swarm 会话收尾结果。
/// </summary>
public sealed record SwarmSessionFinalizationResult(
    SwarmStatus Status,
    string Reason,
    bool NotifyFailure,
    bool NotifyCancellation);
