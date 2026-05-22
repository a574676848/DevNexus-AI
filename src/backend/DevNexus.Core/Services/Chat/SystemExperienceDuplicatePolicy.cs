using DevNexus.Domain.Entities;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 系统经验重复写入策略。
/// </summary>
public static class SystemExperienceDuplicatePolicy
{
    /// <summary>
    /// 判断已有经验是否应进入候选重复判定。
    /// </summary>
    public static bool IsCandidate(SystemExperience candidate, SystemExperience existing)
    {
        return candidate.Type == existing.Type;
    }

    /// <summary>
    /// 判断候选经验是否与已有经验重复。
    /// </summary>
    public static bool IsDuplicate(
        SystemExperience candidate,
        IReadOnlyCollection<SystemExperience> existingExperiences)
    {
        var fingerprint = SystemExperienceFingerprint.Compute(candidate);
        return existingExperiences.Any(existing =>
            string.Equals(SystemExperienceFingerprint.Compute(existing), fingerprint, StringComparison.Ordinal)
            || SystemExperienceFingerprint.HasFingerprint(existing, fingerprint));
    }
}
