// using DevNexus.Domain.Entities via GlobalUsings
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using DevNexus.Shared.Constants;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DevNexus.Core.Services;

/// <summary>
/// 聊天服务 - 消息处理部分
/// </summary>
public partial class ChatService
{


    /// <inheritdoc />
    public async Task CancelMessageGenerationAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Cancelling message generation for session {SessionId}",
            sessionId);

        try
        {
            // ★ 首先确保 Swarm 任务停止（如果这个会话是通过 Swarm 升级的）
            await _swarmSessionControlService.AbortAsync(sessionId.ToString(), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to abort Swarm session {SessionId}", sessionId);
        }

        if (_generationCancellationRegistry.Cancel(sessionId))
        {
            // Registry 已移除会话注册并向流式操作发出取消信号。

            // ★ 重要：不要在这里修改消息状态或内容！
            // Stream 中的 catch(OperationCanceledException) 块会负责：
            // 1. 保存所有已生成的内容（fullResponse）
            // 2. 合并思维链（getAccumulatedThinking）
            // 3. 设置状态为 StatusCancelled
            // 4. 调用 SaveChangesAsync()
            //
            // 在这里修改会导致竞态条件（race condition）：
            // - CancelMessageGenerationAsync 和 OperationCanceledException catch 块同时竞争同一条数据库记录
            // - 可能导致 catch 块中保存的内容被这里的 SaveChangesAsync 覆盖
            //
            // 因此，最安全的做法是：
            // 1. 发出取消信号（Cancel()）✅ 
            // 2. 等待 catch 块完成保存 ✅（通过 IAsyncEnumerator 自动完成）
            // 3. 不要在这里额外调用 SaveChangesAsync()

            _logger.LogInformation(
                "[Cancel] Message generation cancelled for session {SessionId}, awaiting stream completion...",
                sessionId);
        }
        else
        {
            _logger.LogDebug(
                "[Cancel] No active generation found for session {SessionId} (already completed or not found)",
                sessionId);
        }
    }


    private static List<ChatMessage> CollectMessageBranchForDeletion(
        IReadOnlyList<ChatMessage> sessionMessages,
        IReadOnlyCollection<Guid> rootMessageIds)
    {
        if (sessionMessages.Count == 0 || rootMessageIds.Count == 0)
        {
            return new List<ChatMessage>();
        }

        var messagesById = sessionMessages.ToDictionary(message => message.Id);
        var childIdsByParentId = sessionMessages
            .Where(message => message.ParentMessageId.HasValue)
            .GroupBy(message => message.ParentMessageId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.Select(message => message.Id).ToList());

        var messageIdsToDelete = new HashSet<Guid>();
        var pendingMessageIds = new Queue<Guid>();

        foreach (var rootMessageId in rootMessageIds)
        {
            if (!messagesById.ContainsKey(rootMessageId) || !messageIdsToDelete.Add(rootMessageId))
            {
                continue;
            }

            pendingMessageIds.Enqueue(rootMessageId);
        }

        while (pendingMessageIds.Count > 0)
        {
            var currentMessageId = pendingMessageIds.Dequeue();

            if (!childIdsByParentId.TryGetValue(currentMessageId, out var childMessageIds))
            {
                continue;
            }

            foreach (var childMessageId in childMessageIds)
            {
                if (messageIdsToDelete.Add(childMessageId))
                {
                    pendingMessageIds.Enqueue(childMessageId);
                }
            }
        }

        return sessionMessages
            .Where(message => messageIdsToDelete.Contains(message.Id))
            .ToList();
    }

    /// <inheritdoc />
    public async Task DeleteChatMessageAsync(
        Guid messageId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Deleting message {MessageId} for user {UserId}",
            messageId,
            userId);

        var message = await _chatMessageRepository.GetByIdWithSessionAsync(messageId, cancellationToken);

        if (message == null)
        {
            throw new InvalidOperationException($"消息 {messageId} 不存在");
        }

        if (message.ChatSession.UserId != userId)
        {
            throw new UnauthorizedAccessException("User is not authorized to delete this message");
        }

        await CancelMessageGenerationAsync(message.ChatSessionId, cancellationToken);

        var sessionMessages = await _chatMessageRepository.ListBySessionAsync(message.ChatSessionId, cancellationToken);
        var messagesToDelete = CollectMessageBranchForDeletion(sessionMessages, new[] { messageId });

        if (messagesToDelete.Count == 0)
        {
            throw new InvalidOperationException($"消息 {messageId} 不存在");
        }

        await _executionStrategyExecutor.ExecuteAsync(async ct =>
        {
            await using var transaction = await _unitOfWorkTransactionFactory.BeginTransactionAsync(ct);

            try
            {
                await _chatMessageRepository.DeleteRangeAsync(messagesToDelete, ct);
                await transaction.CommitAsync(ct);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete message branch rooted at {MessageId}", messageId);
                throw;
            }
        }, cancellationToken);

        var deletedMessageIds = messagesToDelete
            .Select(item => item.Id)
            .Distinct()
            .ToList();

        // 从 Elasticsearch 中删除索引
        try 
        {
            await _chatSearchService.DeleteMessagesFromElasticsearchAsync(deletedMessageIds, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete message from search index");
        }

        _logger.LogInformation(
            "Deleted {Count} messages in branch rooted at {MessageId}",
            deletedMessageIds.Count,
            messageId);
    }

    /// <inheritdoc />
    public async Task<int> DeleteChatMessagesAsync(
        Guid sessionId,
        List<Guid> messageIds,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (messageIds == null || messageIds.Count == 0)
        {
            return 0;
        }

        _logger.LogDebug(
            "Batch deleting {Count} messages from session {SessionId} for user {UserId}",
            messageIds.Count,
            sessionId,
            userId);

        // 1. 验证会话所有权
        var session = await _chatSessionRepository.GetByIdAsync(userId, sessionId, cancellationToken);

        if (session == null)
        {
            throw new UnauthorizedAccessException($"User {userId} is not authorized to modify session {sessionId}");
        }

        await CancelMessageGenerationAsync(sessionId, cancellationToken);

        // 2. 使用执行策略处理事务（适配 NpgsqlRetryingExecutionStrategy）
        return await _executionStrategyExecutor.ExecuteAsync(async ct =>
        {
            await using var transaction = await _unitOfWorkTransactionFactory.BeginTransactionAsync(ct);
            
            try
            {
                // 3. 批量查询会话消息，并扩展为完整的对话分支删除集合
                var sessionMessages = await _chatMessageRepository.ListBySessionAsync(sessionId, ct);
                var messagesToDelete = CollectMessageBranchForDeletion(sessionMessages, messageIds);

                if (messagesToDelete.Count == 0)
                {
                    _logger.LogWarning("No messages found to delete in session {SessionId}", sessionId);
                    return 0;
                }

                if (messagesToDelete.Count != messageIds.Count)
                {
                    _logger.LogInformation(
                        "Expanded batch delete request from {RequestedCount} to {ExpandedCount} messages in session {SessionId}",
                        messageIds.Count,
                        messagesToDelete.Count,
                        sessionId);
                }

                var deletedMessageIds = messagesToDelete
                    .Select(item => item.Id)
                    .Distinct()
                    .ToList();

                // 4. 批量删除消息
                await _chatMessageRepository.DeleteRangeAsync(messagesToDelete, ct);

                // 5. 提交事务
                await transaction.CommitAsync(ct);

                // 6. 同步删除索引 (事务提交后执行)
                try
                {
                    await _chatSearchService.DeleteMessagesFromElasticsearchAsync(deletedMessageIds, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete messages from search index");
                }

                _logger.LogInformation(
                    "Successfully deleted {Count} messages from session {SessionId}",
                    deletedMessageIds.Count,
                    sessionId);

                return deletedMessageIds.Count;
            }
            catch (Exception ex)
            {
                // 记录错误并抛出，让重试策略决定是否重试
                _logger.LogError(ex, "Failed to batch delete messages from session {SessionId}", sessionId);
                throw;
            }
        }, cancellationToken);
    }

    /// <summary>
    /// 创建用户消息
    /// </summary>
    /// <param name="chatRequest">聊天请求</param>
    /// <param name="chatSession">聊天会话</param>
    /// <param name="userId">用户ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>聊天消息</returns>
    private async Task<ChatMessage> CreateUserMessageAsync(
        ChatRequest chatRequest,
        ChatSession chatSession,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        Dictionary<string, object>? messageMetadata = null;
        if (chatRequest.Metadata?.Count > 0)
        {
            messageMetadata = new Dictionary<string, object>(chatRequest.Metadata, StringComparer.OrdinalIgnoreCase);
        }

        if (!string.IsNullOrWhiteSpace(chatRequest.SelectedSkillName))
        {
            messageMetadata ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            messageMetadata[ChatMessageMetadataKeys.SelectedSkillName] = chatRequest.SelectedSkillName.Trim();
        }

        var message = new ChatMessage
        {
            ChatSessionId = chatSession.Id,
            ParentMessageId = chatRequest.ParentMessageId,
            SenderId = userId,
            SenderType = ChatConstants.RoleUser,
            Content = new Dictionary<string, object>
            {
                { ChatMessageContentKeys.Text, chatRequest.Content }
            },
            MessageType = ChatConstants.NormalizeMessageType(chatRequest.MessageType),
            Status = ChatConstants.StatusCompleted,
            Metadata = messageMetadata
        };

        await _chatMessageRepository.AddAsync(message, cancellationToken);
        
        // 关联附带的 Artifact 到此消息
        if (chatRequest.ArtifactIds?.Any() == true)
        {
            await _artifactService.LinkArtifactsToMessageAsync(
                chatRequest.ArtifactIds,
                message.Id,
                cancellationToken);
            
            _logger.LogInformation(
                "[Chat.Message] Linked {Count} artifacts to user message | MessageId={MessageId}",
                chatRequest.ArtifactIds.Count,
                message.Id);
        }

        return message;
    }

    /// <inheritdoc />
    public async Task<ChatMessageDto> SaveSystemMessageAsync(
        Guid sessionId,
        string content,
        Guid? relatedMessageId = null,
        string type = ChatConstants.MessageTypeSystem,
        CancellationToken cancellationToken = default)
    {
        // 1. 获取会话
        var chatSession = await _chatSessionRepository.GetByIdAsync(sessionId, cancellationToken);

        if (chatSession == null)
        {
            throw new InvalidOperationException($"会话 {sessionId} 不存在");
        }

        // 2. 创建消息
        var message = new ChatMessage
        {
            Id = Guid.NewGuid(),
            ChatSessionId = sessionId,
            ParentMessageId = relatedMessageId,
            SenderId = chatSession.UserId, 
            SenderType = ChatConstants.NormalizeSenderType(type, ChatConstants.RoleSystem), 
            Content = new Dictionary<string, object>
            {
                { ChatMessageContentKeys.Text, content }
            },
            MessageType = ChatConstants.MessageTypeText,
            Status = ChatConstants.StatusCompleted,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // 3. 保存
        await _chatMessageRepository.AddAsync(message, cancellationToken);

        // 4. 返回 DTO
        return new ChatMessageDto
        {
            Id = message.Id,
            ChatSessionId = message.ChatSessionId,
            ParentMessageId = message.ParentMessageId,
            SenderId = message.SenderId,
            SenderType = message.SenderType,
            Content = content,
            MessageType = message.MessageType,
            Status = message.Status,
            CreatedAt = message.CreatedAt,
            UpdatedAt = message.UpdatedAt,
            Metadata = message.Metadata
        };
    }

    /// <summary>
    /// 加载终端流（仅从独立表读取）
    /// </summary>
    private async Task<Dictionary<Guid, List<TerminalStreamSnapshot>>> LoadTerminalStreamsByMessageAsync(
        IReadOnlyList<ChatMessage> messages,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, List<TerminalStreamSnapshot>>();

        try
        {
            var messageIds = messages
                .Select(message => message.Id)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToArray();

            if (messageIds.Length == 0)
            {
                return result;
            }

            var dbStreams = await _terminalStreamRepository.GetByMessageIdsAsync(messageIds, cancellationToken);
            if (dbStreams.Count == 0)
            {
                return result;
            }

            _logger.LogDebug(
                "[ChatService.LoadTerminalStreams] Loaded {Count} streams for {MessageCount} messages",
                dbStreams.Count,
                messageIds.Length);

            foreach (var stream in dbStreams)
            {
                if (!stream.MessageId.HasValue || stream.MessageId.Value == Guid.Empty)
                {
                    continue;
                }

                if (!result.TryGetValue(stream.MessageId.Value, out var snapshots))
                {
                    snapshots = new List<TerminalStreamSnapshot>();
                    result[stream.MessageId.Value] = snapshots;
                }

                snapshots.Add(new TerminalStreamSnapshot
                {
                    TerminalStreamId = stream.Id,
                    SessionKey = stream.SessionKey,
                    ToolCallId = stream.ToolCallId,
                    Command = stream.Command,
                    WorkingDirectory = stream.WorkingDirectory,
                    AttemptNumber = stream.AttemptNumber,
                    IsRetry = stream.IsRetry,
                    Status = stream.Status.ToWireValue(),
                    SessionState = stream.SessionState.ToWireValue(),
                    RuntimeHost = stream.RuntimeHost,
                    ExitCode = stream.ExitCode,
                    WaitingForInput = stream.WaitingForInput,
                    WaitingForInputSince = stream.WaitingForInputSince,
                    TerminationReason = stream.TerminationReason,
                    StartedAt = stream.StartedAt,
                    LastActivityAt = stream.LastActivityAt,
                    IsActive = stream.SessionState.IsActive(),
                    Output = stream.Output,
                    HasArchivedOutput = stream.HasArchivedOutput,
                    OutputLength = stream.OutputLength,
                    OutputLineCount = stream.OutputLineCount,
                    WatchSummary = stream.WatchSummary
                });
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[ChatService.LoadTerminalStreams] Failed to batch load terminal streams");

            return result;
        }
    }

    /// <summary>
    /// 终端流快照（用于回放）
    /// </summary>
    private class TerminalStreamSnapshot
    {
        public Guid TerminalStreamId { get; set; }
        public string? SessionKey { get; set; }
        public Guid? ToolCallId { get; set; }
        public string Command { get; set; } = string.Empty;
        public string? WorkingDirectory { get; set; }
        public int AttemptNumber { get; set; }
        public bool IsRetry { get; set; }
        public string Status { get; set; } = TerminalStreamStatus.Completed.ToWireValue();
        public string SessionState { get; set; } = CliSessionState.Completed.ToWireValue();
        public string? RuntimeHost { get; set; }
        public int? ExitCode { get; set; }
        public bool WaitingForInput { get; set; }
        public DateTime? WaitingForInputSince { get; set; }
        public string? TerminationReason { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? LastActivityAt { get; set; }
        public bool IsActive { get; set; }
        public string Output { get; set; } = string.Empty;
        public bool HasArchivedOutput { get; set; }
        public int OutputLength { get; set; }
        public int OutputLineCount { get; set; }
        public string? WatchSummary { get; set; }
    }
}
