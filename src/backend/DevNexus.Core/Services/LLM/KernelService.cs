using DevNexus.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DevNexus.Core.Services.LLM;

/// <summary>
/// Semantic Kernel 服务封装
/// 提供统一的 AI 聊天完成接口
/// </summary>
public class KernelService
{
    private readonly LLMProviderFactory _providerFactory;
    private readonly TokenAuditService _tokenAuditService;
    private readonly ILogger<KernelService> _logger;
    private Kernel? _kernel;
    private ILLMProvider? _currentProvider;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="providerFactory">LLM 提供商工厂</param>
    /// <param name="tokenAuditService">Token 审计服务</param>
    /// <param name="logger">日志记录器</param>
    public KernelService(
        LLMProviderFactory providerFactory,
        TokenAuditService tokenAuditService,
        ILogger<KernelService> logger)
    {
        _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
        _tokenAuditService = tokenAuditService ?? throw new ArgumentNullException(nameof(tokenAuditService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 获取或创建 Kernel 实例（使用默认提供商）
    /// </summary>
    /// <returns>Kernel 实例</returns>
    public Kernel GetKernel()
    {
        if (_kernel == null)
        {
            _currentProvider = _providerFactory.GetDefaultProvider();
            var chatCompletionService = _currentProvider.GetChatCompletionService();

            var builder = Kernel.CreateBuilder();
            builder.Services.AddSingleton(chatCompletionService);
            
            _kernel = builder.Build();

            _logger.LogInformation(
                "[AI.Kernel] Kernel initialized | Provider={Provider}",
                _currentProvider.ProviderName);
        }

        return _kernel;
    }

    /// <summary>
    /// 获取或创建 Kernel 实例（使用指定提供商）
    /// </summary>
    /// <param name="providerName">提供商名称</param>
    /// <returns>Kernel 实例</returns>
    public Kernel GetKernel(string providerName)
    {
        // 如果当前已有 Kernel 且使用的是相同提供商，直接返回
        if (_kernel != null && _currentProvider?.ProviderName == providerName)
        {
            return _kernel;
        }

        // 创建新的 Kernel
        _currentProvider = _providerFactory.GetProvider(providerName);
        var chatCompletionService = _currentProvider.GetChatCompletionService();

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(chatCompletionService);
        
        _kernel = builder.Build();

        _logger.LogInformation(
            "[AI.Kernel] Kernel initialized | Provider={Provider}",
            _currentProvider.ProviderName);

        return _kernel;
    }

    /// <summary>
    /// 流式聊天完成
    /// </summary>
    /// <param name="chatHistory">聊天历史</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>流式响应</returns>
    public async IAsyncEnumerable<StreamingChatMessageContent> StreamChatCompletionAsync(
        ChatHistory chatHistory,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var kernel = GetKernel();
        var chatCompletion = kernel.GetRequiredService<IChatCompletionService>();
        var stopwatch = Stopwatch.StartNew();
        var outputTokenCount = 0;

        _logger.LogInformation(
            "[AI.Kernel] Starting streaming chat completion | MessageCount={Count} Provider={Provider}",
            chatHistory.Count,
            GetCurrentProviderName());

        await foreach (var content in chatCompletion.GetStreamingChatMessageContentsAsync(
            chatHistory,
            cancellationToken: cancellationToken))
        {
            // 估算输出 Token 数（每个内容块约等于若干 Token）
            if (!string.IsNullOrEmpty(content.Content))
            {
                outputTokenCount += EstimateTokenCount(content.Content);
            }
            
            yield return content;
        }

        stopwatch.Stop();
        
        // 估算输入 Token 数
        var inputTokenCount = EstimateChatHistoryTokens(chatHistory);
        
        // 记录 Token 使用量
        _logger.LogInformation(
            "[AI.TokenAudit] Streaming completion finished | " +
            "Model={Model} InputTokens={InputTokens} OutputTokens={OutputTokens} " +
            "TotalTokens={TotalTokens} Duration={Duration}ms",
            GetCurrentModelId(),
            inputTokenCount,
            outputTokenCount,
            inputTokenCount + outputTokenCount,
            stopwatch.ElapsedMilliseconds);
    }
    
    /// <summary>
    /// 流式聊天完成（带审计上下文）
    /// </summary>
    /// <param name="chatHistory">聊天历史</param>
    /// <param name="sessionId">会话ID</param>
    /// <param name="messageId">消息ID</param>
    /// <param name="userId">用户ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>流式响应</returns>
    public async IAsyncEnumerable<StreamingChatMessageContent> StreamChatCompletionWithAuditAsync(
        ChatHistory chatHistory,
        Guid sessionId,
        Guid messageId,
        Guid userId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var kernel = GetKernel();
        var chatCompletion = kernel.GetRequiredService<IChatCompletionService>();
        var stopwatch = Stopwatch.StartNew();
        var outputTokenCount = 0;

        _logger.LogInformation(
            "[AI.Kernel] Starting streaming chat completion | SessionId={SessionId} MessageId={MessageId} Provider={Provider}",
            sessionId,
            messageId,
            GetCurrentProviderName());

        await foreach (var content in chatCompletion.GetStreamingChatMessageContentsAsync(
            chatHistory,
            cancellationToken: cancellationToken))
        {
            if (!string.IsNullOrEmpty(content.Content))
            {
                outputTokenCount += EstimateTokenCount(content.Content);
            }
            
            yield return content;
        }

        stopwatch.Stop();
        
        var inputTokenCount = EstimateChatHistoryTokens(chatHistory);
        
        // 使用审计服务记录
        _tokenAuditService.RecordStreamingCompletion(
            sessionId,
            messageId,
            userId,
            GetCurrentModelId(),
            inputTokenCount,
            outputTokenCount,
            stopwatch.ElapsedMilliseconds);
    }
    
    /// <summary>
    /// 估算文本的 Token 数量
    /// 使用简单规则：英文约 4 字符 = 1 Token，中文约 1.5 字符 = 1 Token
    /// </summary>
    private static int EstimateTokenCount(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        
        var chineseCount = text.Count(c => c >= 0x4E00 && c <= 0x9FFF);
        var otherCount = text.Length - chineseCount;
        
        return (int)Math.Ceiling(chineseCount / 1.5) + (int)Math.Ceiling(otherCount / 4.0);
    }
    
    /// <summary>
    /// 估算聊天历史的 Token 数量
    /// </summary>
    private static int EstimateChatHistoryTokens(ChatHistory chatHistory)
    {
        return chatHistory.Sum(m => EstimateTokenCount(m.Content ?? string.Empty));
    }
    
    /// <summary>
    /// 获取当前模型ID
    /// </summary>
    private string GetCurrentModelId()
    {
        return _providerFactory.GetDefaultProviderConfig()?.Model ?? "unknown";
    }

    /// <summary>
    /// 非流式聊天完成
    /// </summary>
    /// <param name="chatHistory">聊天历史</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>完整响应</returns>
    public async Task<ChatMessageContent> GetChatCompletionAsync(
        ChatHistory chatHistory,
        CancellationToken cancellationToken = default)
    {
        var kernel = GetKernel();
        var chatCompletion = kernel.GetRequiredService<IChatCompletionService>();

        _logger.LogInformation(
            "[AI.Kernel] Starting chat completion | MessageCount={Count}",
            chatHistory.Count);

        var result = await chatCompletion.GetChatMessageContentAsync(
            chatHistory,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "[AI.Kernel] Chat completion finished | ResponseLength={Length}",
            result.Content?.Length ?? 0);

        return result;
    }

    /// <summary>
    /// 获取当前使用的提供商名称
    /// </summary>
    /// <returns>提供商名称</returns>
    public string GetCurrentProviderName()
    {
        return _currentProvider?.ProviderName ?? "None";
    }
}
