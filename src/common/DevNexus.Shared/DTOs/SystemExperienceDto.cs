using DevNexus.Shared.Enums;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// 系统经验传输对象
/// </summary>
public class SystemExperienceDto
{
    public Guid Id { get; set; }
    
    public string Intent { get; set; } = string.Empty;
    
    public string ContextTags { get; set; } = string.Empty;
    
    public ExperienceType Type { get; set; }

    public string SolutionSop { get; set; } = string.Empty;
    
    public string ReasoningSummary { get; set; } = string.Empty;

    public int UsageCount { get; set; }
    
    public double UtilityScore { get; set; }
    
    public DateTime? LastMatchedAt { get; set; }
    
    public bool IsPinned { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    public DateTime UpdatedAt { get; set; }
}
