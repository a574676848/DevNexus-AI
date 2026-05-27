namespace DevNexus.Client.Shared.Components.Chat;

/// <summary>
/// 流式 Markdown 分帧策略，避免 provider 一次性推送大段文本时造成气泡闪现或渲染卡顿。
/// </summary>
public sealed class StreamingMarkdownFramePolicy
{
    public const int DefaultLargeDeltaThreshold = 8;
    public const int DefaultMaxRevealCharsPerFrame = 4;

    public static StreamingMarkdownFramePolicy Default { get; } = new();

    public StreamingMarkdownFramePolicy(
        int largeDeltaThreshold = DefaultLargeDeltaThreshold,
        int maxRevealCharsPerFrame = DefaultMaxRevealCharsPerFrame)
    {
        LargeDeltaThreshold = Math.Max(1, largeDeltaThreshold);
        MaxRevealCharsPerFrame = Math.Max(1, maxRevealCharsPerFrame);
    }

    public int LargeDeltaThreshold { get; }

    public int MaxRevealCharsPerFrame { get; }

    public string BuildNextFrame(string lastRenderedContent, string targetContent)
    {
        lastRenderedContent ??= string.Empty;
        targetContent ??= string.Empty;

        if (!targetContent.StartsWith(lastRenderedContent, StringComparison.Ordinal))
        {
            return targetContent;
        }

        var remainingChars = targetContent.Length - lastRenderedContent.Length;
        if (remainingChars <= LargeDeltaThreshold)
        {
            return targetContent;
        }

        var nextChars = Math.Min(MaxRevealCharsPerFrame, remainingChars);
        return lastRenderedContent + targetContent.Substring(lastRenderedContent.Length, nextChars);
    }
}
