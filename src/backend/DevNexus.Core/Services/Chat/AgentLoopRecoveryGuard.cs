using DevNexus.Core.Models.Evaluation;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// Agent Loop 恢复前置判断结果。
/// </summary>
public sealed record AgentLoopRecoveryGuardResult
{
    /// <summary>
    /// 归一化后的工具记录。
    /// </summary>
    public IReadOnlyList<ToolExecutionRecord> ToolRecords { get; init; } = Array.Empty<ToolExecutionRecord>();

    /// <summary>
    /// 是否需要停止自动修复。
    /// </summary>
    public bool ShouldStop { get; init; }

    /// <summary>
    /// 是否需要先创建挂起交互。
    /// </summary>
    public bool RequiresPendingInteraction { get; init; }

    /// <summary>
    /// 需要创建挂起交互时对应的工具记录。
    /// </summary>
    public ToolExecutionRecord? PendingInteractionTool { get; init; }

    /// <summary>
    /// 停止提示标题。
    /// </summary>
    public string? StopTitle { get; init; }

    /// <summary>
    /// 停止提示说明。
    /// </summary>
    public string? StopMessage { get; init; }
}

/// <summary>
/// Agent Loop 恢复前置判断服务。
/// 在进入评估与自动修复前，先拦截已存在挂起交互和明显不可恢复的失败。
/// </summary>
public interface IAgentLoopRecoveryGuard
{
    /// <summary>
    /// 评估当前工具执行结果是否允许继续自动修复。
    /// </summary>
    Task<AgentLoopRecoveryGuardResult> EvaluateAsync(
        Guid userId,
        Guid sessionId,
        IReadOnlyList<ToolExecutionRecord> toolRecords,
        int agentLoopAttempt,
        CancellationToken cancellationToken);
}

/// <summary>
/// Agent Loop 恢复前置判断服务实现。
/// </summary>
internal sealed class AgentLoopRecoveryGuard : IAgentLoopRecoveryGuard
{
    private readonly IChatSessionRuntimeInspector _runtimeInspector;
    private readonly AgentLoopRecoveryPipeline _recoveryPipeline;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public AgentLoopRecoveryGuard(
        IChatSessionRuntimeInspector runtimeInspector,
        AgentLoopRecoveryPipeline recoveryPipeline)
    {
        _runtimeInspector = runtimeInspector;
        _recoveryPipeline = recoveryPipeline;
    }

    /// <inheritdoc />
    public async Task<AgentLoopRecoveryGuardResult> EvaluateAsync(
        Guid userId,
        Guid sessionId,
        IReadOnlyList<ToolExecutionRecord> toolRecords,
        int agentLoopAttempt,
        CancellationToken cancellationToken)
    {
        var normalizedToolRecords = ToolExecutionRecordNormalizer.Normalize(toolRecords);
        if (normalizedToolRecords.Count == 0)
        {
            return new AgentLoopRecoveryGuardResult
            {
                ToolRecords = normalizedToolRecords
            };
        }

        var runtime = await _runtimeInspector.InspectAsync(
            userId,
            sessionId,
            queuedCount: 0,
            cancellationToken);
        var recoveryResult = await _recoveryPipeline.ExecuteAsync(
            new AgentLoopRecoveryContext
            {
                UserId = userId,
                SessionId = sessionId,
                ToolRecords = normalizedToolRecords,
                AgentLoopAttempt = agentLoopAttempt,
                Runtime = runtime
            },
            cancellationToken);
        if (recoveryResult != null)
        {
            return recoveryResult;
        }

        return new AgentLoopRecoveryGuardResult
        {
            ToolRecords = normalizedToolRecords
        };
    }
}
