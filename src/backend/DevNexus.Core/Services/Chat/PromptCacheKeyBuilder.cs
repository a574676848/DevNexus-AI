namespace DevNexus.Core.Services.Chat;

/// <summary>
/// Prompt 缓存键构建器。
/// </summary>
public static class PromptCacheKeyBuilder
{
    private const string Version = "v1";
    private const string EmptyToolSchemaHash = "none";

    /// <summary>
    /// 根据稳定前缀与工具 Schema 指纹生成 Prompt 缓存键。
    /// </summary>
    public static string Build(string stablePrefixHash, string? toolSchemaHash)
    {
        var normalizedStablePrefixHash = NormalizeHash(stablePrefixHash);
        var normalizedToolSchemaHash = NormalizeHash(toolSchemaHash, EmptyToolSchemaHash);
        var canonical = string.Join(
            '\n',
            $"version:{Version}",
            $"stable:{normalizedStablePrefixHash}",
            $"tools:{normalizedToolSchemaHash}");

        return PromptFingerprint.ComputeHash(canonical);
    }

    private static string NormalizeHash(string? value, string fallback = "")
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim().ToLowerInvariant();
    }
}
