using System.Text.Json.Serialization;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// 智能文档容器 - 统一的文档解析结果模型
/// 所有类型的文件（PDF、图片、代码、表格、文档）解析后都转化为此结构
/// </summary>
public class SmartDocument
{
    /// <summary>
    /// 文件唯一标识 (GUID)
    /// </summary>
    [JsonPropertyName("fileId")]
    public string FileId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 原始文件名
    /// </summary>
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// MIME 类型
    /// </summary>
    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = string.Empty;

    /// <summary>
    /// 文件大小 (字节)
    /// </summary>
    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; set; }

    /// <summary>
    /// 内容哈希 (SHA256)，用于缓存和去重
    /// </summary>
    [JsonPropertyName("contentHash")]
    public string? ContentHash { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 解析完成时间
    /// </summary>
    [JsonPropertyName("parsedAt")]
    public DateTime? ParsedAt { get; set; }

    /// <summary>
    /// 多态内容核心 - 根据文件类型存储不同的解析结果
    /// </summary>
    [JsonPropertyName("content")]
    public DocumentContent? Content { get; set; }

    /// <summary>
    /// 解析元信息 - 用于成本监控和调试
    /// </summary>
    [JsonPropertyName("parseInfo")]
    public ParseMetadata? ParseInfo { get; set; }

    /// <summary>
    /// 通用元数据扩展
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// 文档分块列表 (RAG Ready)
    /// </summary>
    /// <summary>
    /// 文档分块列表 (RAG Ready)
    /// </summary>
    [JsonPropertyName("chunks")]
    public List<SmartChunk> Chunks { get; set; } = new();

    /// <summary>
    /// 解析状态
    /// </summary>
    [JsonPropertyName("status")]
    public ParsingStatus Status { get; set; } = ParsingStatus.Completed;

    /// <summary>
    /// 异步跟踪 ID
    /// </summary>
    [JsonPropertyName("traceId")]
    public string? TraceId { get; set; }
}

/// <summary>
/// 智能文档分块
/// </summary>
public class SmartChunk
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// 文本内容 (Markdown 格式)
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
    
    /// <summary>
    /// 结构化数据 (JSON)，用于存储 Code AST 或 Table Data
    /// </summary>
    [JsonPropertyName("structuredData")]
    public string? StructuredData { get; set; }
    
    /// <summary>
    /// 分块类型
    /// </summary>
    [JsonPropertyName("type")]
    public ChunkType Type { get; set; } = ChunkType.Text;
    
    /// <summary>
    /// 元数据 (页码, 坐标, 语言等)
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();
    
    /// <summary>
    /// 嵌入向量 (可选)
    /// </summary>
    [JsonPropertyName("embedding")]
    public float[]? Embedding { get; set; }
}

public enum ChunkType
{
    Text,
    Code,
    Table,
    Image,
    SectionHeader
}

public enum ParsingStatus
{
    Completed,
    Processing,
    Failed,
    Pending
}

