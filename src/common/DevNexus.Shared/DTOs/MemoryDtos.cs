namespace DevNexus.Shared.DTOs;

/// <summary>
/// 用户画像/事实 DTO
/// </summary>
public class UserFactDto
{
    /// <summary>
    /// 事实ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 分类标签
    /// 例如: "CodingStyle", "TechStack", "Personal", "Workflow"
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// 事实内容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 来源会话ID
    /// </summary>
    public Guid? SourceSessionId { get; set; }

    /// <summary>
    /// 置信度权重 (1-10)
    /// </summary>
    public int ConfidenceScore { get; set; }

    /// <summary>
    /// 是否被用户固定
    /// </summary>
    public bool IsPinned { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 添加用户画像请求
/// </summary>
public class AddUserFactRequest
{
    /// <summary>
    /// 分类标签
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// 事实内容
    /// </summary>
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// 情境记忆 DTO
/// </summary>
public class EpisodicMemoryDto
{
    /// <summary>
    /// 记忆ID (Qdrant Point ID)
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 关联的会话ID
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// 对话摘要内容
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// 技术标签
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// 发生日期
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// 相似度分数（检索时返回）
    /// </summary>
    public float? Score { get; set; }
}

/// <summary>
/// 记忆上下文（用于 Prompt 注入）
/// </summary>
public class MemoryContext
{
    /// <summary>
    /// 用户画像事实列表
    /// </summary>
    public List<UserFactDto> UserFacts { get; set; } = new();

    /// <summary>
    /// 相关情境记忆列表
    /// </summary>
    public List<EpisodicMemoryDto> EpisodicMemories { get; set; } = new();

    /// <summary>
    /// 是否有有效记忆
    /// </summary>
    public bool HasMemory => UserFacts.Count > 0 || EpisodicMemories.Count > 0;
}

/// <summary>
/// LLM 提取的用户偏好结构
/// </summary>
public class ExtractedUserFact
{
    /// <summary>
    /// 分类
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// 内容
    /// </summary>
    public string Content { get; set; } = string.Empty;
}
