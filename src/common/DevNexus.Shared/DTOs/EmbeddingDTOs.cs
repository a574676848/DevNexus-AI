using System.Text.Json.Serialization;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// Embedding 请求基类
/// </summary>
public abstract class EmbeddingRequestBase
{
    /// <summary>
    /// 模型标识
    /// </summary>
    [JsonPropertyName("model")]
    public required string Model { get; set; }
}

/// <summary>
/// Embedding 响应基类
/// </summary>
public abstract class EmbeddingResponseBase
{
    /// <summary>
    /// 对象类型
    /// </summary>
    [JsonPropertyName("object")]
    public string Object { get; set; } = "list";
    
    /// <summary>
    /// 使用的模型
    /// </summary>
    [JsonPropertyName("model")]
    public required string Model { get; set; }
}

/// <summary>
/// Embedding 数据项基类
/// </summary>
public abstract class EmbeddingDataBase
{
    /// <summary>
    /// 对象类型
    /// </summary>
    [JsonPropertyName("object")]
    public string Object { get; set; } = "embedding";
    
    /// <summary>
    /// 索引位置
    /// </summary>
    [JsonPropertyName("index")]
    public int Index { get; set; }
    
    /// <summary>
    /// Embedding 向量
    /// </summary>
    [JsonPropertyName("embedding")]
    public required float[] Embedding { get; set; }
}

/// <summary>
/// Token 使用统计基类
/// </summary>
public class TokenUsage
{
    /// <summary>
    /// Prompt tokens
    /// </summary>
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }
    
    /// <summary>
    /// 总 tokens
    /// </summary>
    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}

// ============================================================
// OpenAI Embedding DTOs
// ============================================================

/// <summary>
/// OpenAI Embedding 请求
/// API: POST https://api.openai.com/v1/embeddings
/// </summary>
public class OpenAIEmbeddingRequest : EmbeddingRequestBase
{
    /// <summary>
    /// 输入文本（单个或批量）
    /// </summary>
    [JsonPropertyName("input")]
    public required object Input { get; set; } // string or string[]
    
    /// <summary>
    /// 编码格式（可选，默认 float）
    /// </summary>
    [JsonPropertyName("encoding_format")]
    public string? EncodingFormat { get; set; }

    /// <summary>
    /// 目标向量维度，仅 text-embedding-3 系列支持
    /// </summary>
    [JsonPropertyName("dimensions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Dimensions { get; set; }
}

/// <summary>
/// OpenAI Embedding 响应
/// </summary>
public class OpenAIEmbeddingResponse : EmbeddingResponseBase
{
    /// <summary>
    /// Embedding 数据列表
    /// </summary>
    [JsonPropertyName("data")]
    public required List<OpenAIEmbeddingData> Data { get; set; }
    
    /// <summary>
    /// Token 使用统计
    /// </summary>
    [JsonPropertyName("usage")]
    public required TokenUsage Usage { get; set; }
}

/// <summary>
/// OpenAI Embedding 数据项
/// </summary>
public class OpenAIEmbeddingData : EmbeddingDataBase
{
}
