using DevNexus.ApiService.Models;

namespace DevNexus.ApiService.Services;

/// <summary>
/// Registry of supported AI providers with their metadata
/// </summary>
public static class AIProviderRegistry
{
    private static readonly List<AIProviderInfo> _providers = new()
    {
        // OpenAI Compatible (Default)
        new AIProviderInfo
        {
            Id = "openai-compatible",
            Name = "OpenAI Compatible",
            Description = "Generic OpenAI-compatible API endpoint",
            RequiresApiKey = true,
            ApiKeyUrl = "https://platform.openai.com/api-keys",
            Icon = "/images/providers/openai-compatible.svg"
        },

        // Ollama (Local)
        new AIProviderInfo
        {
            Id = "ollama",
            Name = "Ollama",
            Description = "Run large language models locally",
            RequiresApiKey = false,
            DefaultEndpoint = "http://localhost:11434",
            ApiKeyUrl = "https://ollama.com/download",
            Icon = "/images/providers/ollama.svg"
        },

        // OpenAI
        new AIProviderInfo
        {
            Id = "openai",
            Name = "OpenAI",
            Description = "Official OpenAI API (GPT-4, GPT-3.5)",
            RequiresApiKey = true,
            DefaultEndpoint = "https://api.openai.com/v1",
            ApiKeyUrl = "https://platform.openai.com/api-keys",
            Icon = "/images/providers/openai.svg"
        },

        // DeepSeek
        new AIProviderInfo
        {
            Id = "deepseek",
            Name = "DeepSeek",
            Description = "DeepSeek AI models",
            RequiresApiKey = true,
            DefaultEndpoint = "https://api.deepseek.com",
            ApiKeyUrl = "https://platform.deepseek.com/api_keys",
            Icon = "/images/providers/deepseek.svg"
        },

        // Doubao (ByteDance)
        new AIProviderInfo
        {
            Id = "doubao",
            Name = "Doubao",
            Description = "ByteDance's AI assistant",
            RequiresApiKey = true,
            DefaultEndpoint = "https://ark.cn-beijing.volces.com/api/v3",
            ApiKeyUrl = "https://console.volcengine.com/ark/region:ark+cn-beijing/apiKey",
            Icon = "/images/providers/doubao.svg"
        },

        // Claude (Anthropic)
        new AIProviderInfo
        {
            Id = "claude",
            Name = "Claude",
            Description = "Anthropic's Claude AI models",
            RequiresApiKey = true,
            DefaultEndpoint = "https://api.anthropic.com/v1",
            ApiKeyUrl = "https://console.anthropic.com/settings/keys",
            Icon = "/images/providers/claude.svg"
        },

        // Google Gemini
        new AIProviderInfo
        {
            Id = "gemini",
            Name = "Google Gemini",
            Description = "Google's Gemini AI models",
            RequiresApiKey = true,
            DefaultEndpoint = "https://generativelanguage.googleapis.com/v1beta",
            ApiKeyUrl = "https://aistudio.google.com/app/apikey",
            Icon = "/images/providers/gemini.svg"
        },

        // MiniMax
        new AIProviderInfo
        {
            Id = "minimax",
            Name = "MiniMax",
            Description = "MiniMax AI platform",
            RequiresApiKey = true,
            DefaultEndpoint = "https://api.minimax.chat/v1",
            ApiKeyUrl = "https://www.minimaxi.com/user-center/basic-information/interface-key",
            Icon = "/images/providers/minimax.svg"
        },

        // Kimi (Moonshot AI)
        new AIProviderInfo
        {
            Id = "kimi",
            Name = "Kimi",
            Description = "Moonshot AI's Kimi models",
            RequiresApiKey = true,
            DefaultEndpoint = "https://api.moonshot.cn/v1",
            ApiKeyUrl = "https://platform.moonshot.cn/console/api-keys",
            Icon = "/images/providers/kimi.svg"
        },

        // Qwen (Alibaba)
        new AIProviderInfo
        {
            Id = "qwen",
            Name = "Qwen",
            Description = "Alibaba's Qwen (Tongyi Qianwen) models",
            RequiresApiKey = true,
            DefaultEndpoint = "https://dashscope.aliyuncs.com/api/v1",
            ApiKeyUrl = "https://dashscope.console.aliyun.com/apiKey",
            Icon = "/images/providers/qwen.svg"
        }
    };

    /// <summary>
    /// Get all available AI providers
    /// </summary>
    public static IReadOnlyList<AIProviderInfo> GetProviders() => _providers.AsReadOnly();

    /// <summary>
    /// Get a specific provider by ID
    /// </summary>
    public static AIProviderInfo? GetProvider(string id)
        => _providers.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Get the default provider (OpenAI Compatible)
    /// </summary>
    public static AIProviderInfo GetDefaultProvider()
        => _providers.First(p => p.Id == "openai-compatible");
}
