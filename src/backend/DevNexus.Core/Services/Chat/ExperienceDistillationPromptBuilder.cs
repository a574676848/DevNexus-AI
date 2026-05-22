namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 系统经验提纯 Prompt 构建器。
/// </summary>
public static class ExperienceDistillationPromptBuilder
{
    /// <summary>
    /// 构建系统经验提纯 Prompt。
    /// </summary>
    public static ExperienceDistillationPrompt Build(string question, string answer)
    {
        var content = string.Join(
            Environment.NewLine,
            "请判断接下来的 QA 是否具有普适性的经验价值，是否能作为 SOP 解决同类问题。",
            $"输出协议：{ExperienceDistillationOutputProtocol.Version}",
            "默认不要写入长期经验。只有命中以下任一高价值信号时才允许输出 SOP：",
            BuildBullets(ExperienceDistillationOutputProtocol.HighValueSignals),
            "",
            "以下内容必须拒绝提纯：",
            BuildBullets(ExperienceDistillationOutputProtocol.SkipConditions),
            "",
            $"如果没有高价值信号，请只回复 {ExperienceDistillationOutputProtocol.NoValueMarker}。",
            "如果有，请提纯为高质量、可复用、面向同类问题的 SOP 描述，不要保留一次性细节。",
            $"SOP 正文必须控制在 {ExperienceDistillationOutputProtocol.MaximumSopCharacters} 字以内。",
            "禁止把原始 QA、聊天日志、工具输出、命令输出或临时调试记录写入 SOP。",
            "",
            "【用户问题】",
            question.Trim(),
            "",
            "【助手回答】",
            answer.Trim(),
            "",
            "【输出格式】",
            $"第一行：{ExperienceDistillationOutputProtocol.IntentMarker}用户的核心意图",
            "第二行开始：SOP 步骤描述",
            "不要输出 Markdown 代码块，不要添加格式外说明。");

        return new ExperienceDistillationPrompt { Content = content };
    }

    private static string BuildBullets(IReadOnlyList<string> items)
    {
        return string.Join(
            Environment.NewLine,
            items.Select((item, index) => $"{index + 1}. {item}"));
    }
}
