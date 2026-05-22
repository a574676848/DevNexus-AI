using System.Text.RegularExpressions;

namespace DevNexus.Core.Services.Terminal;

/// <summary>
/// 终端输出观察摘要构建器。
/// </summary>
public static class TerminalOutputWatchSummaryBuilder
{
    private const string Separator = "；";
    private const string ErrorLabel = "检测到错误输出";
    private const string WarningLabel = "检测到警告输出";
    private const string ApprovalLabel = "检测到权限或审批拦截";
    private const string WaitingInputLabel = "检测到交互输入提示";

    private static readonly Regex ErrorWatchPattern = new(
        @"(?im)\b(error|exception|failed|fatal|traceback)\b",
        RegexOptions.Compiled);

    private static readonly Regex WarningWatchPattern = new(
        @"(?im)\bwarning\b",
        RegexOptions.Compiled);

    private static readonly Regex ApprovalWatchPattern = new(
        @"(?im)(permission denied|access denied|not allowed|approval required)",
        RegexOptions.Compiled);

    private static readonly Regex WaitingInputWatchPattern = new(
        @"(?im)(password\s*[:：]?$|continue\?\s*\[y/n\]|press\s+enter\s+to\s+continue|confirm\s*\[y/n\]|enter\s+.*[:：]$)",
        RegexOptions.Compiled);

    /// <summary>
    /// 从增量输出中识别观察标签。
    /// </summary>
    public static IReadOnlyList<string> DetectLabels(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return Array.Empty<string>();
        }

        var labels = new List<string>(capacity: 4);
        AddLabelIfMatch(labels, ErrorWatchPattern, output, ErrorLabel);
        AddLabelIfMatch(labels, WarningWatchPattern, output, WarningLabel);
        AddLabelIfMatch(labels, ApprovalWatchPattern, output, ApprovalLabel);
        AddLabelIfMatch(labels, WaitingInputWatchPattern, output, WaitingInputLabel);
        return labels;
    }

    /// <summary>
    /// 构建去重后的观察摘要。
    /// </summary>
    public static string? Build(IEnumerable<string> labels)
    {
        var normalized = labels
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return normalized.Count == 0 ? null : string.Join(Separator, normalized);
    }

    /// <summary>
    /// 合并两个观察摘要。
    /// </summary>
    public static string? Merge(string? existing, string? next)
    {
        if (string.IsNullOrWhiteSpace(existing))
        {
            return next;
        }

        if (string.IsNullOrWhiteSpace(next))
        {
            return existing;
        }

        return Build(Split(existing).Concat(Split(next)));
    }

    /// <summary>
    /// 统计换行符数量。
    /// </summary>
    public static int CountNewLines(string output)
    {
        return string.IsNullOrEmpty(output)
            ? 0
            : output.Count(character => character == '\n');
    }

    private static IEnumerable<string> Split(string value)
    {
        return value.Split(
            Separator,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static void AddLabelIfMatch(
        ICollection<string> labels,
        Regex pattern,
        string output,
        string label)
    {
        if (pattern.IsMatch(output))
        {
            labels.Add(label);
        }
    }
}
