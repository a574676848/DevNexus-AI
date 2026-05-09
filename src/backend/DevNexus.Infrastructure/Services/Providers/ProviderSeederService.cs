using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
// using DevNexus.Domain.Abstractions via GlobalUsings
using DevNexus.Domain.Configuration;
// using DevNexus.Domain.Entities via GlobalUsings
using DevNexus.Infrastructure.Models;
using DevNexus.Shared.Enums;

namespace DevNexus.Infrastructure.Services.Providers;

/// <summary>
/// 供应商种子数据服务 - 从配置迁移到数据库
/// </summary>
public class ProviderSeederService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<ProviderSeederService> _logger;
    private readonly int _globalVectorSize;
    
    public ProviderSeederService(
        ApplicationDbContext context,
        IConfiguration configuration,
        IEncryptionService encryptionService,
        IOptions<QdrantOptions> qdrantOptions,
        ILogger<ProviderSeederService> logger)
    {
        _context = context;
        _configuration = configuration;
        _encryptionService = encryptionService;
        _globalVectorSize = (int)(qdrantOptions?.Value?.VectorSize ?? 1024);
        _logger = logger;
    }
    
    public async Task SeedFromConfigurationAsync()
    {
        // 检查是否已有数据
        if (await _context.LLMProviders.AnyAsync())
        {
            _logger.LogDebug("Providers already seeded, skipping");
            return;
        }
        
        _logger.LogDebug("Seeding providers from configuration");
        
        // 从配置读取并创建供应商
        await SeedLLMProvidersAsync();
        await SeedEmbeddingProvidersAsync();
        
        await _context.SaveChangesAsync();
        
        _logger.LogDebug("Provider seeding completed");
    }
    
    private async Task SeedLLMProvidersAsync()
    {
        var llmSection = _configuration.GetSection("LLM:Providers");
        var priority = 1;
        
        // OpenAI Compatible
        var openAIConfig = llmSection.GetSection("OpenAICompatible");
        if (openAIConfig.Exists() && !string.IsNullOrWhiteSpace(openAIConfig["ApiKey"]))
        {
            _context.LLMProviders.Add(new LLMProvider
            {
                ProviderId = "openai-compatible",
                DisplayName = "OpenAI Compatible",
                Type = ProviderType.OpenAICompatible,
                LogoUrl = "https://cdn.simpleicons.org/openai",
                Endpoint = openAIConfig["Endpoint"] ?? "https://api.openai.com/v1",
                ApiKey = _encryptionService.Encrypt(openAIConfig["ApiKey"] ?? ""),
                ModelName = openAIConfig["ModelName"] ?? "gpt-4",
                IsEnabled = true,
                IsDefault = priority == 1,
                Priority = priority++
            });
            
            _logger.LogDebug("Seeded {ProviderName} provider", "OpenAI Compatible");
        }
        
        // Gemini
        var geminiConfig = llmSection.GetSection("Gemini");
        if (geminiConfig.Exists() && !string.IsNullOrWhiteSpace(geminiConfig["ApiKey"]))
        {
            _context.LLMProviders.Add(new LLMProvider
            {
                ProviderId = "gemini",
                DisplayName = "Google Gemini",
                Type = ProviderType.Gemini,
                LogoUrl = "https://www.gstatic.com/lamda/images/gemini_sparkle_v002_d4735304ff6292a690345.svg",
                Endpoint = geminiConfig["Endpoint"] ?? "https://generativelanguage.googleapis.com",
                ApiKey = _encryptionService.Encrypt(geminiConfig["ApiKey"] ?? ""),
                ModelName = geminiConfig["ModelName"] ?? "gemini-pro",
                IsEnabled = true,
                IsDefault = priority == 1,
                Priority = priority++
            });
            
            _logger.LogDebug("Seeded Gemini provider");
        }
        
        // Kimi
        var kimiConfig = llmSection.GetSection("Kimi");
        if (kimiConfig.Exists() && !string.IsNullOrWhiteSpace(kimiConfig["ApiKey"]))
        {
            _context.LLMProviders.Add(new LLMProvider
            {
                ProviderId = "kimi",
                DisplayName = "Kimi (月之暗面)",
                Type = ProviderType.Kimi,
                LogoUrl = "https://statics.moonshot.cn/kimi-chat/favicon.ico",
                Endpoint = kimiConfig["Endpoint"] ?? "https://api.moonshot.cn/v1",
                ApiKey = _encryptionService.Encrypt(kimiConfig["ApiKey"] ?? ""),
                ModelName = kimiConfig["ModelName"] ?? "moonshot-v1-8k",
                IsEnabled = true,
                IsDefault = priority == 1,
                Priority = priority++
            });
            
            _logger.LogDebug("Seeded Kimi provider");
        }
        
        // MiniMax
        var miniMaxConfig = llmSection.GetSection("MiniMax");
        if (miniMaxConfig.Exists() && !string.IsNullOrWhiteSpace(miniMaxConfig["ApiKey"]))
        {
            _context.LLMProviders.Add(new LLMProvider
            {
                ProviderId = "minimax",
                DisplayName = "MiniMax",
                Type = ProviderType.MiniMax,
                LogoUrl = "https://platform.minimaxi.com/favicon.ico",
                Endpoint = miniMaxConfig["Endpoint"] ?? "https://api.minimaxi.com/v1",
                ApiKey = _encryptionService.Encrypt(miniMaxConfig["ApiKey"] ?? ""),
                ModelName = miniMaxConfig["ModelName"] ?? "MiniMax-M2",
                IsEnabled = true,
                IsDefault = priority == 1,
                Priority = priority++
            });
            
            _logger.LogDebug("Seeded MiniMax provider");
        }
        
        // DeepSeek
        var deepSeekConfig = llmSection.GetSection("DeepSeek");
        if (deepSeekConfig.Exists() && !string.IsNullOrWhiteSpace(deepSeekConfig["ApiKey"]))
        {
            _context.LLMProviders.Add(new LLMProvider
            {
                ProviderId = "deepseek",
                DisplayName = "DeepSeek",
                Type = ProviderType.DeepSeek,
                LogoUrl = "https://chat.deepseek.com/favicon.ico",
                Endpoint = deepSeekConfig["Endpoint"] ?? "https://api.deepseek.com/v1",
                ApiKey = _encryptionService.Encrypt(deepSeekConfig["ApiKey"] ?? ""),
                ModelName = deepSeekConfig["ModelName"] ?? "deepseek-chat",
                IsEnabled = true,
                IsDefault = priority == 1,
                Priority = priority++
            });
            
            _logger.LogDebug("Seeded DeepSeek provider");
        }
        
        // GLM (智谱)
        var glmConfig = llmSection.GetSection("GLM");
        if (glmConfig.Exists() && !string.IsNullOrWhiteSpace(glmConfig["ApiKey"]))
        {
            _context.LLMProviders.Add(new LLMProvider
            {
                ProviderId = "glm",
                DisplayName = "GLM（智谱）",
                Type = ProviderType.GLM,
                LogoUrl = "https://open.bigmodel.cn/favicon.ico",
                Endpoint = glmConfig["Endpoint"] ?? "https://open.bigmodel.cn/api/paas/v4",
                ApiKey = _encryptionService.Encrypt(glmConfig["ApiKey"] ?? ""),
                ModelName = glmConfig["ModelName"] ?? "glm-4",
                IsEnabled = true,
                IsDefault = priority == 1,
                Priority = priority++
            });
            
            _logger.LogDebug("Seeded GLM provider");
        }
        
    }
    
    private async Task SeedEmbeddingProvidersAsync()
    {
        var embeddingSection = _configuration.GetSection("Embedding:Providers");
        var priority = 1;
        
        // Doubao
        var doubaoConfig = embeddingSection.GetSection("Doubao");
        if (doubaoConfig.Exists() && !string.IsNullOrWhiteSpace(doubaoConfig["ApiKey"]))
        {
            _context.EmbeddingProviders.Add(new EmbeddingProvider
            {
                ProviderId = "doubao",
                DisplayName = "豆包 Embedding",
                Type = EmbeddingProviderType.Doubao,
                LogoUrl = "https://cdn.simpleicons.org/bytedance",
                Endpoint = doubaoConfig["Endpoint"] ?? "https://ark.cn-beijing.volces.com/api/v3",
                ApiKey = _encryptionService.Encrypt(doubaoConfig["ApiKey"] ?? ""),
                ModelName = doubaoConfig["ModelName"] ?? "doubao-embedding",
                VectorSize = _globalVectorSize,
                IsEnabled = true,
                IsDefault = priority == 1,
                Priority = priority++
            });
            
            _logger.LogDebug("Seeded Doubao Embedding provider");
        }
        
        // OpenAI
        var openAIConfig = embeddingSection.GetSection("OpenAI");
        if (openAIConfig.Exists() && !string.IsNullOrWhiteSpace(openAIConfig["ApiKey"]))
        {
            _context.EmbeddingProviders.Add(new EmbeddingProvider
            {
                ProviderId = "openai",
                DisplayName = "OpenAI Embedding",
                Type = EmbeddingProviderType.OpenAI,
                LogoUrl = "https://cdn.simpleicons.org/openai",
                Endpoint = openAIConfig["Endpoint"] ?? "https://api.openai.com/v1",
                ApiKey = _encryptionService.Encrypt(openAIConfig["ApiKey"] ?? ""),
                ModelName = openAIConfig["ModelName"] ?? "text-embedding-3-small",
                VectorSize = _globalVectorSize,
                IsEnabled = true,
                IsDefault = priority == 1,
                Priority = priority++
            });
            
            _logger.LogDebug("Seeded OpenAI Embedding provider");
        }
        
        // Local
        var localConfig = embeddingSection.GetSection("Local");
        if (localConfig.Exists() && !string.IsNullOrWhiteSpace(localConfig["Endpoint"]))
        {
            _context.EmbeddingProviders.Add(new EmbeddingProvider
            {
                ProviderId = "local",
                DisplayName = "Local Embedding Model",
                Type = EmbeddingProviderType.Local,
                LogoUrl = "/assets/logos/local-model.svg",
                Endpoint = localConfig["Endpoint"] ?? "http://localhost:11434",
                ApiKey = _encryptionService.Encrypt(localConfig["ApiKey"] ?? ""),
                ModelName = localConfig["ModelName"] ?? "nomic-embed-text",
                VectorSize = _globalVectorSize,
                IsEnabled = true,
                IsDefault = priority == 1,
                Priority = priority++
            });
            
            _logger.LogDebug("Seeded Local Embedding provider");
        }
    }
    
}
