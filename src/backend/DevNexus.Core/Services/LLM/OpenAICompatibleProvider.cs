using DevNexus.Core.Abstractions;
using DevNexus.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

#pragma warning disable SKEXP0010

namespace DevNexus.Core.Services.LLM;

/// <summary>
/// OpenAI Compatible LLM 提供商
/// 支持 OpenAI API 兼容的服务（如 MODELSCOPE、Ollama 等）
/// </summary>
public class OpenAICompatibleProvider : ILLMProvider
{
    private readonly LLMProviderConfig _config;
    private readonly ILogger<OpenAICompatibleProvider> _logger;
    private readonly IChatCompletionService _chatCompletionService;

    /// <summary>
    /// 提供商名称
    /// </summary>
    public string ProviderName => "OpenAICompatible";

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="config">提供商配置</param>
    /// <param name="logger">日志记录器</param>
    public OpenAICompatibleProvider(
        LLMProviderConfig config,
        ILogger<OpenAICompatibleProvider> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // 创建 OpenAI Chat Completion Service
        _chatCompletionService = new OpenAIChatCompletionService(
            modelId: _config.Model,
            apiKey: _config.ApiKey,
            endpoint: new Uri(_config.BaseUrl),
            loggerFactory: new LoggerFactory(new[] { new Microsoft.Extensions.Logging.Debug.DebugLoggerProvider() })
        );

        _logger.LogInformation(
            "[AI.LLM] OpenAI Compatible Provider initialized | BaseUrl={BaseUrl} Model={Model}",
            _config.BaseUrl,
            _config.Model);
    }

    /// <summary>
    /// 获取聊天完成服务实例
    /// </summary>
    /// <returns>聊天完成服务</returns>
    public IChatCompletionService GetChatCompletionService()
    {
        return _chatCompletionService;
    }
}
