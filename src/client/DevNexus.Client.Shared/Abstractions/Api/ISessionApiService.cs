using DevNexus.Shared.DTOs;

namespace DevNexus.Client.Shared.Abstractions;

/// <summary>
/// 会话管理 API 服务接口
/// </summary>
public interface ISessionApiService
{
    /// <summary>
    /// 获取会话列表
    /// </summary>
    Task<List<ChatSessionDto>> GetSessionsAsync();

    /// <summary>
    /// 创建新会话
    /// </summary>
    Task<ChatSessionDto> CreateSessionAsync(string? title = null);

    /// <summary>
    /// 获取会话消息
    /// </summary>
    Task<List<ChatMessageDto>> GetMessagesAsync(Guid sessionId);

    /// <summary>
    /// 获取指定会话当前活跃的终端记录。
    /// </summary>
    Task<List<TerminalRecordDto>> GetActiveTerminalRecordsAsync(Guid sessionId);

    /// <summary>
    /// 获取指定终端记录的完整输出内容。
    /// </summary>
    Task<TerminalOutputContentDto> GetTerminalOutputAsync(Guid sessionId, Guid recordId);

    /// <summary>
    /// 获取指定会话当前活跃的挂起交互。
    /// </summary>
    Task<List<PendingInteractionDto>> GetPendingInteractionsAsync(Guid sessionId);

    /// <summary>
    /// 获取指定会话当前统一运行时快照。
    /// </summary>
    Task<ChatSessionRuntimeDto> GetSessionRuntimeAsync(Guid sessionId);

    /// <summary>
    /// 解决指定挂起交互。
    /// </summary>
    Task<PendingInteractionResolutionResponse> ResolvePendingInteractionAsync(
        Guid sessionId,
        Guid interactionId,
        PendingInteractionResolutionRequest request);

    /// <summary>
    /// 删除消息
    /// </summary>
    Task DeleteMessageAsync(Guid sessionId, Guid messageId);

    /// <summary>
    /// 批量删除消息（用于编辑/重新发送场景，提升性能）
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="messageIds">要删除的消息ID列表</param>
    /// <returns>成功删除的消息数量</returns>
    Task<int> DeleteMessagesAsync(Guid sessionId, List<Guid> messageIds);

    /// <summary>
    /// 删除会话
    /// </summary>

    /// <summary>
    /// 删除会话
    /// </summary>
    Task DeleteSessionAsync(Guid sessionId);

    /// <summary>
    /// 更新会话上下文。
    /// </summary>
    Task<ChatSessionDto> UpdateSessionAsync(Guid sessionId, ChatSessionUpdateRequest request);

    /// <summary>
    /// 更新会话标题
    /// </summary>
    Task UpdateSessionTitleAsync(Guid sessionId, string title);

    /// <summary>
    /// 使用 LLM 智能生成会话标题
    /// </summary>
    Task<string?> GenerateSmartTitleAsync(Guid sessionId);

    /// <summary>
    /// 中止 Swarm 编排会话
    /// </summary>
    Task AbortSwarmSessionAsync(Guid sessionId);

    /// <summary>
    /// 重试指定的 Swarm 工作包。
    /// </summary>
    Task RetrySwarmPackageAsync(Guid sessionId, string packageId);
}

