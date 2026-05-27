using DevNexus.Client.Shared.Abstractions;
using DevNexus.Client.Shared.Abstractions.Chat;
using DevNexus.Client.Shared.Helpers;
using DevNexus.Client.Shared.Models;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using Microsoft.Extensions.Logging;
using DevNexus.Client.Shared.Components.Chat;
using System.Text.Json;

namespace DevNexus.Client.Shared.Services.Chat;

/// <summary>
/// 消息处理服务实现 - 处理会话消息的加载、接收和处理
/// </summary>
public class MessageHandlingService : IMessageHandlingService
{
    private static readonly TimeSpan CliWaitingInputTimeout = TimeSpan.FromMinutes(5);

    private readonly IApiService _apiService;
    private readonly IChatState _chatState;
    private readonly ISessionState _sessionState;
    private readonly IChatMessageProcessor _messageProcessor;
    private readonly ILogger<MessageHandlingService> _logger;

    public MessageHandlingService(
        IApiService apiService,
        IChatState chatState,
        ISessionState sessionState,
        IChatMessageProcessor messageProcessor,
        ILogger<MessageHandlingService> logger)
    {
        _apiService = apiService;
        _chatState = chatState;
        _sessionState = sessionState;
        _messageProcessor = messageProcessor;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<ChatMessageDto>> LoadSessionMessagesAsync(Guid sessionId)
    {
        var messages = new List<ChatMessageDto>();

        try
        {
            // 异步加载消息
            var loadedMessages = await _apiService.GetMessagesAsync(sessionId);
            messages = loadedMessages ?? new List<ChatMessageDto>();

            // 发现后端进行中或后台挂起的 Swarm 任务
            var activeSwarmMsg = messages.LastOrDefault(m =>
                ChatConstants.IsAssistantSender(m.SenderType) && ChatConstants.IsInProgressStatus(m.Status));
            if (activeSwarmMsg != null)
            {
                var isSwarm = ChatMessageMetadataKeys.IsSwarmMode(activeSwarmMsg.Metadata);

                if (isSwarm)
                {
                    _logger.LogInformation("[LoadSession] Detected active Swarm message for session {SessionId}, restoring state", sessionId);

                    // 把该消息转换到流式生成层，不显示在历史记录以避免冲突和渲染跳动
                    var block = new BlockDto
                    {
                        BlockType = BlockType.TextDelta,
                        Content = activeSwarmMsg.TextContent ?? activeSwarmMsg.Content ?? "",
                        MessageId = activeSwarmMsg.Id,
                        SessionId = sessionId
                    };

                    _chatState.AddBlock(sessionId, block);
                }
                else
                {
                    _logger.LogInformation("[LoadSession] Detected active non-Swarm message for session {SessionId}", sessionId);

                    messages.Remove(activeSwarmMsg);
                    _chatState.SetSwarmActive(sessionId, false);
                    RestoreInProgressMessage(sessionId, activeSwarmMsg);
                }
            }
            else
            {
                // 没有活动消息，确保 Swarm 状态为 false
                if (_chatState.IsSwarmActive(sessionId))
                {
                    _logger.LogInformation("[LoadSession] Clearing stale Swarm state for session {SessionId}", sessionId);
                    _chatState.SetSwarmActive(sessionId, false);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载消息失败");
        }

        return messages;
    }

    /// <inheritdoc />
    public CliSessionStateDto? RestoreCliExecSession(Guid sessionId, IReadOnlyList<ChatMessageDto> messages)
    {
        if (messages == null || messages.Count == 0)
        {
            return null;
        }

        foreach (var message in messages.OrderByDescending(x => x.CreatedAt))
        {
            if (message.OrderedBlocks == null || message.OrderedBlocks.Count == 0)
            {
                continue;
            }

            foreach (var block in message.OrderedBlocks
                .Where(x => x.BlockType == BlockType.Terminal)
                .OrderByDescending(x => x.Version))
            {
                var state = BuildCliExecSessionFromBlock(sessionId, block);
                if (state == null)
                {
                    continue;
                }

                NormalizeHistoricalCliExecSession(message, state);
                return state;
            }
        }

        return null;
    }

    private void RestoreInProgressMessage(Guid sessionId, ChatMessageDto message)
    {
        var blocks = new List<BlockDto>();

        if (!string.IsNullOrEmpty(message.Content) || message.TextContent != null || message.ThinkingContent != null)
        {
            var parsed = MessageContentParser.ParseContent(message);
            var thoughtText = string.Join("\n", parsed.Thoughts);
            if (!string.IsNullOrEmpty(thoughtText))
            {
                blocks.Add(new BlockDto
                {
                    BlockType = BlockType.Thinking,
                    Content = thoughtText,
                    MessageId = message.Id,
                    SessionId = sessionId
                });
            }

            if (!string.IsNullOrEmpty(parsed.DisplayedContent))
            {
                blocks.Add(new BlockDto
                {
                    BlockType = BlockType.TextDelta,
                    Content = parsed.DisplayedContent,
                    MessageId = message.Id,
                    SessionId = sessionId
                });
            }
        }

        if (message.OrderedBlocks != null && message.OrderedBlocks.Any())
        {
            foreach (var block in message.OrderedBlocks)
            {
                blocks.Add(new BlockDto
                {
                    BlockId = block.BlockId,
                    ArtifactId = block.ArtifactId,
                    Version = block.Version,
                    Action = block.Action,
                    SessionId = block.SessionId == Guid.Empty ? sessionId : block.SessionId,
                    BlockType = block.BlockType,
                    Content = block.Content,
                    Metadata = block.Metadata,
                    MessageId = block.MessageId == Guid.Empty ? message.Id : block.MessageId,
                    IsLast = block.IsLast,
                    Highlight = block.Highlight
                });
            }
        }

        _chatState.SetBlocks(sessionId, blocks);
        _chatState.SetSessionGeneratingOptimistic(sessionId, true);
    }

    private static CliSessionStateDto? BuildCliExecSessionFromBlock(Guid sessionId, BlockDto block)
    {
        var metadata = block.Metadata;
        if (metadata == null)
        {
            return null;
        }

        var sessionKey = GetStringFromMetadata(metadata, TerminalBlockMetadataKeys.SessionKey);
        var sessionState = CliSessionStateExtensions.Parse(GetStringFromMetadata(metadata, TerminalBlockMetadataKeys.SessionState));
        var status = TerminalStreamStatusExtensions.Parse(GetStringFromMetadata(metadata, TerminalBlockMetadataKeys.Status));
        if (string.IsNullOrWhiteSpace(sessionKey) && !metadata.ContainsKey(TerminalBlockMetadataKeys.WaitingForInput))
        {
            return null;
        }

        return new CliSessionStateDto
        {
            SessionId = sessionId,
            SessionKey = sessionKey ?? string.Empty,
            TerminalStreamId = GetGuidFromMetadata(metadata, TerminalBlockMetadataKeys.TerminalStreamId),
            Command = GetStringFromMetadata(metadata, TerminalBlockMetadataKeys.Command) ?? string.Empty,
            WorkingDirectory = GetStringFromMetadata(metadata, TerminalBlockMetadataKeys.WorkingDirectory),
            Status = status == TerminalStreamStatus.Unknown ? TerminalStreamStatus.Running.ToWireValue() : status.ToWireValue(),
            SessionState = sessionState == CliSessionState.Unknown ? CliSessionState.Created.ToWireValue() : sessionState.ToWireValue(),
            RuntimeHost = GetStringFromMetadata(metadata, TerminalBlockMetadataKeys.RuntimeHost),
            WaitingForInput = GetBoolFromMetadata(metadata, TerminalBlockMetadataKeys.WaitingForInput) ?? false,
            WaitingForInputSince = GetDateTimeFromMetadata(metadata, TerminalBlockMetadataKeys.WaitingForInputSince),
            TerminationReason = CliSessionTerminationReasons.Normalize(
                GetStringFromMetadata(metadata, TerminalBlockMetadataKeys.TerminationReason),
                string.Empty),
            IsActive = sessionState.IsActive()
        };
    }

    private static void NormalizeHistoricalCliExecSession(ChatMessageDto message, CliSessionStateDto state)
    {
        var isMessageInProgress = ChatConstants.IsInProgressStatus(message.Status);

        if (state.WaitingForInput && state.WaitingForInputSince.HasValue &&
            DateTime.UtcNow - state.WaitingForInputSince.Value > CliWaitingInputTimeout)
        {
            state.Status = TerminalStreamStatus.Completed.ToWireValue();
            state.SessionState = CliSessionState.Reaped.ToWireValue();
            state.WaitingForInput = false;
            state.TerminationReason = CliSessionTerminationReasons.WaitingForInputTimeout;
            state.IsActive = false;
            return;
        }

        if (!isMessageInProgress && state.IsActive)
        {
            state.Status = TerminalStreamStatus.Completed.ToWireValue();
            state.SessionState = CliSessionState.Completed.ToWireValue();
            state.WaitingForInput = false;
            state.TerminationReason ??= CliSessionTerminationReasons.Completed;
            state.IsActive = false;
        }
    }

    private static string? GetStringFromMetadata(Dictionary<string, object>? metadata, string key)
    {
        if (metadata == null || !metadata.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.ToString(),
                JsonValueKind.True => bool.TrueString,
                JsonValueKind.False => bool.FalseString,
                _ => null
            };
        }

        return value.ToString();
    }

    private static bool? GetBoolFromMetadata(Dictionary<string, object>? metadata, string key)
    {
        if (metadata == null || !metadata.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        if (value is bool boolValue)
        {
            return boolValue;
        }

        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String when bool.TryParse(element.GetString(), out var parsed) => parsed,
                _ => null
            };
        }

        return bool.TryParse(value.ToString(), out var parsedValue) ? parsedValue : null;
    }

    private static Guid? GetGuidFromMetadata(Dictionary<string, object>? metadata, string key)
    {
        var value = GetStringFromMetadata(metadata, key);
        return Guid.TryParse(value, out var parsed) ? parsed : null;
    }

    private static DateTime? GetDateTimeFromMetadata(Dictionary<string, object>? metadata, string key)
    {
        var value = GetStringFromMetadata(metadata, key);
        return DateTime.TryParse(value, out var parsed) ? parsed : null;
    }

    /// <inheritdoc />
    public async Task HandleMessageReceivedAsync(
        ChatMessageDto message, List<BlockDto> currentBlocks,
        List<ArtifactDto> completedArtifacts, ArtifactDto? currentArtifact)
    {
        try
        {
            // 调用 MessageProcessor 保存所有积累的 Artifacts（图表、交互卡片等）
            var builtMessage = await _messageProcessor.BuildChatMessageAsync(
                message.ChatSessionId,
                message.Id,
                currentBlocks.ToList(), // 传递副本避免并发修改
                completedArtifacts.ToList(),
                currentArtifact);

            // 如果构建的消息有额外的 Artifacts，合并到接收的消息中
            if (builtMessage?.Artifacts?.Any() == true)
            {
                if (message.Artifacts == null)
                {
                    message.Artifacts = builtMessage.Artifacts;
                }
                else
                {
                    // 合并，避免重复
                    var existingIds = message.Artifacts.Select(a => a.ArtifactId).ToHashSet();
                    foreach (var artifact in builtMessage.Artifacts.Where(a => !existingIds.Contains(a.ArtifactId)))
                    {
                        message.Artifacts.Add(artifact);
                    }
                }
            }

            // 如果有图表或交互卡片 blocks，也合并
            if (builtMessage?.ChartBlocks?.Any() == true)
            {
                message.ChartBlocks = builtMessage.ChartBlocks;
            }
            if (builtMessage?.InteractiveBlocks?.Any() == true)
            {
                message.InteractiveBlocks = builtMessage.InteractiveBlocks;
            }
            if (builtMessage?.OrderedBlocks?.Any() == true)
            {
                message.OrderedBlocks = builtMessage.OrderedBlocks;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MessageHandlingService] 保存 Artifacts 失败");
        }

        // 确保 Artifacts 存在（如果后端返回为空，使用本地积累的）
        if ((message.Artifacts == null || !message.Artifacts.Any()) && completedArtifacts.Any())
        {
            message.Artifacts = new List<ArtifactDto>(completedArtifacts);
        }
    }

    /// <inheritdoc />
    public Task<(bool shouldGenerateTitle, bool shouldGenerateSmartTitle)> HandleGenerationCompleteAsync(
        Guid sessionId, int messageCount, bool isFirstMessage)
    {
        // 确保状态重置（作为 HandleMessageReceived 的备份）
        _chatState.SetSessionGeneratingOptimistic(sessionId, false);

        // 智能标题生成逻辑判断
        var shouldGenerateTitle = false;
        var shouldGenerateSmartTitle = false;

        if (messageCount >= 2 && (isFirstMessage || messageCount == 2))
        {
            // 首轮对话后生成标题
            shouldGenerateTitle = true;
        }
        else if (messageCount % 6 == 0 && messageCount > 2)
        {
            // 每 3 轮对话（6 条消息）后更新标题
            shouldGenerateSmartTitle = true;
        }

        return Task.FromResult((shouldGenerateTitle, shouldGenerateSmartTitle));
    }

    /// <inheritdoc />
    public Task<ChatMessageDto?> HandleGenerationErrorAsync(
        Guid sessionId, string errorMessage,
        List<BlockDto> currentBlocks, Guid currentMessageId)
    {
        // 如果 SessionId 为 Empty，说明是全局错误或无法解析 SessionId，默认为当前会话
        var targetSessionId = sessionId == Guid.Empty ? _chatState.CurrentSessionId : sessionId;

        // 【幂等保护】会话已不处于生成链路时，错误事件通常是晚到包或取消后的竞态事件，直接忽略
        if (!_chatState.GetSessionRunControl(targetSessionId).IsGenerationLike)
        {
            _logger.LogDebug(
                "[生成错误-已忽略] SessionId={SessionId} 已非生成状态, Error={Error}",
                targetSessionId,
                errorMessage);
            return Task.FromResult<ChatMessageDto?>(null);
        }

        // 【语义纠偏】将“取消类错误”按取消事件处理，避免显示为失败气泡
        if (IsCancellationLikeError(errorMessage))
        {
            _logger.LogInformation(
                "[生成错误-取消语义] SessionId={SessionId}, Error={Error}",
                targetSessionId,
                errorMessage);
            return HandleGenerationCancelledAsync(targetSessionId, currentBlocks, currentMessageId);
        }

        // 非当前会话的错误，只重置状态，不构建错误消息（与原始 ChatContainer 逻辑对齐）
        if (targetSessionId != _chatState.CurrentSessionId)
        {
            _chatState.SetSessionGeneratingOptimistic(targetSessionId, false);
            _logger.LogWarning("[生成错误-非当前会话] SessionId={SessionId}, Error={Error}", targetSessionId, errorMessage);
            return Task.FromResult<ChatMessageDto?>(null);
        }

        // 当前会话：收集已生成的 AI 内容（包含思考）
        var textContent = string.Join("", currentBlocks
            .Where(b => b.BlockType == BlockType.TextDelta).Select(b => b.Content));
        var thoughtContent = MetadataHelper.JoinThoughtSegments(currentBlocks
            .Where(b => b.BlockType == BlockType.Thinking).Select(b => b.Content));

        var existingContent = textContent;

        // 构建包含已生成内容和错误信息的完整 AI 消息
        var fullContent = string.IsNullOrEmpty(existingContent)
            ? $"⚠️ 生成响应时发生错误：{errorMessage}"
            : $"{existingContent}\n\n---\n⚠️ 生成过程中发生错误：{errorMessage}";

        var errorMsg = new ChatMessageDto
        {
            Id = currentMessageId,
            ChatSessionId = targetSessionId,
            SenderType = ChatConstants.RoleAssistant,
            Content = fullContent,
            TextContent = fullContent,
            ThinkingContent = string.IsNullOrEmpty(thoughtContent) ? null : thoughtContent,
            CreatedAt = DateTime.UtcNow
        };

        // 关键：重置指定会话的生成状态，解除 UI 卡死
        _chatState.SetSessionGeneratingOptimistic(targetSessionId, false);

        _logger.LogWarning("[生成错误] SessionId={SessionId}, Error={Error}", targetSessionId, errorMessage);

        return Task.FromResult<ChatMessageDto?>(errorMsg);
    }

    private static bool IsCancellationLikeError(string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            return false;
        }

        return errorMessage.Contains("cancel", StringComparison.OrdinalIgnoreCase)
               || errorMessage.Contains("canceled", StringComparison.OrdinalIgnoreCase)
               || errorMessage.Contains("cancelled", StringComparison.OrdinalIgnoreCase)
               || errorMessage.Contains("取消", StringComparison.OrdinalIgnoreCase)
               || errorMessage.Contains("已停止", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public Task<ChatMessageDto?> HandleGenerationCancelledAsync(
        Guid sessionId, List<BlockDto> currentBlocks, Guid currentMessageId)
    {
        var targetSessionId = sessionId == Guid.Empty ? _chatState.CurrentSessionId : sessionId;

        // 【防重入保护】如果会话已经不在生成状态，说明取消已经被处理过（如本地主动取消），直接返回确保幂等性
        if (!_chatState.GetSessionRunControl(targetSessionId).IsGenerationLike)
        {
            _logger.LogDebug("[生成取消-已处理] SessionId={SessionId}, 忽略重复取消广播", targetSessionId);
            return Task.FromResult<ChatMessageDto?>(null);
        }

        if (targetSessionId != _chatState.CurrentSessionId)
        {
            _chatState.SetSessionGeneratingOptimistic(targetSessionId, false);
            _logger.LogWarning("[生成取消-非当前会话] SessionId={SessionId}", targetSessionId);
            return Task.FromResult<ChatMessageDto?>(null);
        }

        var textContent = string.Join("", currentBlocks
            .Where(b => b.BlockType == BlockType.TextDelta).Select(b => b.Content));
        var thoughtContent = MetadataHelper.JoinThoughtSegments(currentBlocks
            .Where(b => b.BlockType == BlockType.Thinking).Select(b => b.Content));

        var existingContent = textContent;

        ChatMessageDto? cancelledMsg = null;
        if (!string.IsNullOrEmpty(existingContent) || !string.IsNullOrEmpty(thoughtContent))
        {
            cancelledMsg = new ChatMessageDto
            {
                Id = currentMessageId,
                ChatSessionId = targetSessionId,
                SenderType = ChatConstants.RoleAssistant,
                Content = existingContent + "\n\n(已取消生成)",
                TextContent = existingContent + "\n\n(已取消生成)",
                ThinkingContent = string.IsNullOrEmpty(thoughtContent) ? null : thoughtContent,
                CreatedAt = DateTime.UtcNow
            };
        }

        _chatState.SetSessionGeneratingOptimistic(targetSessionId, false);
        _logger.LogInformation("[生成取消] SessionId={SessionId}", targetSessionId);

        return Task.FromResult(cancelledMsg);
    }

    /// <inheritdoc />
    public List<Guid> RestoreGeneratingState(Guid sessionId, List<BlockDto> currentBlocks)
    {
        var messageIdsToRemove = new List<Guid>();

        if (_chatState.CurrentSessionId == sessionId)
        {
            var savedBlocks = _chatState.CurrentBlocks;
            if (savedBlocks != null && savedBlocks.Any())
            {
                currentBlocks.Clear();
                using var blockIndexer = new BlockIndexer();
                foreach (var savedBlock in savedBlocks)
                {
                    blockIndexer.AddBlock(CloneBlockForRestore(savedBlock));
                }

                currentBlocks.AddRange(blockIndexer.GetOrderedBlocks().Select(CloneBlockForRestore));

                var generatingMessageId = savedBlocks.FirstOrDefault()?.MessageId;
                if (generatingMessageId.HasValue && generatingMessageId.Value != Guid.Empty)
                {
                    messageIdsToRemove.Add(generatingMessageId.Value);
                }
            }
        }

        return messageIdsToRemove;
    }

    private static BlockDto CloneBlockForRestore(BlockDto block)
    {
        return new BlockDto
        {
            BlockType = block.BlockType,
            BlockId = block.BlockId,
            ArtifactId = block.ArtifactId,
            Version = block.Version,
            Action = block.Action,
            SessionId = block.SessionId,
            MessageId = block.MessageId,
            Content = block.Content,
            IsLast = block.IsLast,
            Highlight = block.Highlight,
            Metadata = block.Metadata == null
                ? null
                : new Dictionary<string, object>(block.Metadata)
        };
    }

}
