using DevNexus.Core.Services.Swarm.Analysis;
using DevNexus.Core.Services.Chat;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Threading.Channels;

namespace DevNexus.Core.Services;

/// <summary>
/// 聊天服务 - Swarm 集群编排桥接
/// </summary>
public partial class ChatService
{
    /// <summary>
    /// 执行 Swarm 上下文工作包协同链路，并将结果写入 BlockDto Channel。
    /// 支持即时反馈、流式进度推送和部分结果降级。
    /// </summary>
    private async Task ExecuteSwarmExecutionAsync(
        ChatMessage aiMessage,
        ChatSession chatSession,
        string userRequest,
        Guid providerId,
        ComplexityVector complexity,
        ChannelWriter<BlockDto> blockWriter,
        CancellationToken cancellationToken)
    {
        // ★ 创建思维链累积器（拦截所有 Thinking 块）
        var thinkingAccumulator = new StringBuilder();
        var wrappedWriter = new ThinkingAccumulatingChannelWriter(blockWriter, thinkingAccumulator);

        // ★ 在 try 块外声明，以便在 catch 块中也能访问
        string? swarmResult = null;

        // ★ 设置周期性持久化回调（Swarm 长任务保护）
        wrappedWriter.SetPersistenceCallback(
            async (partialThinking) =>
            {
                await PersistPartialThinkingAsync(chatSession.Id, aiMessage.Id, partialThinking);
            });

        try
        {
            // 1. 即时反馈：立即向聊天流写入提示，消除用户等待焦虑
            await wrappedWriter.WriteAsync(new BlockDto
            {
                BlockType = BlockType.TextDelta,
                Content = "🚀 **Swarm 多智能体集群已启动**\n\n"
                    + $"> 复杂度评分: **{complexity.CompositeScore:F1}** | 领域: **{complexity.PrimaryDomain}**\n\n"
                    + "您可以点击上方按钮查看实时执行拓扑图。\n\n---\n\n",
                MessageId = aiMessage.Id,
                SessionId = chatSession.Id,
                IsLast = false,
                Metadata = new Dictionary<string, object> { ["swarmEvent"] = SwarmEventNames.Started }
            }, cancellationToken);

            // 2. 发送 Swarm 开始事件（通知 SwarmMonitor 侧边栏）
            await _swarmEventService.NotifySwarmStartedAsync(
                chatSession.Id.ToString(),
                cancellationToken);

            // 3. 调用 Swarm 编排器执行（流式版本：中间进度通过 blockWriter 推送）
            swarmResult = await _swarmOrchestrator.OrchestrateAsync(
                userRequest, providerId, chatSession.Id.ToString(),
                chatSession.UserId, complexity, wrappedWriter, cancellationToken);
            bool isTruncated = false;
            const string truncationMarker = "\n\n[TRUNCATED: maximum automatic continuations reached]";
            if (swarmResult.EndsWith(truncationMarker) || swarmResult.Contains(truncationMarker))
            {
                isTruncated = true;
                swarmResult = swarmResult.Replace(truncationMarker, string.Empty);
            }

            // 4. 将 Swarm 汇总结果写入 Block Channel
            await blockWriter.WriteAsync(new BlockDto
            {
                BlockType = BlockType.TextDelta,
                Content = swarmResult,
                MessageId = aiMessage.Id,
                SessionId = chatSession.Id,
                IsLast = false
            }, cancellationToken);

            // 发送终止 Block 或者 Truncated Block
            if (isTruncated)
            {
                _logger.LogWarning(
                    "[AI.Swarm] Result truncated after max continuations | SessionId={SessionId} MessageId={MessageId}",
                    chatSession.Id, aiMessage.Id);

                // 发送截断通知 Block，前端显示"继续生成"按钮
                await blockWriter.WriteAsync(new BlockDto
                {
                    BlockType = BlockType.Truncated,
                    Content = "回复因长度限制未能完全展开（已达到最大自动续写次数）",
                    MessageId = aiMessage.Id,
                    SessionId = chatSession.Id,
                    IsLast = false,
                    Metadata = new Dictionary<string, object>
                    {
                        { TruncatedBlockMetadataConstants.Reason, TruncatedBlockMetadataConstants.ReasonMaxAutoContinuationsReached },
                        { TruncatedBlockMetadataConstants.CanContinue, true }
                    }
                });
            }

            // 必然要发送最后的标记 Block (告诉流结束)
            await blockWriter.WriteAsync(new BlockDto
            {
                BlockType = BlockType.TextDelta,
                Content = string.Empty,
                MessageId = aiMessage.Id,
                SessionId = chatSession.Id,
                IsLast = true
            }, cancellationToken);

            var thinkingContent = thinkingAccumulator.ToString();
            thinkingContent = await MergeExternalThinkingForPersistenceAsync(aiMessage.Id, thinkingContent);
            await _chatSwarmFinalizer.FinalizeCompletedAsync(
                aiMessage,
                chatSession,
                swarmResult,
                isTruncated,
                thinkingContent,
                blockWriter,
                CancellationToken.None);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug(
                "[AI.Swarm] Swarm orchestration cancelled by user | SessionId={SessionId} MessageId={MessageId}",
                chatSession.Id,
                aiMessage.Id);

            var accumulatedThinking = thinkingAccumulator.ToString();
            accumulatedThinking = await MergeExternalThinkingForPersistenceAsync(aiMessage.Id, accumulatedThinking);
            await _chatSwarmFinalizer.FinalizeCancelledAsync(
                aiMessage,
                chatSession,
                accumulatedThinking,
                blockWriter,
                CancellationToken.None);

            throw;
        }
        catch (Exception ex)
        {
            var errorDetails = ChatErrorDetailExtractor.Extract(ex);
            _logger.LogError(ex,
                "[AI.Swarm] Swarm orchestration failed | SessionId={SessionId} Error={Error}",
                chatSession.Id, errorDetails);

            await _chatSwarmFinalizer.FinalizeFailedAsync(
                aiMessage,
                chatSession,
                errorDetails,
                blockWriter,
                CancellationToken.None);
        }
    }
}
