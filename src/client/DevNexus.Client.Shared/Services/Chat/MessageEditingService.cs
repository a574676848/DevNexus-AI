using DevNexus.Client.Shared.Abstractions;
using DevNexus.Client.Shared.Abstractions.Chat;
using DevNexus.Client.Shared.Models;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using Microsoft.Extensions.Logging;

namespace DevNexus.Client.Shared.Services.Chat;

/// <summary>
/// 消息编辑服务实现 - 处理消息的编辑、重生成和续写操作
/// </summary>
public class MessageEditingService : IMessageEditingService
{
    private readonly IApiService _apiService;
    private readonly ISignalRService _signalR;
    private readonly IChatState _chatState;
    private readonly ILogger<MessageEditingService> _logger;

    public MessageEditingService(
        IApiService apiService,
        ISignalRService signalR,
        IChatState chatState,
        ILogger<MessageEditingService> logger)
    {
        _apiService = apiService;
        _signalR = signalR;
        _chatState = chatState;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<Guid>> HandleEditMessageAsync(
        ChatMessageDto originalMessage, string newContent,
        Guid sessionId, Guid? providerId, List<ChatMessageDto> allMessages)
    {
        var deletedIds = new List<Guid>();

        _logger.LogInformation("[Edit] 开始编辑消息: MessageId={MessageId}, SessionId={SessionId}", originalMessage.Id, sessionId);
        _logger.LogInformation("[Edit] 新内容长度: {Length}", newContent?.Length ?? 0);

        // 验证会话状态
        if (sessionId == Guid.Empty)
        {
            _logger.LogWarning("[Edit] 错误: 当前会话 ID 为空");
            return deletedIds;
        }

        // 找到该消息在列表中的索引
        var index = allMessages.FindIndex(m => m.Id == originalMessage.Id);
        if (index == -1)
        {
            _logger.LogWarning("[Edit] 错误: 未找到消息 {MessageId} 在消息列表中", originalMessage.Id);
            return deletedIds;
        }
        _logger.LogInformation("[Edit] 消息索引: {Index}, 总消息数: {Count}", index, allMessages.Count);

        // 收集需要删除的消息 IDs（从 index 开始的所有消息，包括该用户消息及其后的 AI 回复）
        var messagesToDelete = allMessages.Skip(index).ToList();
        deletedIds = messagesToDelete.Select(m => m.Id).ToList();
        _logger.LogInformation("[Edit] 将删除 {Count} 条消息", messagesToDelete.Count);

        // 清空 ChatState 中该会话的 Blocks
        _chatState.Clear(sessionId);

        // 设置生成状态
        _chatState.SetSessionGeneratingOptimistic(sessionId, true);

        // 后端批量删除这些消息
        try
        {
            _logger.LogInformation("[Edit] 正在批量删除 {Count} 条消息...", deletedIds.Count);
            var deletedCount = await _apiService.DeleteMessagesAsync(sessionId, deletedIds);
            _logger.LogInformation("[Edit] 批量删除成功，已删除 {Count} 条消息", deletedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Edit] 删除旧消息失败");
            // 继续发送新消息
        }

        // 重新发送编辑后的内容，触发 AI 重新生成
        _logger.LogInformation("[Edit] 正在发送编辑后的内容...使用 Provider: {ProviderId}", providerId);
        var selectedSkillName = TryGetStringMetadataValue(originalMessage.Metadata, ChatMessageMetadataKeys.SelectedSkillName);
        var copiedMetadata = CloneMetadata(originalMessage.Metadata);
        var copiedArtifacts = CloneArtifacts(originalMessage.Artifacts);
        await SendWithProviderAsync(
            newContent ?? string.Empty,
            sessionId,
            providerId,
            ExtractArtifactIds(copiedArtifacts),
            copiedArtifacts,
            true,
            selectedSkillName,
            copiedMetadata);
        _logger.LogInformation("[Edit] 编辑消息处理完成");

        return deletedIds;
    }

    /// <inheritdoc />
    public async Task<(List<Guid> deletedIds, string? resentContent)> HandleRegenerateAsync(
        ChatMessageDto message, Guid sessionId, Guid? providerId, List<ChatMessageDto> allMessages)
    {
        var deletedIds = new List<Guid>();

        // 1. 确认是 Assistant 消息
        if (!ChatConstants.IsAssistantSender(message.SenderType))
            return (deletedIds, null);

        // 2. 找到该消息在列表中的索引
        var index = allMessages.FindIndex(m => m.Id == message.Id);
        if (index == -1)
            return (deletedIds, null);

        // 3. 找到前一条 User 消息的内容（如果 index=0 则无法重试）
        if (index == 0)
            return (deletedIds, null);

        var previousUserMsg = allMessages[index - 1];
        if (!ChatConstants.IsUserSender(previousUserMsg.SenderType))
            return (deletedIds, null);

        var contentToResend = previousUserMsg.Content;
        if (string.IsNullOrEmpty(contentToResend))
            return (deletedIds, null);

        // 4. 需要删除的消息：AI 消息和前一条 User 消息
        deletedIds = new List<Guid> { previousUserMsg.Id, message.Id };

        // 设置生成状态
        _chatState.SetSessionGeneratingOptimistic(sessionId, true);

        // 后端批量删除
        try
        {
            _logger.LogInformation("[Regenerate] 正在批量删除 {Count} 条消息...", deletedIds.Count);
            var deletedCount = await _apiService.DeleteMessagesAsync(sessionId, deletedIds);
            _logger.LogInformation("[Regenerate] 批量删除成功，已删除 {Count} 条消息", deletedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Regenerate] 删除旧消息失败");
        }

        // 5. 重新发送用户内容
        var selectedSkillName = TryGetStringMetadataValue(previousUserMsg.Metadata, ChatMessageMetadataKeys.SelectedSkillName);
        var copiedMetadata = CloneMetadata(previousUserMsg.Metadata);
        var copiedArtifacts = CloneArtifacts(previousUserMsg.Artifacts);
        await SendWithProviderAsync(
            contentToResend,
            sessionId,
            providerId,
            ExtractArtifactIds(copiedArtifacts),
            copiedArtifacts,
            true,
            selectedSkillName,
            copiedMetadata);

        return (deletedIds, contentToResend);
    }

    /// <inheritdoc />
    public async Task HandleContinueGenerationAsync(BlockDto truncatedBlock, Guid sessionId, Guid? providerId)
    {
        if (_chatState.GetSessionRunControl(sessionId).IsGenerationLike) return;

        // 清空当前 Blocks，准备接收续写内容
        _chatState.Clear(sessionId);

        // 设置生成状态
        _chatState.SetSessionGeneratingOptimistic(sessionId, true);

        // 构建续写请求
        var request = new ChatRequest
        {
            SessionId = sessionId,
            Content = "请继续你上面未完成的回复，从截断处接着写，不要重复已有内容。",
            LLMProviderId = providerId,
            IsContinuation = true,
            ContinuationMessageId = truncatedBlock.MessageId,
            EnableRag = false // 续写不需要 RAG
        };

        await _signalR.SendMessageAsync(request);
    }

    /// <summary>
    /// 内部发送消息方法
    /// </summary>
    private async Task SendWithProviderAsync(
        string content, Guid sessionId, Guid? providerId,
        List<Guid>? artifactIds, List<ArtifactDto>? artifacts, bool enableRag,
        string? selectedSkillName = null, Dictionary<string, object>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(content) && (artifacts == null || !artifacts.Any())) return;

        var request = new ChatRequest
        {
            SessionId = sessionId,
            Content = content,
            LLMProviderId = providerId,
            ArtifactIds = artifactIds,
            EnableRag = enableRag,
            EnableSwarm = string.IsNullOrWhiteSpace(selectedSkillName) && metadata?.ContainsKey("toolId") != true,
            SelectedSkillName = selectedSkillName,
            Metadata = metadata
        };

        // 避免异步阻塞导致的竞态冲突，发送转为后台触发
        _ = Task.Run(async () =>
        {
            try
            {
                await _signalR.SendMessageAsync(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MessageEditing] 发送失败");
            }
        });
    }

    private static Dictionary<string, object>? CloneMetadata(Dictionary<string, object>? metadata)
    {
        if (metadata == null || metadata.Count == 0)
        {
            return null;
        }

        return new Dictionary<string, object>(metadata, StringComparer.OrdinalIgnoreCase);
    }

    private static List<Guid>? ExtractArtifactIds(List<ArtifactDto>? artifacts)
    {
        if (artifacts == null || artifacts.Count == 0)
        {
            return null;
        }

        var artifactIds = artifacts
            .Select(artifact => artifact.ArtifactId)
            .Where(artifactId => artifactId != Guid.Empty)
            .Distinct()
            .ToList();

        return artifactIds.Count == 0 ? null : artifactIds;
    }

    private static List<ArtifactDto>? CloneArtifacts(List<ArtifactDto>? artifacts)
    {
        if (artifacts == null || artifacts.Count == 0)
        {
            return null;
        }

        return artifacts.Select(artifact => new ArtifactDto
        {
            ArtifactId = artifact.ArtifactId,
            SemanticId = artifact.SemanticId,
            Version = artifact.Version,
            BaseVersion = artifact.BaseVersion,
            Type = artifact.Type,
            Name = artifact.Name,
            Content = artifact.Content,
            FileAssetId = artifact.FileAssetId,
            FileVersionId = artifact.FileVersionId,
            ParentArtifactId = artifact.ParentArtifactId,
            MessageId = artifact.MessageId,
            SessionId = artifact.SessionId,
            CreatedAt = artifact.CreatedAt,
            UpdatedAt = artifact.UpdatedAt,
            Metadata = CloneMetadata(artifact.Metadata)
        }).ToList();
    }

    private static string? TryGetStringMetadataValue(Dictionary<string, object>? metadata, string key)
    {
        if (metadata == null || !metadata.TryGetValue(key, out var rawValue) || rawValue == null)
        {
            return null;
        }

        var value = rawValue.ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
