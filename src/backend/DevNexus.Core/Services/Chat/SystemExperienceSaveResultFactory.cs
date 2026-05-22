using DevNexus.Core.DTOs;
using DevNexus.Domain.Entities;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 系统经验保存结果工厂。
/// </summary>
public static class SystemExperienceSaveResultFactory
{
    /// <summary>
    /// 创建重复跳过结果。
    /// </summary>
    public static ExperienceSaveResultDto Duplicate(SystemExperience existingExperience, SystemExperience attemptedExperience)
    {
        var memoryCitation = BuildMemoryCitation(existingExperience);
        var attemptMemoryCitation = BuildMemoryCitation(attemptedExperience);
        return new ExperienceSaveResultDto
        {
            Experience = existingExperience,
            IsDuplicate = true,
            CitationFingerprint = memoryCitation.CitationFingerprint,
            MemoryCitation = memoryCitation,
            AttemptCitationFingerprint = attemptMemoryCitation.CitationFingerprint,
            AttemptMemoryCitation = attemptMemoryCitation,
            Reason = SystemExperienceSaveReasons.DuplicateSkipped
        };
    }

    /// <summary>
    /// 创建重复跳过结果。
    /// </summary>
    public static ExperienceSaveResultDto Duplicate(SystemExperience experience)
    {
        return Duplicate(experience, experience);
    }

    /// <summary>
    /// 创建新增并完成索引结果。
    /// </summary>
    public static ExperienceSaveResultDto CreatedAndIndexed(SystemExperience experience)
    {
        var memoryCitation = BuildMemoryCitation(experience);
        return new ExperienceSaveResultDto
        {
            Experience = experience,
            IsNew = true,
            VectorIndexed = true,
            CitationFingerprint = memoryCitation.CitationFingerprint,
            MemoryCitation = memoryCitation,
            AttemptCitationFingerprint = memoryCitation.CitationFingerprint,
            AttemptMemoryCitation = memoryCitation,
            Reason = SystemExperienceSaveReasons.CreatedAndIndexed
        };
    }

    /// <summary>
    /// 创建新增但索引失败结果。
    /// </summary>
    public static ExperienceSaveResultDto CreatedButIndexFailed(SystemExperience experience)
    {
        var memoryCitation = BuildMemoryCitation(experience);
        return new ExperienceSaveResultDto
        {
            Experience = experience,
            IsNew = true,
            CitationFingerprint = memoryCitation.CitationFingerprint,
            MemoryCitation = memoryCitation,
            AttemptCitationFingerprint = memoryCitation.CitationFingerprint,
            AttemptMemoryCitation = memoryCitation,
            Reason = SystemExperienceSaveReasons.CreatedButIndexFailed
        };
    }

    private static SystemExperienceMemoryCitation BuildMemoryCitation(SystemExperience experience)
    {
        return SystemExperienceMemoryCitation
            .FromContextTags(experience.Id, experience.ContextTags);
    }
}
