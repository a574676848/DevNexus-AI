using DevNexus.Domain.Entities.Base;
using DevNexus.Shared.DTOs;

namespace DevNexus.Domain.Entities;

/// <summary>
/// 模型定价配置
/// </summary>
public class ModelPricing : AuditableEntity
{
    /// <summary>
    /// 提供商类型（llm / embedding）
    /// </summary>
    public string ProviderType { get; set; } = ModelInvocationProviderTypes.Llm;

    /// <summary>
    /// 关联的供应商 ID
    /// </summary>
    public Guid ProviderId { get; set; }

    /// <summary>
    /// 输入成本 (每百万Token，单位：人民币)
    /// </summary>
    public decimal InputCostPerMillion { get; set; }

    /// <summary>
    /// 输出成本 (每百万Token，单位：人民币)
    /// </summary>
    public decimal OutputCostPerMillion { get; set; }

    /// <summary>
    /// 货币单位 (默认 CNY)
    /// </summary>
    public string Currency { get; set; } = "CNY";

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}
