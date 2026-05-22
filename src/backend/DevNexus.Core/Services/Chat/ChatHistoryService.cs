// using DevNexus.Domain.Abstractions via GlobalUsings
// using removed - LLM moved to Infrastructure
// using DevNexus.Domain.Entities via GlobalUsings
using DevNexus.Shared.Constants;
using DevNexus.Core.Abstractions;
using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 聊天历史服务 - 负责构建和管理聊天上下文
/// 采用 RAG 优先策略
/// </summary>
public class ChatHistoryService
{
    private readonly ChatPromptService _chatPromptService;
    private readonly ChatSystemPromptBuilder _systemPromptBuilder;
    private readonly ChatHistoryMessageBuilder _messageBuilder;
    private readonly ChatHistorySummaryService _summaryService;
    private readonly IToolCatalogService _toolCatalogService;
    private readonly ILogger<ChatHistoryService> _logger;

    public ChatHistoryService(
        ChatPromptService chatPromptService,
        ChatSystemPromptBuilder systemPromptBuilder,
        ChatHistoryMessageBuilder messageBuilder,
        ChatHistorySummaryService summaryService,
        IToolCatalogService toolCatalogService,
        ILogger<ChatHistoryService> logger)
    {
        _chatPromptService = chatPromptService;
        _systemPromptBuilder = systemPromptBuilder;
        _messageBuilder = messageBuilder;
        _summaryService = summaryService;
        _toolCatalogService = toolCatalogService;
        _logger = logger;
    }

    /// <summary>
    /// 构建聊天历史（RAG 优先策略）
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="userId">用户ID（用于 RAG 检索隔离）</param>
    /// <param name="providerId">Provider ID</param>
    /// <param name="currentMessage">当前用户消息（用于 RAG 检索）</param>
    /// <param name="enableRag">是否启用知识库检索</param>
    /// <param name="activeArtifactIds">当前请求显式激活的 Artifact 列表（优先全文注入）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<ChatHistoryResult> BuildChatHistoryAsync(
        Guid sessionId,
        Guid userId,
        Guid? providerId,
        string? currentMessage = null,
        string? selectedSkillName = null,
        Dictionary<string, object>? requestMetadata = null,
        bool enableRag = true,
        IEnumerable<Guid>? activeArtifactIds = null,
        CancellationToken cancellationToken = default)
    {
        var chatHistory = new ChatHistory();
        var promptBuildResult = await _systemPromptBuilder.BuildAsync(
            sessionId,
            userId,
            providerId,
            currentMessage,
            selectedSkillName,
            requestMetadata,
            cancellationToken);
        var maxContextTokens = promptBuildResult.MaxContextTokens;

        // 计算各部分的 Token 预算
        int maxTotalTokens = maxContextTokens - AiOptimizationConstants.OutputReservedTokens;
        int maxHistoryTokens = (int)(maxTotalTokens * AiOptimizationConstants.HistoryTokenBudgetRatio);

        chatHistory.AddSystemMessage(promptBuildResult.Prompt);
        int systemTokens = EstimateTokenCount(promptBuildResult.Prompt);
        if (!string.IsNullOrWhiteSpace(promptBuildResult.DynamicContextMessage))
        {
            chatHistory.AddUserMessage(promptBuildResult.DynamicContextMessage);
        }

        if (!string.IsNullOrWhiteSpace(promptBuildResult.CriticalPrompt))
        {
            chatHistory.AddSystemMessage(promptBuildResult.CriticalPrompt);
            systemTokens += EstimateTokenCount(promptBuildResult.CriticalPrompt);
        }

        // ★ 构建历史对话消息 (RAG 为辅，直接加载最近对话)
        // 注意：如果是历史中的终端输出，LLM 本身并不需要看到所有的终端原始 Raw 字符流，
        // 但为了保持上下文完整，我们会保留已经格式化的 content 主体。
        // 持久化的终端分片主要用于前端 UI 还原。

        // 构建 Artifacts 上下文（使用 RAG 优先策略）
        var artifactsContext = await _chatPromptService.GetArtifactsContextMessageAsync(
            sessionId,
            userId,
            providerId,
            currentMessage,
            enableRag,
            activeArtifactIds,
            cancellationToken);

        int artifactsTokens = EstimateTokenCount(artifactsContext ?? string.Empty);

        if (!string.IsNullOrEmpty(artifactsContext))
        {
            var artifactsContextMessage = PromptDynamicContextMessageBuilder.Build(
                "文档与 RAG 上下文",
                artifactsContext);
            chatHistory.AddUserMessage(artifactsContextMessage ?? artifactsContext);
            _logger.LogDebug("[AI.Chat] AI 聊天： Added RAG context message | SessionId={SessionId} Tokens={Tokens}",
                sessionId, artifactsTokens);
        }

        // 计算已使用的 Token（系统提示 + Artifacts 上下文）
        int usedTokens = systemTokens + artifactsTokens;
        int remainingTokenBudget = Math.Min(maxHistoryTokens, maxTotalTokens - usedTokens);

        _logger.LogDebug(
            "[AI.Chat] Token budget | MaxContext={MaxContext} SystemTokens={System} ArtifactsTokens={Artifacts} HistoryBudget={History}",
            maxContextTokens, systemTokens, artifactsTokens, remainingTokenBudget);

        // 构建历史消息
        var historyGovernance = await _messageBuilder.AppendHistoryMessagesAsync(
            chatHistory,
            sessionId,
            providerId,
            remainingTokenBudget,
            cancellationToken);

        var actualHistoryTokens = historyGovernance.ConsumedTokens;
        if (promptBuildResult.LayerMetadata != null)
        {
            var cacheMarkerPlan = PromptCacheMarkerPlanner.Plan(chatHistory);
            promptBuildResult.LayerMetadata.HistoryTokens = actualHistoryTokens;
            promptBuildResult.LayerMetadata.HistoryGovernance = historyGovernance;
            promptBuildResult.LayerMetadata.ToolSchemaHash = _toolCatalogService.ComputeSchemaHash();
            promptBuildResult.LayerMetadata.PromptCacheKey = PromptCacheKeyBuilder.Build(
                promptBuildResult.LayerMetadata.StablePrefixHash,
                promptBuildResult.LayerMetadata.ToolSchemaHash);
            promptBuildResult.LayerMetadata.CacheMarkerCandidateCount = cacheMarkerPlan.MarkerIndexes.Count;
            promptBuildResult.LayerMetadata.CacheDoubleMarkerReady = cacheMarkerPlan.IsDoubleMarkerReady;
            promptBuildResult.LayerMetadata.CacheMarkerReadinessReason = cacheMarkerPlan.ReadinessReason;
        }

        _logger.LogDebug(
            "[AI.Context.Governance] History built | SessionId={SessionId} Strategy={Strategy} BudgetTokens={BudgetTokens} " +
            "ConsumedTokens={ConsumedTokens} Fetched={Fetched} Replayable={Replayable} Direct={Direct} " +
            "Compressed={Compressed} Summary={Summary} Recent={Recent} SkippedRepair={SkippedRepair} " +
            "SkippedIncompleteAssistant={SkippedIncompleteAssistant} Truncated={Truncated} " +
            "CompressionIndexed={CompressionIndexed} CompressionTopics={CompressionTopics} " +
            "CompressionSummaryChars={CompressionSummaryChars} CompressionSummaryFingerprint={CompressionSummaryFingerprint}",
            sessionId,
            historyGovernance.Strategy,
            historyGovernance.BudgetTokens,
            historyGovernance.ConsumedTokens,
            historyGovernance.FetchedMessageCount,
            historyGovernance.ReplayableMessageCount,
            historyGovernance.DirectMessageCount,
            historyGovernance.CompressedMessageCount,
            historyGovernance.SummaryMessageCount,
            historyGovernance.RecentMessageCount,
            historyGovernance.SkippedInternalRepairPromptCount,
            historyGovernance.SkippedIncompleteAssistantMessageCount,
            historyGovernance.TruncatedByBudget,
            historyGovernance.CompressionIndex.HasIndex,
            historyGovernance.CompressionIndex.TopicHints.Count,
            historyGovernance.CompressionIndex.SummaryCharacterCount,
            historyGovernance.CompressionIndex.SummaryFingerprint);

        return new ChatHistoryResult
        {
            ChatHistory = chatHistory,
            CriticalSystemPrompt = promptBuildResult.CriticalPrompt,
            MatchedSkills = promptBuildResult.MatchedSkills,
            PromptLayerMetadata = promptBuildResult.LayerMetadata
        };
    }

    /// <summary>
    /// 获取或生成文档摘要（带缓存）
    /// </summary>
    public async Task<string> GetOrGenerateSummaryAsync(string content, int targetChars, Guid sessionId, Guid providerId, CancellationToken cancellationToken)
    {
        return await _summaryService.GetOrGenerateSummaryAsync(
            content,
            targetChars,
            sessionId,
            providerId,
            cancellationToken);
    }

    /// <summary>
    /// 估算文本的 Token 数量（简单启发式）
    /// </summary>
    public static int EstimateTokenCount(string text)
    {
        return ChatHistoryTextHelper.EstimateTokenCount(text);
    }

    /// <summary>
    /// 截断过长的输出（保留头尾）
    /// </summary>
    public static string TruncateOutput(string output, int maxChars = 3000)
    {
        return ChatHistoryTextHelper.TruncateOutput(output, maxChars);
    }

    /// <summary>
    /// 补全会话上下文参数（UserId 和最新 MessageId）
    /// </summary>
    public async Task<(Guid UserId, Guid? MessageId)> EnrichSessionParamsAsync(
        Guid sessionId,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        return await _summaryService.EnrichSessionParamsAsync(
            sessionId,
            userId,
            cancellationToken);
    }
}
