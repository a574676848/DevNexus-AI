namespace DevNexus.Shared.Constants;

/// <summary>
/// 聊天消息 Content 字典的共享协议键。
/// </summary>
public static class ChatMessageContentKeys
{
    /// <summary>
    /// 可见正文内容。
    /// </summary>
    public const string Text = "text";

    /// <summary>
    /// 结构化思维链内容。
    /// </summary>
    public const string Thinking = "thinking";

    /// <summary>
    /// 流式生成期间暂存的思维链增量。
    /// </summary>
    public const string ThinkingPartial = "thinking_partial";

    /// <summary>
    /// 流式生成期间暂存的正文增量。
    /// </summary>
    public const string TextPartial = "text_partial";

    /// <summary>
    /// 外部执行链路暂存的思维链增量。
    /// </summary>
    public const string ThinkingExternalPartial = "thinking_external_partial";
}
