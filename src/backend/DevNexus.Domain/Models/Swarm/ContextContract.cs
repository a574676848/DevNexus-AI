using DevNexus.Domain.Enums;

namespace DevNexus.Domain.Models.Swarm;

/// <summary>
/// 表示上下文工作包之间的稳定输入输出契约。
/// </summary>
public class ContextContract
{
    /// <summary>
    /// 契约名称。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 契约说明。
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 契约 Schema 定义。
    /// </summary>
    public string Schema { get; set; } = string.Empty;

    /// <summary>
    /// 是否必填。
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// 生产方工作包 ID。
    /// </summary>
    public string? ProducerPackageId { get; set; }

    /// <summary>
    /// 消费方工作包 ID。
    /// </summary>
    public string? ConsumerPackageId { get; set; }

    /// <summary>
    /// 契约涉及的上下文类型。
    /// </summary>
    public SwarmContextType ContextType { get; set; } = SwarmContextType.Unknown;
}
