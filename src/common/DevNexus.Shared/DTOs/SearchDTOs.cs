namespace DevNexus.Shared.DTOs;

/// <summary>
/// 会话搜索文档 DTO
/// </summary>
public class SessionSearchDocumentDto
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

/// <summary>
/// 消息搜索文档 DTO
/// </summary>
public class MessageSearchDocumentDto
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

/// <summary>
/// 会话搜索结果 DTO
/// </summary>
public class SessionSearchResultDto
{
    /// <summary>
    /// 会话列表
    /// </summary>
    public List<SessionSearchDocumentDto> Sessions { get; set; } = [];

    /// <summary>
    /// 总数
    /// </summary>
    public long Total { get; set; }
}

/// <summary>
/// 搜索请求 DTO
/// </summary>
public class SearchRequestDto
{
    /// <summary>
    /// 搜索关键词
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// 是否同时搜索消息内容
    /// </summary>
    public bool SearchContent { get; set; } = true;

    /// <summary>
    /// 跳过数量
    /// </summary>
    public int Skip { get; set; } = 0;

    /// <summary>
    /// 获取数量
    /// </summary>
    public int Take { get; set; } = 20;
}

/// <summary>
/// 搜索响应 DTO
/// </summary>
public class SearchResponseDto
{
    /// <summary>
    /// 会话列表
    /// </summary>
    public List<SessionSearchDocumentDto> Sessions { get; set; } = [];

    /// <summary>
    /// 总数
    /// </summary>
    public long Total { get; set; }

    /// <summary>
    /// 搜索来源（elasticsearch 或 database）
    /// </summary>
    public string Source { get; set; } = "database";
}
