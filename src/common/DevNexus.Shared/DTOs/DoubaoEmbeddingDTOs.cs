using System.Text.Json.Serialization;

namespace DevNexus.Shared.DTOs;

// ============================================================
// 豆包 (Doubao) Multimodal Embedding DTOs
// API: POST https://ark.cn-beijing.volces.com/api/v3/embeddings/multimodal
// ============================================================

/// <summary>
/// 豆包多模态 Embedding 输入项基类
/// </summary>
[JsonDerivedType(typeof(DoubaoTextInput))]
[JsonDerivedType(typeof(DoubaoImageInput))]
public abstract class DoubaoInputItem
{
    /// <summary>
    /// 输入类型: "text" | "image_url"
    /// </summary>
    [JsonPropertyName("type")]
    public abstract string Type { get; }
}

/// <summary>
/// 文本输入项
/// </summary>
public class DoubaoTextInput : DoubaoInputItem
{
    [JsonPropertyName("type")]
    public override string Type => "text";

    /// <summary>
    /// 文本内容
    /// </summary>
    [JsonPropertyName("text")]
    public required string Text { get; set; }
}

/// <summary>
/// 图片输入项
/// </summary>
public class DoubaoImageInput : DoubaoInputItem
{
    [JsonPropertyName("type")]
    public override string Type => "image_url";

    /// <summary>
    /// 图片 URL 对象
    /// </summary>
    [JsonPropertyName("image_url")]
    public required DoubaoImageUrl ImageUrl { get; set; }
}

/// <summary>
/// 豆包图片 URL 对象
/// </summary>
public class DoubaoImageUrl
{
    /// <summary>
    /// 图片 URL 地址
    /// </summary>
    [JsonPropertyName("url")]
    public required string Url { get; set; }
}

/// <summary>
/// 豆包多模态 Embedding 请求
/// </summary>
public class DoubaoMultimodalEmbeddingRequest : EmbeddingRequestBase
{
    /// <summary>
    /// 多模态输入项列表
    /// </summary>
    [JsonPropertyName("input")]
    public required List<DoubaoInputItem> Input { get; set; }
}

/// <summary>
/// 豆包纯文本 Embedding 请求（兼容 OpenAI 格式）
/// </summary>
public class DoubaoTextEmbeddingRequest : EmbeddingRequestBase
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
}

/// <summary>
/// 豆包多模态 Embedding 响应
/// 用于多模态向量化 API (/embeddings/multimodal)，data 是单个对象
/// </summary>
public class DoubaoMultimodalEmbeddingResponse : EmbeddingResponseBase
{
    /// <summary>
    /// 本次请求的唯一标识
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 本次请求创建时间的 Unix 时间戳（秒）
    /// </summary>
    [JsonPropertyName("created")]
    public long Created { get; set; }

    /// <summary>
    /// Embedding 数据项（单个对象，不是数组）
    /// </summary>
    [JsonPropertyName("data")]
    public required DoubaoEmbeddingData Data { get; set; }

    /// <summary>
    /// Token 使用统计
    /// </summary>
    [JsonPropertyName("usage")]
    public required TokenUsage Usage { get; set; }
}

/// <summary>
/// 豆包 Embedding 数据项
/// </summary>
public class DoubaoEmbeddingData : EmbeddingDataBase
{
}
