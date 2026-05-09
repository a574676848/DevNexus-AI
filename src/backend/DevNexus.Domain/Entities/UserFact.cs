using System.ComponentModel.DataAnnotations;
using DevNexus.Domain.Entities.Base;

namespace DevNexus.Domain.Entities;

/// <summary>
/// 用户画像/事实 (长期语义记忆)
/// 存储结构化的用户偏好，每次对话必读
/// </summary>
public class UserFact : AuditableEntity
{
    /// <summary>
    /// 所属用户ID
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// 分类标签
    /// 例如: "CodingStyle", "TechStack", "Personal", "Workflow"
    /// </summary>
    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// 事实内容
    /// 例如: "偏好使用 Dapper 而非 EF Core"
    /// </summary>
    [MaxLength(500)]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 事实内容的向量哈希（用于快速去重和相似度判断）
    /// </summary>
    [MaxLength(64)]
    public string? ContentHash { get; set; }

    /// <summary>
    /// 来源会话ID (可追溯是从哪次聊天学到的)
    /// </summary>
    public Guid? SourceSessionId { get; set; }

    /// <summary>
    /// 置信度权重 (1-10)
    /// 随着用户确认次数增加而增加
    /// </summary>
    public int ConfidenceScore { get; set; } = 1;

    /// <summary>
    /// 是否被用户固定 (Pinned)
    /// 固定后 AI 必须遵守，不可被新记忆覆盖
    /// </summary>
    public bool IsPinned { get; set; } = false;

}
