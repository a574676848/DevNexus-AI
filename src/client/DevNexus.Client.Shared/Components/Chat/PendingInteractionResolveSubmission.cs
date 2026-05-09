namespace DevNexus.Client.Shared.Components.Chat;

/// <summary>
/// 挂起交互解决提交模型。
/// </summary>
public class PendingInteractionResolveSubmission
{
    /// <summary>
    /// 挂起交互标识。
    /// </summary>
    public Guid InteractionId { get; init; }

    /// <summary>
    /// 解决动作。
    /// </summary>
    public string Action { get; init; } = "submit";

    /// <summary>
    /// 用户提交的字段值。
    /// </summary>
    public Dictionary<string, string?> Values { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
