namespace DevNexus.Shared.Constants;

/// <summary>
/// Artifact 相关 Block metadata 的共享协议定义。
/// </summary>
public static class ArtifactBlockMetadataConstants
{
    public const string ArtifactId = "artifactId";
    public const string Type = "type";
    public const string Language = "language";
    public const string Title = "title";
    public const string OriginalCode = "originalCode";
    public const string ModifiedCode = "modifiedCode";
    public const string IsComplete = "isComplete";
    public const string ChartType = "chartType";
    public const string Layout = "layout";

    public const string TypeCode = "code";
    public const string TypeHtml = "html";
    public const string TypeChart = "chart";

    public const string LanguagePlaintext = "plaintext";

    public const string ChartTypeAuto = "auto";
    public const string ChartTypeLine = "line";

    public const string DefaultChartTitle = "图表";
    public const string DefaultCodeTitle = "Code";
    public const string DefaultDiffTitle = "Code Diff";
    public const string DefaultHtmlTitle = "HTML Preview";
    public const string DefaultArtifactTitle = "Artifact";

    /// <summary>
    /// 规范化 artifact 类型协议值。
    /// </summary>
    public static string NormalizeType(string? type, string fallback = TypeCode)
    {
        return type?.Trim().ToLowerInvariant() switch
        {
            TypeCode => TypeCode,
            TypeHtml => TypeHtml,
            TypeChart => TypeChart,
            _ => fallback
        };
    }
}