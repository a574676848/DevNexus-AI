using System.Text.RegularExpressions;
using DevNexus.Core.Abstractions;
using DevNexus.Domain.Models;
using DevNexus.Shared.Enums;
using Microsoft.Extensions.Logging;

namespace DevNexus.Infrastructure.Services.Skills;

/// <summary>
/// Skill 匹配器实现 - 三级漏斗匹配：正则 → 关键词/标签 → 语义相似度
/// </summary>
public class SkillMatcher : ISkillMatcher
{
    private readonly ILogger<SkillMatcher> _logger;

    public SkillMatcher(ILogger<SkillMatcher> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<List<SkillMatchResult>> MatchAsync(
        string userMessage,
        IReadOnlyList<SkillMetadata> availableSkills,
        int maxResults = 3,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userMessage) || availableSkills.Count == 0)
        {
            return Task.FromResult(new List<SkillMatchResult>());
        }

        var results = new List<SkillMatchResult>();
        var messageLower = userMessage.ToLowerInvariant();

        foreach (var skill in availableSkills.Where(s => s.AutoTrigger))
        {
            ct.ThrowIfCancellationRequested();

            // Level 1: 正则模式匹配（最快，命中则 score=1.0）
            var regexScore = MatchByTriggerPatterns(userMessage, skill);
            if (regexScore > 0)
            {
                results.Add(new SkillMatchResult
                {
                    Skill = skill,
                    Score = regexScore,
                    Method = SkillMatchMethod.TriggerPattern
                });
                continue;
            }

            // Level 2: 关键词 + 标签匹配
            var keywordScore = MatchByKeywordsAndTags(messageLower, skill);
            if (keywordScore > 0)
            {
                results.Add(new SkillMatchResult
                {
                    Skill = skill,
                    Score = keywordScore,
                    Method = SkillMatchMethod.KeywordTag
                });
                continue;
            }

            // Level 3: 语义相似度（当前使用简化的描述词匹配，后续可接入 EmbeddingService）
            var semanticScore = MatchByDescriptionSimilarity(messageLower, skill);
            if (semanticScore >= 0.65)
            {
                results.Add(new SkillMatchResult
                {
                    Skill = skill,
                    Score = semanticScore,
                    Method = SkillMatchMethod.SemanticSimilarity
                });
            }
        }

        // 按得分降序 → 优先级降序排序，取 top N
        var topResults = results
            .OrderByDescending(r => r.Score)
            .ThenByDescending(r => r.Skill.Priority)
            .Take(maxResults)
            .ToList();

        if (topResults.Count > 0)
        {
            _logger.LogDebug(
                "[Skill.Matcher] 匹配完成 | Message=\"{Message}\" Matched={Count} Top={TopSkill}({TopScore:F2})",
                userMessage.Length > 50 ? userMessage[..50] + "..." : userMessage,
                topResults.Count,
                topResults[0].Skill.Name,
                topResults[0].Score);
        }

        return Task.FromResult(topResults);
    }

    // ==================== 私有匹配方法 ====================

    /// <summary>
    /// Level 1: 正则模式匹配
    /// </summary>
    private static double MatchByTriggerPatterns(string message, SkillMetadata skill)
    {
        if (skill.TriggerPatterns.Count == 0) return 0;

        foreach (var pattern in skill.TriggerPatterns)
        {
            try
            {
                if (Regex.IsMatch(message, pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)))
                {
                    return 1.0;
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // 防止恶意正则 DoS，静默忽略
            }
        }

        return 0;
    }

    /// <summary>
    /// Level 2: 关键词 + 标签匹配（简化 BM25）
    /// </summary>
    private static double MatchByKeywordsAndTags(string messageLower, SkillMetadata skill)
    {
        var matchTokens = new List<string>();

        // 将 Skill 名称按连字符拆分作为关键词
        matchTokens.AddRange(skill.Name.Split('-', StringSplitOptions.RemoveEmptyEntries));

        // 添加标签
        matchTokens.AddRange(skill.Tags.Select(t => t.ToLowerInvariant()));

        if (matchTokens.Count == 0) return 0;

        // 计算匹配比例
        var matchCount = matchTokens.Count(token => messageLower.Contains(token));
        if (matchCount == 0) return 0;

        // 得分 = 匹配数 / 总关键词数，上限 0.95（低于正则匹配）
        return Math.Min(0.95, (double)matchCount / matchTokens.Count);
    }

    /// <summary>
    /// Level 3: 基于描述的简化语义匹配
    /// 使用词重叠度作为简化的语义相似度估计
    /// 后续可替换为真正的 EmbeddingService 向量相似度
    /// </summary>
    private static double MatchByDescriptionSimilarity(string messageLower, SkillMetadata skill)
    {
        var descLower = skill.Description.ToLowerInvariant();

        // 将描述和消息分词（简化：按空格和中文标点分割）
        var descTokens = TokenizeText(descLower);
        var messageTokens = TokenizeText(messageLower);

        if (descTokens.Count == 0 || messageTokens.Count == 0) return 0;

        // 计算 Jaccard 相似度
        var intersection = descTokens.Intersect(messageTokens).Count();
        var union = descTokens.Union(messageTokens).Count();

        if (union == 0) return 0;

        return (double)intersection / union;
    }

    /// <summary>
    /// 简化分词：按空格、标点分割，过滤短词
    /// </summary>
    private static HashSet<string> TokenizeText(string text)
    {
        // 按非字母数字字符分割
        var tokens = Regex.Split(text, @"[\s\p{P}]+")
            .Where(t => t.Length >= 2)
            .Select(t => t.ToLowerInvariant())
            .ToHashSet();

        return tokens;
    }
}
