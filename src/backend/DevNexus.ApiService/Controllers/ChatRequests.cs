using System.ComponentModel.DataAnnotations;

namespace DevNexus.ApiService.Controllers;

/// <summary>
/// 创建聊天会话请求。
/// </summary>
public class CreateChatSessionRequest
{
    /// <summary>
    /// 会话标题。
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Title { get; set; } = string.Empty;
}

/// <summary>
/// 批量删除消息请求。
/// </summary>
public class BatchDeleteMessagesRequest
{
    /// <summary>
    /// 要删除的消息 ID 列表。
    /// </summary>
    [Required]
    public List<Guid> MessageIds { get; set; } = new();
}
