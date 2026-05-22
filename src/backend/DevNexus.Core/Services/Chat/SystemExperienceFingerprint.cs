using DevNexus.Domain.Entities;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 系统经验指纹工具。
/// </summary>
public static class SystemExperienceFingerprint
{
    /// <summary>
    /// 上下文标签中的指纹前缀。
    /// </summary>
    public const string ContextTagPrefix = "fingerprint:";

    /// <summary>
    /// 计算系统经验语义指纹。
    /// </summary>
    public static string Compute(SystemExperience experience)
    {
        var canonical = string.Join(
            "\n",
            experience.Type.ToString(),
            Normalize(experience.Intent),
            Normalize(experience.SolutionSop));

        return PromptFingerprint.ComputeHash(canonical);
    }

    /// <summary>
    /// 将指纹写入上下文标签。
    /// </summary>
    public static string MergeIntoContextTags(string? contextTags, string fingerprint)
    {
        var tags = SplitTags(contextTags)
            .Where(tag => !tag.StartsWith(ContextTagPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        tags.Add($"{ContextTagPrefix}{fingerprint}");
        return string.Join(",", tags);
    }

    /// <summary>
    /// 判断经验是否携带指定指纹。
    /// </summary>
    public static bool HasFingerprint(SystemExperience experience, string fingerprint)
    {
        return SplitTags(experience.ContextTags)
            .Any(tag => string.Equals(tag, $"{ContextTagPrefix}{fingerprint}", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> SplitTags(string? contextTags)
    {
        return (contextTags ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(tag => !string.IsNullOrWhiteSpace(tag));
    }

    private static string Normalize(string? value)
    {
        return string.Join(
            " ",
            (value ?? string.Empty)
                .Trim()
                .ToLowerInvariant()
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
