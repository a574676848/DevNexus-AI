namespace DevNexus.Core.Services.Chat;

/// <summary>
/// Prompt 动态上下文消息构建器。
/// </summary>
public static class PromptDynamicContextMessageBuilder
{
    /// <summary>
    /// 系统注入消息前缀。
    /// </summary>
    public const string SystemInjectedPrefix = "[系统注入上下文]";

    /// <summary>
    /// 构建系统注入型动态上下文消息。
    /// </summary>
    public static string? Build(string? dynamicContext)
    {
        return Build("本轮动态上下文", dynamicContext);
    }

    /// <summary>
    /// 构建带标题的系统注入型动态上下文消息。
    /// </summary>
    public static string? Build(string title, string? dynamicContext)
    {
        if (string.IsNullOrWhiteSpace(dynamicContext))
        {
            return null;
        }

        var normalizedTitle = string.IsNullOrWhiteSpace(title)
            ? "本轮动态上下文"
            : title.Trim();

        return string.Join(
            Environment.NewLine,
            SystemInjectedPrefix,
            $"[{normalizedTitle}]",
            "以下内容只用于当前轮推理，不属于稳定系统提示，也不应作为缓存标记候选：",
            dynamicContext.Trim());
    }

    /// <summary>
    /// 判断消息是否为系统注入型动态上下文。
    /// </summary>
    public static bool IsSystemInjected(string? content)
    {
        return !string.IsNullOrWhiteSpace(content)
            && content.TrimStart().StartsWith(SystemInjectedPrefix, StringComparison.Ordinal);
    }
}
