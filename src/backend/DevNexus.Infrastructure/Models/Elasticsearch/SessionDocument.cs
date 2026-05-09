namespace DevNexus.Infrastructure.Models.Elasticsearch;

/// <summary>
/// Elasticsearch 会话文档模型（ES 索引专用）
/// </summary>
public class SessionDocument
{
    /// <summary>
    /// 会话 ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 用户 ID
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// 会话标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 会话创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 会话更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 最后一条消息预览
    /// </summary>
    public string? LastMessagePreview { get; set; }

    /// <summary>
    /// 消息数量
    /// </summary>
    public int MessageCount { get; set; }
}
