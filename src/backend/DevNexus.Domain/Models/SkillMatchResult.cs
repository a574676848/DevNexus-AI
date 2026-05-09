using DevNexus.Shared.Enums;

namespace DevNexus.Domain.Models;

/// <summary>
/// Skill 匹配结果
/// </summary>
public class SkillMatchResult
{
    /// <summary>匹配到的 Skill 元数据</summary>
    public SkillMetadata Skill { get; set; } = null!;

    /// <summary>匹配得分 (0.0 ~ 1.0)</summary>
    public double Score { get; set; }

    /// <summary>匹配方式</summary>
    public SkillMatchMethod Method { get; set; }
}
