using DevNexus.Core.Models.Cli;

namespace DevNexus.Core.Services.Cli;

/// <summary>
/// CLI 运行时输入协议。
/// </summary>
public static class CliRuntimeInputProtocol
{
    private const int PreviewMaxLength = 120;
    private const string BlankLinePreview = "[stdin] 空行";
    private const string InputPreviewPrefix = "[stdin] ";
    private const string TruncatedSuffix = "...";

    /// <summary>
    /// 构建运行时可写入输入和模型可见摘要。
    /// </summary>
    public static CliRuntimeInputEnvelope Build(string? input)
    {
        var original = input ?? string.Empty;
        var normalized = NormalizeInput(original);

        return new CliRuntimeInputEnvelope(
            normalized,
            BuildPreview(normalized),
            original.Length,
            string.IsNullOrWhiteSpace(normalized));
    }

    private static string NormalizeInput(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        return input.TrimEnd('\r', '\n');
    }

    private static string BuildPreview(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return BlankLinePreview;
        }

        var compact = input
            .Replace("\r\n", "\\n", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\n", StringComparison.Ordinal);

        if (compact.Length > PreviewMaxLength)
        {
            compact = compact[..PreviewMaxLength] + TruncatedSuffix;
        }

        return InputPreviewPrefix + compact;
    }
}
