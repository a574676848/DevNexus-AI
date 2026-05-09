using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;

namespace DevNexus.Client.Shared.Abstractions.Chat;

/// <summary>
/// 生成控制服务接口 - 处理AI生成的生命周期管理
/// </summary>
public interface IGenerationControlService
{
    /// <summary>
    /// 处理发送消息（带 Provider 选择和 Artifact ID），返回构建的用户消息 DTO（供组件添加到消息列表）
    /// </summary>
    Task<ChatMessageDto?> HandleSendWithProviderAsync(
        string content, Guid sessionId, Guid? providerId,
        List<Guid>? artifactIds, List<ArtifactDto>? artifacts, bool enableRag,
        string? selectedSkillName = null, Dictionary<string, object>? metadata = null);

    /// <summary>
    /// 处理发送消息（不带 Provider，向后兼容）
    /// </summary>
    Task<ChatMessageDto?> HandleSendAsync(string content, Guid sessionId);

    /// <summary>
    /// 处理取消生成（Swarm 中止、内容固化、SignalR 取消）。
    /// ⚠️ 调用前组件应先回落会话统一运行态并触发一次 UI 刷新，以确保按钮即时响应。
    /// 返回固化的 AI 消息（如果有已生成的部分内容则构建消息返回，否则返回 null）。
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="currentBlocks">当前流式接收的 Block 列表</param>
    /// <param name="currentMessageId">当前正在生成的消息 ID</param>
    Task<ChatMessageDto?> HandleCancelAsync(Guid sessionId, List<BlockDto> currentBlocks, Guid currentMessageId);
}
