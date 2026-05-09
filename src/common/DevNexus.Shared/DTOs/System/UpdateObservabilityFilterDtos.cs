namespace DevNexus.Shared.DTOs;

/// <summary>
/// 更新观测筛选条件。
/// </summary>
public class UpdateObservabilityFilterRequest
{
    /// <summary>
    /// 发布版本标识。
    /// </summary>
    public Guid? ReleaseId { get; set; }

    /// <summary>
    /// 投放规则标识。
    /// </summary>
    public Guid? RolloutId { get; set; }

    /// <summary>
    /// 统计最近多少天。
    /// </summary>
    public int Days { get; set; } = 30;

    /// <summary>
    /// 事件类型。
    /// </summary>
    public string? EventType { get; set; }

    /// <summary>
    /// 结果状态。
    /// </summary>
    public string? Result { get; set; }
}
