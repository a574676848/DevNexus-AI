namespace DevNexus.Core.DTOs;

using DevNexus.Domain.Entities;

/// <summary>
/// 经验库匹配结果 DTO
/// </summary>
public class ExperienceMatchDto
{
    /// <summary>
    /// 匹配到的系统经验
    /// </summary>
    public SystemExperience Experience { get; set; } = null!;
    
    /// <summary>
    /// 相似度评分 (0-1.0)
    /// </summary>
    public float Similarity { get; set; }
}
