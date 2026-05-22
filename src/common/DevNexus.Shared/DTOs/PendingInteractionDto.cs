namespace DevNexus.Shared.DTOs;

/// <summary>
/// 挂起交互 DTO。
/// </summary>
public class PendingInteractionDto
{
    /// <summary>
    /// 交互标识。
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 所属会话标识。
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// 交互类型。
    /// </summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// 当前状态。
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 标题。
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 说明文案。
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 来源工具。
    /// </summary>
    public string? SourceTool { get; set; }

    /// <summary>
    /// 建议动作。
    /// </summary>
    public string? SuggestedAction { get; set; }

    /// <summary>
    /// 请求字段列表。
    /// </summary>
    public List<PendingInteractionFieldDto> RequestedFields { get; set; } = new();

    /// <summary>
    /// 过期时间。
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// 恢复令牌。
    /// </summary>
    public string? RetryToken { get; set; }

    /// <summary>
    /// 交互摘要。
    /// </summary>
    public PendingInteractionSummaryDto? Summary { get; set; }
}

/// <summary>
/// 挂起交互请求字段 DTO。
/// </summary>
public class PendingInteractionFieldDto
{
    /// <summary>
    /// 字段键。
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 字段类型。
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 字段展示名。
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// 是否必填。
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// 占位提示。
    /// </summary>
    public string? Placeholder { get; set; }
}
