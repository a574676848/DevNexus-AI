namespace DevNexus.Shared.DTOs;

/// <summary>
/// 模型定价响应 DTO
/// </summary>
public class ModelPricingResponse
{
    /// <summary>
    /// 定价配置 ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 提供商类型（llm / embedding）
    /// </summary>
    public string ProviderType { get; set; } = ModelInvocationProviderTypes.Llm;

    /// <summary>
    /// 关联的供应商主键 ID
    /// </summary>
    public Guid ProviderId { get; set; }

    /// <summary>
    /// 供应商显示名称
    /// </summary>
    public string? ProviderDisplayName { get; set; }

    /// <summary>
    /// 供应商标识
    /// </summary>
    public string? ProviderProviderId { get; set; }

    /// <summary>
    /// 输入成本 (每百万Token，单位：人民币)
    /// </summary>
    public decimal InputCostPerMillion { get; set; }

    /// <summary>
    /// 输出成本 (每百万Token，单位：人民币)
    /// </summary>
    public decimal OutputCostPerMillion { get; set; }

    /// <summary>
    /// 货币单位
    /// </summary>
    public string Currency { get; set; } = "CNY";

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 创建模型定价请求 DTO
/// </summary>
public class CreateModelPricingRequest
{
    /// <summary>
    /// 提供商类型（llm / embedding）
    /// </summary>
    public string ProviderType { get; set; } = ModelInvocationProviderTypes.Llm;

    /// <summary>
    /// 关联的供应商主键 ID
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
    /// 是否启用 (默认 true)
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// 更新模型定价请求 DTO
/// </summary>
public class UpdateModelPricingRequest
{
    /// <summary>
    /// 提供商类型（llm / embedding）
    /// </summary>
    public string ProviderType { get; set; } = ModelInvocationProviderTypes.Llm;

    /// <summary>
    /// 输入成本 (每百万Token，单位：人民币)
    /// </summary>
    public decimal InputCostPerMillion { get; set; }

    /// <summary>
    /// 输出成本 (每百万Token，单位：人民币)
    /// </summary>
    public decimal OutputCostPerMillion { get; set; }

    /// <summary>
    /// 货币单位
    /// </summary>
    public string Currency { get; set; } = "CNY";

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}
