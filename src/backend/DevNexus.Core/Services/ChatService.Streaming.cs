using DevNexus.Core.Abstractions.Observability;
using DevNexus.Core.Services.Chat;
using DevNexus.Core.Services.Observability;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using DevNexus.Shared.Utils;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;
using System.Threading.Channels;

namespace DevNexus.Core.Services;

/// <summary>
/// 聊天服务 - 单 Agent 流式响应生成
/// </summary>
public partial class ChatService
{
    /// <summary>
    /// 流式生成 AI 响应（Producer：将 Block 写入 Channel）
    /// </summary>
    private async Task StreamAiResponseAsync(
        ChatMessage aiMessage,
        ChatSession chatSession,
        Guid userId,
        ChatMessage userMessage,
        ChatRequest chatRequest,
        ChannelWriter<BlockDto> blockWriter,
        CancellationToken cancellationToken,
        int agentLoopAttempt = 0)
    {
        // ★ 在 try 块外声明，以便在 catch 块中也能访问
        System.Text.StringBuilder? fullResponse = null;
        StreamBlockParser? blockParser = null;
        StringBuilder? preParserThinking = null;
        StringBuilder? partialTextBuffer = null;
        ThinkingEmitter? thinkingEmitter = null;
        ReasoningStreamAccumulator? reasoningAccumulator = null;

        try
        {
            // === 初始化分布式追踪上下文 ===
            // 为当前请求生成唯一的 TraceId，用于关联所有日志
            var traceSnapshot = TraceContext.BeginTrace();

            // 🟢 记录消息生成开始
            await _tracingService.LogStructuredEventAsync(
                TraceEvent.MessageGenerationStarted,
                "Information",
                $"开始生成消息 | SessionId={chatSession.Id} MessageId={aiMessage.Id} AttemptNumber={agentLoopAttempt}");

            // 在解析器初始化前先缓存系统级思维链，流结束时统一合并持久化
            // ✅ 设置思维链上下文
            thinkingEmitter = new ThinkingEmitter(
                blockWriter,
                chatSession.Id,
                aiMessage.Id,
                cancellationToken,
                _logger);
            ThinkingContext.SetEmitter(thinkingEmitter);

            // ✅ 设置执行上下文（供 HostService 等使用）
            ChatExecutionContext.Begin(
                chatSession.Id,
                aiMessage.Id,
                agentLoopAttempt,
                ResolveAgentApprovalMode(chatRequest.Metadata));

            preParserThinking = new StringBuilder();
            var existingThinking = GetMessageContentText(aiMessage, ChatMessageContentKeys.Thinking);
            if (!string.IsNullOrWhiteSpace(existingThinking))
            {
                preParserThinking.AppendLine(existingThinking);
            }
            partialTextBuffer = new StringBuilder();
            var lastTextPersistAt = DateTime.UtcNow;
            const int partialTextPersistThreshold = 256;
            const int partialTextPersistIntervalMs = 1000;

            reasoningAccumulator = new ReasoningStreamAccumulator();

            // 1. 解析 Provider 并构建聊天历史
            var preparation = await _chatStreamingPreparationService.PrepareAsync(
                chatSession,
                userMessage,
                userId,
                chatRequest,
                agentLoopAttempt,
                cancellationToken);
            var providerId = preparation.ProviderId;
            var userQuery = preparation.UserQuery;
            var chatHistory = preparation.ChatHistory;

            fullResponse = new System.Text.StringBuilder(GetMessageContentText(aiMessage, ChatMessageContentKeys.Text));

            _logger.LogDebug(
                "[AI.Chat] Starting streaming completion | SessionId={SessionId} MessageId={MessageId} RagEnabled={RagEnabled}",
                chatSession.Id,
                aiMessage.Id,
                chatRequest.EnableRag);

            // 创建流式 Block 解析器（替代 RenderPlugin）
            blockParser = new StreamBlockParser(_logger);

            // ★ 设置周期性持久化回调（用于长时间运行的任务）
            blockParser.SetPersistenceCallback(
                async (partialThinking, sessionId, messageId) =>
                {
                    await PersistPartialThinkingAsync(sessionId, messageId, partialThinking);
                });

            // 用于捕获 FinishReason（检测 max_tokens 截断）
            string? lastFinishReason = null;
            var toolExecutionTasks = new List<Task>();

            // ✅ 仅在首次调用时显示生成提示（避免递归时重复）
            if (agentLoopAttempt == 0)
            {
                await ThinkingContext.EmitAsync("🤖 正在生成回复...");
            }

            // 流式调用 LLM（不再使用 RenderPlugin，改为流式解析 Block 标记）
            await foreach (var streamContent in _IKernelService.StreamChatCompletionAsync(
                chatHistory,
                providerId,
                chatSession.Id,
                aiMessage.Id,
                userId,
                preparation.MatchedSkills,
                cancellationToken,
                enableAutoFunctionCalling: true,
                promptMetadata: preparation.PromptLayerMetadata == null
                    ? null
                    : new PromptOptimizationMetadataDto
                    {
                        PromptCacheKey = preparation.PromptLayerMetadata.PromptCacheKey,
                        StablePrefixHash = preparation.PromptLayerMetadata.StablePrefixHash,
                        ToolSchemaHash = preparation.PromptLayerMetadata.ToolSchemaHash,
                        DynamicContextTokens = preparation.PromptLayerMetadata.DynamicContextTokens,
                        HistoryTokens = preparation.PromptLayerMetadata.HistoryTokens,
                        CacheMarkerCandidateCount = preparation.PromptLayerMetadata.CacheMarkerCandidateCount,
                        CacheDoubleMarkerReady = preparation.PromptLayerMetadata.CacheDoubleMarkerReady,
                        CacheMarkerReadinessReason = preparation.PromptLayerMetadata.CacheMarkerReadinessReason,
                        StablePrefixManifest = PromptFragmentManifestMapper.ToDto(
                            preparation.PromptLayerMetadata.StablePrefixManifest),
                        DynamicContextManifest = PromptFragmentManifestMapper.ToDto(
                            preparation.PromptLayerMetadata.DynamicContextManifest)
                    }))
            {
                // 捕获 FinishReason（仅最后一个 chunk 有值："Stop" = 正常, "Length" = 截断）
                if (streamContent.Metadata?.TryGetValue("FinishReason", out var fr) == true && fr != null)
                {
                    lastFinishReason = fr.ToString();
                }

                // 健壮性改进：使用注入的服务提取 Content，避免反射崩溃风险
                var reasoningContent = _reasoningExtractor.Extract(streamContent.Metadata, streamContent.InnerContent);
                var reasoningDelta = reasoningAccumulator.GetDelta(reasoningContent);
                if (!string.IsNullOrEmpty(reasoningDelta))
                {
                    // 统一裁剪为真正的增量后再下发，避免快照式 reasoning 重复推送。
                    await blockParser.EmitThoughtBlockAsync(reasoningDelta, blockWriter, chatSession.Id, aiMessage.Id, cancellationToken);
                }

                // === 优先级 2: 使用 StreamBlockParser 解析 Block 标记 ===
                var content = streamContent.Content;
                if (string.IsNullOrEmpty(content))
                    continue;

                // 流式解析 Block 标记（:::chart, :::code, :::card, <search_web> 等）
                foreach (var block in blockParser.ParseChunk(content))
                {
                    block.SessionId = chatSession.Id;
                    block.MessageId = aiMessage.Id;

                    // 确保所有 Block 都有 BlockId（防止前端重复渲染）
                    if (block.BlockId == Guid.Empty)
                    {
                        block.BlockId = Guid.NewGuid();
                    }

                    if (block.BlockType == BlockType.TextDelta && !string.IsNullOrEmpty(block.Content))
                    {
                        fullResponse.Append(block.Content);
                        if (partialTextBuffer != null)
                        {
                            partialTextBuffer.Append(block.Content);
                            if (partialTextBuffer.Length >= partialTextPersistThreshold ||
                                (DateTime.UtcNow - lastTextPersistAt).TotalMilliseconds >= partialTextPersistIntervalMs)
                            {
                                var snapshot = partialTextBuffer.ToString();
                                partialTextBuffer.Clear();
                                lastTextPersistAt = DateTime.UtcNow;
                                await PersistPartialTextAsync(chatSession.Id, aiMessage.Id, snapshot);
                            }
                        }
                    }

                    await blockWriter.WriteAsync(block, cancellationToken);

                    if (_toolBlockExecutionCoordinator.CanHandle(block))
                    {
                        toolExecutionTasks.Add(_toolBlockExecutionCoordinator.HandleAsync(
                            block,
                            providerId,
                            aiMessage.Id,
                            chatSession.Id,
                            blockWriter,
                            cancellationToken));
                    }
                }
            }

            // 刷新解析器缓冲区，处理剩余内容
            foreach (var remainingBlock in blockParser.Flush())
            {
                remainingBlock.SessionId = chatSession.Id;
                remainingBlock.MessageId = aiMessage.Id;
                if (remainingBlock.BlockType == BlockType.TextDelta && !string.IsNullOrEmpty(remainingBlock.Content))
                {
                    fullResponse.Append(remainingBlock.Content);
                    if (partialTextBuffer != null)
                    {
                        partialTextBuffer.Append(remainingBlock.Content);
                        if (partialTextBuffer.Length >= partialTextPersistThreshold ||
                            (DateTime.UtcNow - lastTextPersistAt).TotalMilliseconds >= partialTextPersistIntervalMs)
                        {
                            var snapshot = partialTextBuffer.ToString();
                            partialTextBuffer.Clear();
                            lastTextPersistAt = DateTime.UtcNow;
                            await PersistPartialTextAsync(chatSession.Id, aiMessage.Id, snapshot);
                        }
                    }
                }
                await blockWriter.WriteAsync(remainingBlock, cancellationToken);
            }

            if (toolExecutionTasks.Count > 0)
            {
                await Task.WhenAll(toolExecutionTasks);
            }

            // ★ 检测 max_tokens 截断（FinishReason == "Length"）
            var isTruncated = "Length".Equals(lastFinishReason, StringComparison.OrdinalIgnoreCase);

            if (isTruncated)
            {
                _logger.LogWarning(
                    "[AI.Chat] Response truncated by max_tokens | SessionId={SessionId} MessageId={MessageId} ResponseLength={Length}",
                    chatSession.Id,
                    aiMessage.Id,
                    fullResponse.Length);

                // 发送截断通知 Block，前端显示"继续生成"按钮
                await blockWriter.WriteAsync(new BlockDto
                {
                    BlockType = BlockType.Truncated,
                    Content = "回复因 token 限制被截断",
                    MessageId = aiMessage.Id,
                    SessionId = chatSession.Id,
                    IsLast = false,
                    Metadata = new Dictionary<string, object>
                    {
                        { TruncatedBlockMetadataConstants.Reason, TruncatedBlockMetadataConstants.ReasonMaxTokens },
                        { TruncatedBlockMetadataConstants.CanContinue, true }
                    }
                });
            }

            var parserThinking = blockParser.GetAccumulatedThinking();
            var preThinking = preParserThinking?.ToString() ?? string.Empty;
            var contextThinking = thinkingEmitter.GetAccumulatedThinking();
            var toolRecords = ChatExecutionContext.GetToolRecordsSnapshot().ToList();
            var agentLoopDecision = await _chatAgentLoopCoordinator.HandleAsync(
                chatSession.Id,
                userId,
                providerId,
                userQuery,
                fullResponse.ToString(),
                aiMessage,
                toolRecords,
                agentLoopAttempt,
                blockWriter,
                cancellationToken);

            if (agentLoopDecision.Action == AgentLoopAction.Retry && agentLoopDecision.RepairMessage != null)
            {
                await StreamAiResponseAsync(
                    aiMessage,
                    chatSession,
                    userId,
                    agentLoopDecision.RepairMessage,
                    chatRequest,
                    blockWriter,
                    cancellationToken,
                    agentLoopAttempt + 1);

                return;
            }

            var pendingMemoryDecision = MemoryConsolidationTriggerPolicy.Decide(
                await _chatMessageRepository.CountBySessionAsync(chatSession.Id, CancellationToken.None),
                chatSession.LastConsolidatedMessageCount,
                MemoryConsolidationMessageThreshold,
                minimumDelayedMessageCount: 3,
                preparation.PromptLayerMetadata?.HistoryGovernance,
                !string.IsNullOrEmpty(chatSession.MemoryConsolidationJobId));
            var pendingTaskSnapshot = AgentTaskOrchestrationSnapshotBuilder.Build(
                aiMessage.Id,
                agentLoopAttempt,
                agentLoopDecision.Action,
                preparation.PromptLayerMetadata?.HistoryGovernance,
                preparation.PromptLayerMetadata?.SystemExperienceReplay,
                pendingMemoryDecision,
                toolRecords,
                fullResponse?.Length ?? 0);
            var selfIterationCandidate = EvaluateExperienceDistillationCandidate(pendingTaskSnapshot);

            var completedResponse = fullResponse?.ToString() ?? string.Empty;
            aiMessage.Metadata ??= new Dictionary<string, object>();
            SelfIterationCandidateMetadata.Apply(aiMessage.Metadata, selfIterationCandidate);
            await _chatStreamingFinalizer.FinalizeCompletedAsync(
                aiMessage,
                chatSession.Id,
                completedResponse,
                isTruncated,
                preParserThinking,
                parserThinking,
                contextThinking,
                CancellationToken.None);

            await _chatMessageCompletionCoordinator.HandleCompletedAsync(
                chatSession,
                aiMessage,
                userId,
                agentLoopAttempt,
                fullResponse?.Length ?? 0,
                includeExperienceDistillation: selfIterationCandidate.ShouldDistillExperience,
                selfIterationCandidate: selfIterationCandidate,
                cancellationToken: CancellationToken.None);

            // 触发记忆沉淀检查
            var memoryDecision = await TriggerMemoryConsolidationCheckAsync(
                chatSession,
                userId,
                preparation.PromptLayerMetadata?.HistoryGovernance,
                CancellationToken.None);
            LogTaskOrchestrationSnapshot(
                aiMessage.Id,
                agentLoopAttempt,
                agentLoopDecision.Action,
                preparation.PromptLayerMetadata?.HistoryGovernance,
                preparation.PromptLayerMetadata?.SystemExperienceReplay,
                memoryDecision,
                toolRecords);

            await WriteTerminalBlockAsync(aiMessage.Id, chatSession.Id, blockWriter, cancellationToken);

            if (agentLoopDecision.Action == AgentLoopAction.Stop)
            {
                return;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug(
                "[AI.Chat] Streaming cancelled by user | SessionId={SessionId} MessageId={MessageId} GeneratedLength={Length}",
                chatSession.Id,
                aiMessage.Id,
                fullResponse?.Length ?? 0);

            // 🟡 记录消息生成被取消
            await _tracingService.LogStructuredEventAsync(
                TraceEvent.MessageGenerationCancelled,
                "Warning",
                $"消息生成被用户取消 | SessionId={chatSession.Id} MessageId={aiMessage.Id}");

            await ThinkingContext.EmitAsync("⏹️ 已取消本次生成。");

            await _chatStreamingFinalizer.FinalizeCancelledAsync(
                aiMessage,
                chatSession.Id,
                fullResponse?.ToString(),
                preParserThinking,
                blockParser?.GetAccumulatedThinking(),
                thinkingEmitter?.GetAccumulatedThinking() ?? string.Empty,
                fullResponse != null && blockParser != null,
                CancellationToken.None);

            throw;
        }
        catch (Exception ex)
        {
            // 🔴 记录异常
            await _tracingService.LogStructuredEventAsync(
                TraceEvent.UnexpectedError,
                "Error",
                $"消息生成异常 | SessionId={chatSession.Id} MessageId={aiMessage.Id}",
                ex);

            // 提取详细错误信息
            var errorDetails = ChatErrorDetailExtractor.Extract(ex);

            _logger.LogError(
                ex,
                "[AI.Chat] Streaming error | SessionId={SessionId} MessageId={MessageId} ErrorType={ErrorType} Details={Details}",
                chatSession.Id,
                aiMessage.Id,
                ex.GetType().Name,
                errorDetails);

            var renderedError = await _chatStreamingFinalizer.FinalizeErroredAsync(
                aiMessage,
                fullResponse?.ToString(),
                errorDetails,
                CancellationToken.None);

            await TryWriteErrorTerminalBlockAsync(
                aiMessage.Id,
                chatSession.Id,
                renderedError,
                blockWriter,
                _logger,
                cancellationToken);

            // 移除 throw; 使得外部当做普通消息完成，从而推送流终止状态和完成事件
        }
        finally
        {
            // 清理执行上下文（在 Agent Loop 评估完成后释放）
            ChatExecutionContext.Clear();
            // ✅ 清理思维链上下文
            ThinkingContext.Clear();
        }
    }

    private static AgentApprovalMode ResolveAgentApprovalMode(IReadOnlyDictionary<string, object>? metadata)
    {
        if (metadata == null
            || !metadata.TryGetValue(ChatMessageMetadataKeys.AgentApprovalMode, out var value)
            || value == null)
        {
            return AgentApprovalMode.AskUser;
        }

        return Enum.TryParse<AgentApprovalMode>(value.ToString(), ignoreCase: true, out var mode)
            ? mode
            : AgentApprovalMode.AskUser;
    }

    private static async Task WriteTerminalBlockAsync(
        Guid messageId,
        Guid sessionId,
        ChannelWriter<BlockDto> blockWriter,
        CancellationToken cancellationToken)
    {
        await blockWriter.WriteAsync(new BlockDto
        {
            BlockType = BlockType.TextDelta,
            Content = string.Empty,
            MessageId = messageId,
            SessionId = sessionId,
            IsLast = true
        }, cancellationToken);
    }

    private static async Task TryWriteErrorTerminalBlockAsync(
        Guid messageId,
        Guid sessionId,
        string renderedError,
        ChannelWriter<BlockDto> blockWriter,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var block = new BlockDto
        {
            BlockType = BlockType.TextDelta,
            Content = renderedError,
            MessageId = messageId,
            SessionId = sessionId,
            IsLast = true
        };

        try
        {
            if (blockWriter.TryWrite(block))
            {
                return;
            }

            if (await blockWriter.WaitToWriteAsync(cancellationToken))
            {
                await blockWriter.WriteAsync(block, cancellationToken);
            }
        }
        catch (ChannelClosedException ex)
        {
            logger.LogDebug(ex,
                "[AI.Chat] Error terminal block skipped because stream channel is closed | SessionId={SessionId} MessageId={MessageId}",
                sessionId,
                messageId);
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug(ex,
                "[AI.Chat] Error terminal block skipped because stream channel is cancelled | SessionId={SessionId} MessageId={MessageId}",
                sessionId,
                messageId);
        }
    }
}
