using DevNexus.Core.Models.Evaluation;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// Agent Loop 恢复上下文。
/// </summary>
internal sealed class AgentLoopRecoveryContext
{
    /// <summary>
    /// 用户标识。
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// 会话标识。
    /// </summary>
    public Guid SessionId { get; init; }

    /// <summary>
    /// 归一化后的工具执行记录。
    /// </summary>
    public IReadOnlyList<ToolExecutionRecord> ToolRecords { get; init; } = Array.Empty<ToolExecutionRecord>();

    /// <summary>
    /// 当前自动修复尝试次数。
    /// </summary>
    public int AgentLoopAttempt { get; init; }

    /// <summary>
    /// 当前统一运行态快照。
    /// </summary>
    public ChatSessionRuntimeSnapshot Runtime { get; init; } = new();
}
