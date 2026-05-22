namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 经验提纯问答对。
/// </summary>
public sealed class ExperienceDistillationQaPair
{
    /// <summary>
    /// 用户问题。
    /// </summary>
    public required string Question { get; init; }

    /// <summary>
    /// 助手回答。
    /// </summary>
    public required string Answer { get; init; }
}
