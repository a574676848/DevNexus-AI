using DevNexus.Shared.Constants;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;

namespace DevNexus.Core.Services.Chat;

public sealed class ChatHistorySummaryService
{
    private readonly IKernelService _kernelService;
    private readonly IDistributedCache _distributedCache;
    private readonly IChatSessionRepository _chatSessionRepository;
    private readonly IChatMessageRepository _chatMessageRepository;
    private readonly ILogger<ChatHistorySummaryService> _logger;

    public ChatHistorySummaryService(
        IKernelService kernelService,
        IDistributedCache distributedCache,
        IChatSessionRepository chatSessionRepository,
        IChatMessageRepository chatMessageRepository,
        ILogger<ChatHistorySummaryService> logger)
    {
        _kernelService = kernelService;
        _distributedCache = distributedCache;
        _chatSessionRepository = chatSessionRepository;
        _chatMessageRepository = chatMessageRepository;
        _logger = logger;
    }

    public async Task<string> GetOrGenerateSummaryAsync(
        string content,
        int targetChars,
        Guid sessionId,
        Guid providerId,
        CancellationToken cancellationToken)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        var hashBytes = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content));
        var hashParams = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(targetChars.ToString()));
        var hashStr = BitConverter.ToString(hashBytes).Replace("-", string.Empty) + "_" + BitConverter.ToString(hashParams).Replace("-", string.Empty);

        var cacheKey = $"artifact_summary:{hashStr}";

        var cachedSummary = await _distributedCache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrEmpty(cachedSummary))
        {
            _logger.LogDebug("[AI.Chat] AI 聊天： Cache hit for artifact summary: {Key}", cacheKey);
            return cachedSummary;
        }

        _logger.LogDebug(
            "[AI.Chat] AI 聊天： Generating summary using LLM. ContentLen={Len}, Target={Target}",
            content.Length,
            targetChars);

        var prompt = string.Format(PromptConstants.Chat.GenerateSummary, targetChars, content);
        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage(prompt);

        var (finalUserId, latestMessageId) = await EnrichSessionParamsAsync(sessionId, null, cancellationToken);

        var result = await _kernelService.GetChatCompletionAsync(
            chatHistory,
            providerId,
            sessionId: sessionId,
            messageId: latestMessageId,
            userId: finalUserId,
            cancellationToken: cancellationToken);
        var summary = result.Content ?? string.Empty;

        if (string.IsNullOrWhiteSpace(summary))
        {
            _logger.LogWarning("[AI.Chat] AI 聊天： Generating summary using LLM failed, Generated summary is empty");
            return string.Empty;
        }

        await _distributedCache.SetStringAsync(
            cacheKey,
            summary,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
            },
            cancellationToken);

        return summary;
    }

    public async Task<(Guid UserId, Guid? MessageId)> EnrichSessionParamsAsync(
        Guid sessionId,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var finalUserId = userId ?? Guid.Empty;
        if (finalUserId == Guid.Empty)
        {
            finalUserId = await _chatSessionRepository.GetUserIdAsync(sessionId, cancellationToken) ?? Guid.Empty;
        }

        var latestMessageId = await _chatMessageRepository.GetLatestMessageIdBySessionAsync(
            sessionId,
            cancellationToken);

        return (finalUserId, latestMessageId);
    }
}