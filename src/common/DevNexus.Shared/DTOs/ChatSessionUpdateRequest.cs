using System.Text.Json.Serialization;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// 更新聊天会话请求。
/// </summary>
public class ChatSessionUpdateRequest
{
    /// <summary>
    /// 新标题。
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }
}
