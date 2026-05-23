namespace DevNexus.Core.Services.Swarm;

/// <summary>
/// Swarm 聊天主线展示文案策略。
/// </summary>
public static class SwarmChatPresentation
{
    private const string StartedTitle = "**Swarm 协作已启动**";
    private const string StartedDescription = "已切换到工作包协作模式。请在 Swarm 面板查看阶段、失败原因和执行报告。";
    private const string SectionDivider = "\n\n---\n\n";

    /// <summary>
    /// 构建进入 Swarm 协作模式时写入聊天主线的低噪提示。
    /// </summary>
    public static string BuildStartedMessage()
    {
        return $"{StartedTitle}\n\n{StartedDescription}{SectionDivider}";
    }
}
