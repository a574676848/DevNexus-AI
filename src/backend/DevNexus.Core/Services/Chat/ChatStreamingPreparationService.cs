using DevNexus.Shared.DTOs;
using DevNexus.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 聊天流式生成准备结果。
/// </summary>
public sealed class ChatStreamingPreparationResult
{
    /// <summary>
    /// 本次生成使用的 Provider 标识。
    /// </summary>
    public Guid ProviderId { get; init; }

    /// <summary>
    /// 当前用户输入文本。
    /// </summary>
    public string UserQuery { get; init; } = string.Empty;

    /// <summary>
    /// 已构建完成的聊天历史。
    /// </summary>
    public ChatHistory ChatHistory { get; init; } = null!;

    /// <summary>
    /// 匹配到的技能结果。
    /// </summary>
    public List<SkillMatchResult>? MatchedSkills { get; init; }

    /// <summary>
    /// Prompt 优化审计元数据。
    /// </summary>
    public PromptLayerMetadata? PromptLayerMetadata { get; init; }
}

/// <summary>
/// 聊天流式生成准备服务。
/// 负责解析 Provider、构建聊天历史，并输出生成前的技能与上下文提示。
/// </summary>
public sealed class ChatStreamingPreparationService
{
    private readonly ILLMProviderManagementService _llmProviderService;
    private readonly ChatHistoryService _chatHistoryService;
    private readonly ILogger<ChatStreamingPreparationService> _logger;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public ChatStreamingPreparationService(
        ILLMProviderManagementService llmProviderService,
        ChatHistoryService chatHistoryService,
        ILogger<ChatStreamingPreparationService> logger)
    {
        _llmProviderService = llmProviderService;
        _chatHistoryService = chatHistoryService;
        _logger = logger;
    }

    /// <summary>
    /// 为聊天流式生成准备 Provider 与上下文。
    /// </summary>
    public async Task<ChatStreamingPreparationResult> PrepareAsync(
        ChatSession chatSession,
        ChatMessage userMessage,
        Guid userId,
        ChatRequest chatRequest,
        int agentLoopAttempt,
        CancellationToken cancellationToken)
    {
        var providerId = await ResolveProviderIdAsync(chatSession, cancellationToken);
        var userQuery = userMessage.Content["text"].ToString() ?? string.Empty;

        if (agentLoopAttempt == 0)
        {
            await ThinkingContext.EmitAsync("🧭 正在整理上下文并检索相关资料...");
        }

        var historyResult = await _chatHistoryService.BuildChatHistoryAsync(
            chatSession.Id,
            userId,
            providerId,
            currentMessage: userQuery,
            selectedSkillName: chatRequest.SelectedSkillName,
            requestMetadata: chatRequest.Metadata,
            enableRag: chatRequest.EnableRag,
            activeArtifactIds: chatRequest.ArtifactIds,
            cancellationToken);

        historyResult.ChatHistory.AddUserMessage(userQuery);
        await EmitMatchedSkillThinkingAsync(chatSession.Id, historyResult.MatchedSkills);

        return new ChatStreamingPreparationResult
        {
            ProviderId = providerId,
            UserQuery = userQuery,
            ChatHistory = historyResult.ChatHistory,
            MatchedSkills = historyResult.MatchedSkills,
            PromptLayerMetadata = historyResult.PromptLayerMetadata
        };
    }

    /// <summary>
    /// 解析当前会话使用的 Provider。
    /// </summary>
    private async Task<Guid> ResolveProviderIdAsync(
        ChatSession chatSession,
        CancellationToken cancellationToken)
    {
        if (chatSession.LLMProviderId.HasValue)
        {
            return chatSession.LLMProviderId.Value;
        }

        var defaultProvider = await _llmProviderService.GetDefaultProviderAsync(cancellationToken);
        if (defaultProvider == null)
        {
            throw new InvalidOperationException(
                "No LLM provider configured. Please set a default provider in the database or select one for this session.");
        }

        _logger.LogDebug(
            "[AI.Chat] Using default provider | ProviderId={ProviderId} ProviderName={ProviderName}",
            defaultProvider.Id,
            defaultProvider.DisplayName);

        return defaultProvider.Id;
    }

    /// <summary>
    /// 输出命中的技能提示。
    /// </summary>
    private async Task EmitMatchedSkillThinkingAsync(
        Guid sessionId,
        List<SkillMatchResult>? matchedSkills)
    {
        if (matchedSkills is null || matchedSkills.Count == 0)
        {
            return;
        }

        var resolvedSkills = matchedSkills;
        var topSkill = resolvedSkills[0];
        var topSkillName = topSkill.Skill?.Name ?? "unknown";
        _logger.LogDebug(
            "[AI.Chat] Skill 匹配结果传递 | SessionId={SessionId} MatchedCount={Count} TopSkill={Top}",
            sessionId,
            resolvedSkills.Count,
            topSkillName);

        var topSkills = string.Join("、", resolvedSkills
            .Take(3)
            .Select(match => match.Skill?.Name ?? "unknown"));
        var skillThinking = $"🧩 已匹配到技能: {topSkills}，正在调用相关能力处理您的请求...";
        await ThinkingContext.EmitAsync(skillThinking);
    }
}
