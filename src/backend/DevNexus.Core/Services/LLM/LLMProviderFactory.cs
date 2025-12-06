using DevNexus.Core.Abstractions;
using DevNexus.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevNexus.Core.Services.LLM;

/// <summary>
/// LLM 提供商工厂
/// 根据配置创建和管理不同的 LLM 提供商
/// </summary>
public class LLMProviderFactory
{
    private readonly LLMOptions _options;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Dictionary<string, ILLMProvider> _providers = new();

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">LLM 配置选项</param>
    /// <param name="loggerFactory">日志工厂</param>
    public LLMProviderFactory(
        IOptions<LLMOptions> options,
        ILoggerFactory loggerFactory)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    /// <summary>
    /// 获取默认提供商
    /// </summary>
    /// <returns>LLM 提供商实例</returns>
    public ILLMProvider GetDefaultProvider()
    {
        return GetProvider(_options.DefaultProvider);
    }

    /// <summary>
    /// 根据名称获取提供商
    /// </summary>
    /// <param name="providerName">提供商名称</param>
    /// <returns>LLM 提供商实例</returns>
    public ILLMProvider GetProvider(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new ArgumentException("Provider name cannot be null or empty", nameof(providerName));
        }

        // 如果已经创建过，直接返回
        if (_providers.TryGetValue(providerName, out var existingProvider))
        {
            return existingProvider;
        }

        // 获取配置
        if (!_options.Providers.TryGetValue(providerName, out var config))
        {
            throw new InvalidOperationException($"Provider configuration not found: {providerName}");
        }

        // 创建提供商实例
        ILLMProvider provider = providerName switch
        {
            "OpenAICompatible" => new OpenAICompatibleProvider(
                config,
                _loggerFactory.CreateLogger<OpenAICompatibleProvider>()),
            
            // TODO: 添加其他提供商支持
            // "Gemini" => new GeminiProvider(config, _loggerFactory.CreateLogger<GeminiProvider>()),
            // "Kimi" => new KimiProvider(config, _loggerFactory.CreateLogger<KimiProvider>()),
            // "MiniMaxM2" => new MiniMaxProvider(config, _loggerFactory.CreateLogger<MiniMaxProvider>()),
            
            _ => throw new NotSupportedException($"Provider not supported: {providerName}")
        };

        // 缓存提供商实例
        _providers[providerName] = provider;

        return provider;
    }

    /// <summary>
    /// 获取所有可用的提供商名称
    /// </summary>
    /// <returns>提供商名称列表</returns>
    public IReadOnlyList<string> GetAvailableProviders()
    {
        return _options.Providers.Keys.ToList();
    }
    
    /// <summary>
    /// 获取默认提供商配置
    /// </summary>
    /// <returns>提供商配置</returns>
    public LLMProviderConfig? GetDefaultProviderConfig()
    {
        if (_options.Providers.TryGetValue(_options.DefaultProvider, out var config))
        {
            return config;
        }
        return null;
    }
    
    /// <summary>
    /// 获取指定提供商配置
    /// </summary>
    /// <param name="providerName">提供商名称</param>
    /// <returns>提供商配置</returns>
    public LLMProviderConfig? GetProviderConfig(string providerName)
    {
        if (_options.Providers.TryGetValue(providerName, out var config))
        {
            return config;
        }
        return null;
    }
}
