namespace DevNexus.Domain.Configuration;

/// <summary>
/// LLM 配置选项
/// </summary>
public class LLMOptions
{
    /// <summary>
    /// 默认提供商
    /// </summary>
    public string DefaultProvider { get; set; } = "OpenAICompatible";
    
    /// <summary>
    /// 提供商配置字典
    /// </summary>
    public Dictionary<string, LLMProviderConfig> Providers { get; set; } = new();
}

/// <summary>
/// LLM 提供商配置
/// </summary>
public class LLMProviderConfig
{
    /// <summary>
    /// 服务地址
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// 模型名称
    /// </summary>
    public string Model { get; set; } = string.Empty;
    
    /// <summary>
    /// API 密钥
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
    
    /// <summary>
    /// 区域（可选，某些服务需要）
    /// </summary>
    public string? Region { get; set; }
    
    /// <summary>
    /// 组 ID（可选，MiniMax 需要）
    /// </summary>
    public string? GroupId { get; set; }
    
    /// <summary>
    /// 最大 Token 数
    /// </summary>
    public int MaxTokens { get; set; } = 4096;
    
    /// <summary>
    /// 温度参数
    /// </summary>
    public double Temperature { get; set; } = 0.7;
    
    /// <summary>
    /// Top P 参数
    /// </summary>
    public double TopP { get; set; } = 0.9;
}
