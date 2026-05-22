namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 系统经验提纯 Prompt。
/// </summary>
public sealed class ExperienceDistillationPrompt
{
    /// <summary>
    /// Prompt 正文。
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Prompt 稳定指纹。
    /// </summary>
    public string Fingerprint => PromptFingerprint.ComputeHash(Content);

    /// <summary>
    /// 隐式转换为 Prompt 正文。
    /// </summary>
    public static implicit operator string(ExperienceDistillationPrompt prompt)
    {
        return prompt.Content;
    }
}
