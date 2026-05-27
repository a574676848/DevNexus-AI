using DevNexus.Shared.DTOs;

namespace DevNexus.Client.Shared.Abstractions;

/// <summary>
/// 聊天消息处理器接口 - 处理 Block 到消息的转换
/// </summary>
public interface IChatMessageProcessor
{
    /// <summary>
    /// 构建聊天消息
    /// </summary>
    Task<ChatMessageDto?> BuildChatMessageAsync(
        Guid sessionId,
        Guid messageId,
        List<BlockDto> blocks,
        List<ArtifactDto> completedArtifacts,
        ArtifactDto? currentArtifact);
}
