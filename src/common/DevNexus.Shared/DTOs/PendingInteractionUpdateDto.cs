namespace DevNexus.Shared.DTOs;

/// <summary>
/// 挂起交互列表更新 DTO。
/// </summary>
public class PendingInteractionUpdateDto
{
    /// <summary>
    /// 会话标识。
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// 当前会话的活跃挂起交互列表。
    /// </summary>
    public List<PendingInteractionDto> Interactions { get; set; } = new();
}
