using DevNexus.Domain.Models;

namespace DevNexus.Core.Abstractions;

/// <summary>
/// Skill 匹配器 - 根据用户消息匹配最合适的 Skill
/// </summary>
public interface ISkillMatcher
{
    /// <summary>
    /// 匹配 Skill（三级漏斗：正则 → 关键词 → 语义）
    /// </summary>
    /// <param name="userMessage">用户消息</param>
    /// <param name="availableSkills">可用 Skill 列表</param>
    /// <param name="maxResults">最大匹配数量</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>按匹配度排序的 Skill 列表</returns>
    Task<List<SkillMatchResult>> MatchAsync(
        string userMessage,
        IReadOnlyList<SkillMetadata> availableSkills,
        int maxResults = 3,
        CancellationToken ct = default);
}
