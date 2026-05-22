namespace DevNexus.Core.Services.Chat;

/// <summary>
/// Prompt 缓存命名空间构建器。
/// </summary>
public static class PromptCacheNamespaceBuilder
{
    private const string Unknown = "unknown";

    /// <summary>
    /// 根据 Provider 与模型上下文构建审计分组命名空间。
    /// </summary>
    public static string Build(
        string? providerType,
        string? providerName,
        string? providerId,
        string? modelId)
    {
        return string.Join(
            '|',
            Normalize(providerType),
            Normalize(providerName),
            Normalize(providerId),
            Normalize(modelId));
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? Unknown
            : value.Trim().ToLowerInvariant();
    }
}
