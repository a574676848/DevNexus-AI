using DevNexus.Domain.Entities;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 系统经验生命周期策略。
/// </summary>
public static class SystemExperienceLifecyclePolicy
{
    /// <summary>
    /// 经验向量检索最小相关度。
    /// </summary>
    public const double MinimumSearchRelevance = 0.8;

    /// <summary>
    /// 每次命中后的效用增量。
    /// </summary>
    public const double BoostIncrement = 0.1;

    /// <summary>
    /// 经验效用评分上限。
    /// </summary>
    public const double MaximumUtilityScore = 10.0;

    /// <summary>
    /// 未命中经验进入衰减的天数。
    /// </summary>
    public const int StaleAfterDays = 30;

    /// <summary>
    /// 过期经验每日衰减倍率。
    /// </summary>
    public const double StaleDecayFactor = 0.8;

    /// <summary>
    /// 低于该效用评分时淘汰经验。
    /// </summary>
    public const double PruneBelowUtilityScore = 0.2;

    /// <summary>
    /// 计算命中后的经验效用评分。
    /// </summary>
    public static double BoostUtilityScore(double currentScore)
    {
        return Math.Min(MaximumUtilityScore, currentScore + BoostIncrement);
    }

    /// <summary>
    /// 应用重复经验再发现反馈。
    /// </summary>
    public static void ApplyDuplicateRediscovery(SystemExperience experience, DateTime matchedAt)
    {
        experience.UsageCount += 1;
        experience.UtilityScore = BoostUtilityScore(experience.UtilityScore);
        experience.LastMatchedAt = matchedAt;
    }

    /// <summary>
    /// 判断向量检索结果是否可作为经验命中。
    /// </summary>
    public static bool IsSearchMatch(double relevance)
    {
        return relevance >= MinimumSearchRelevance;
    }

    /// <summary>
    /// 计算经验过期边界时间。
    /// </summary>
    public static DateTime GetStaleBoundary(DateTime now)
    {
        return now.AddDays(-StaleAfterDays);
    }

    /// <summary>
    /// 对过期经验执行衰减并返回是否需要淘汰。
    /// </summary>
    public static bool ApplyDecay(SystemExperience experience)
    {
        experience.UtilityScore *= StaleDecayFactor;
        return experience.UtilityScore < PruneBelowUtilityScore;
    }
}
