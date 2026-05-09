using DevNexus.Domain.Entities.Base;
using DevNexus.Shared.Enums;

namespace DevNexus.Domain.Entities;

/// <summary>
/// 智能体系统经验实体 (System 1 记忆库核心)
/// </summary>
public class SystemExperience : AuditableEntity, ISoftDelete
{
    // --- 核心特征 ---
    
    /// <summary>
    /// 提纯后的用户意图（用于向量检索分析）
    /// </summary>
    public string Intent { get; set; } = string.Empty;
    
    /// <summary>
    /// 上下文标签（如 ".net10", "docker"）
    /// </summary>
    public string ContextTags { get; set; } = string.Empty;
    
    /// <summary>
    /// 经验类型
    /// </summary>
    public ExperienceType Type { get; set; }

    // --- 解决路径与答案 ---
    
    /// <summary>
    /// 普通聊天的精华答案，或上下文工作包拓扑快照
    /// </summary>
    public string SolutionSop { get; set; } = string.Empty;
    
    /// <summary>
    /// 为什么这么做的简短原因（用于 Few-shot 提示）
    /// </summary>
    public string ReasoningSummary { get; set; } = string.Empty;

    // --- 淘汰与生命周期指标 ---
    
    /// <summary>
    /// 成功命中并被采用的次数
    /// </summary>
    public int UsageCount { get; set; } = 1;
    
    /// <summary>
    /// 效用评分（随时间衰减，根据用户反馈增减，默认 1.0）
    /// </summary>
    public double UtilityScore { get; set; } = 1.0;
    
    /// <summary>
    /// 最后一次被检索匹配到的时间
    /// </summary>
    public DateTime LastMatchedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 是否由管理员人工置顶（免疫自然衰减与遗忘）
    /// </summary>
    public bool IsPinned { get; set; } = false;
}
