namespace DevNexus.Infrastructure.Models.Elasticsearch;

/// <summary>
/// Elasticsearch 消息文档模型（ES 索引专用）
/// </summary>
public class MessageDocument
{
    /// <summary>
    /// 消息 ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 会话 ID
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// 用户 ID
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// 消息角色 (user, assistant, system)
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// 消息内容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 消息创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
