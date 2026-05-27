using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;

namespace DevNexus.Client.Shared.Abstractions.Chat;

/// <summary>
/// 消息处理服务接口 - 处理会话消息的加载、接收和处理
/// </summary>
public interface IMessageHandlingService
{
    /// <summary>
    /// 加载会话历史消息
    /// </summary>
    Task<List<ChatMessageDto>> LoadSessionMessagesAsync(Guid sessionId);

    /// <summary>
    /// 从历史消息中恢复当前会话的 CLI 执行会话状态。
    /// </summary>
    CliSessionStateDto? RestoreCliExecSession(Guid sessionId, IReadOnlyList<ChatMessageDto> messages);

    /// <summary>
    /// 处理接收到的完整消息（合并 Artifacts、ChartBlocks 等）
    /// </summary>
    Task HandleMessageReceivedAsync(
        ChatMessageDto message, List<BlockDto> currentBlocks,
        List<ArtifactDto> completedArtifacts, ArtifactDto? currentArtifact);

    /// <summary>
    /// 处理生成完成事件。
    /// 返回是否需要生成标题和是否需要智能标题。
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="messageCount">当前消息总数</param>
    /// <param name="isFirstMessage">是否为首轮对话</param>
    Task<(bool shouldGenerateTitle, bool shouldGenerateSmartTitle)> HandleGenerationCompleteAsync(
        Guid sessionId, int messageCount, bool isFirstMessage);

    /// <summary>
    /// 处理生成错误事件。
    /// 仅当目标为当前会话时，将已生成的 Blocks 内容和错误信息构建为 AI 消息返回；
    /// 非当前会话仅重置生成状态并返回 null。
    /// </summary>
    /// <param name="sessionId">目标会话 ID（Guid.Empty 时默认当前会话）</param>
    /// <param name="errorMessage">错误消息文本</param>
    /// <param name="currentBlocks">当前已接收的 Block 列表</param>
    /// <param name="currentMessageId">当前消息 ID</param>
    Task<ChatMessageDto?> HandleGenerationErrorAsync(
        Guid sessionId, string errorMessage,
        List<BlockDto> currentBlocks, Guid currentMessageId);

    /// <summary>
    /// 处理生成取消事件。
    /// 当前会话将已生成内容固化为消息返回；非当前会话仅重置状态并返回 null。
    /// </summary>
    /// <param name="sessionId">目标会话 ID（Guid.Empty 时默认当前会话）</param>
    /// <param name="currentBlocks">当前已接收的 Block 列表</param>
    /// <param name="currentMessageId">当前消息 ID</param>
    Task<ChatMessageDto?> HandleGenerationCancelledAsync(
        Guid sessionId, List<BlockDto> currentBlocks, Guid currentMessageId);

    /// <summary>
    /// 恢复流式生成状态。
    /// 返回需要从历史消息中移除的消息 ID 列表（避免重复显示）。
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="currentBlocks">组件的 currentBlocks 列表（会被填充）</param>
    List<Guid> RestoreGeneratingState(Guid sessionId, List<BlockDto> currentBlocks);

}
