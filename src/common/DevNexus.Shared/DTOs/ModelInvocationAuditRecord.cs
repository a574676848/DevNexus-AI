namespace DevNexus.Shared.DTOs;

/// <summary>
/// 模型调用审计写入记录。
/// </summary>
public class ModelInvocationAuditRecord
{
    /// <summary>
    /// 审计记录ID。
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 调用主体类型。
    /// </summary>
    public string OwnerType { get; set; } = ModelInvocationOwnerTypes.System;

    /// <summary>
    /// 调用主体用户ID。
    /// </summary>
    public Guid? OwnerUserId { get; set; }

    /// <summary>
    /// 调用技术类型。
    /// </summary>
    public string InvocationKind { get; set; } = ModelInvocationKinds.Other;

    /// <summary>
    /// 场景编码。
    /// </summary>
    public string SceneCode { get; set; } = ModelInvocationSceneCodes.SystemOther;

    /// <summary>
    /// 场景分组。
    /// </summary>
    public string SceneCategory { get; set; } = ModelInvocationSceneCategories.Other;

    /// <summary>
    /// 关联资源类型。
    /// </summary>
    public string ResourceType { get; set; } = ModelInvocationResourceTypes.None;

    /// <summary>
    /// 关联资源ID。
    /// </summary>
    public string? ResourceId { get; set; }

    /// <summary>
    /// 会话ID。
    /// </summary>
    public Guid? SessionId { get; set; }

    /// <summary>
    /// 消息ID。
    /// </summary>
    public Guid? MessageId { get; set; }

    /// <summary>
    /// TraceId。
    /// </summary>
    public string? TraceId { get; set; }

    /// <summary>
    /// 父调用ID。
    /// </summary>
    public Guid? ParentInvocationId { get; set; }

    /// <summary>
    /// 根调用ID。
    /// </summary>
    public Guid? RootInvocationId { get; set; }

    /// <summary>
    /// 模型名称。
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// 提供商类型。
    /// </summary>
    public string ProviderType { get; set; } = ModelInvocationProviderTypes.Llm;

    /// <summary>
    /// 提供商名称。
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// 提供商数据库主键ID。
    /// </summary>
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>
    /// 计量类型。
    /// </summary>
    public string MeteringType { get; set; } = ModelInvocationMeteringTypes.Token;

    /// <summary>
    /// 输入 Token 数。
    /// </summary>
    public int? InputTokens { get; set; }

    /// <summary>
    /// 输出 Token 数。
    /// </summary>
    public int? OutputTokens { get; set; }

    /// <summary>
    /// 总 Token 数。
    /// </summary>
    public int? TotalTokens { get; set; }

    /// <summary>
    /// 通用计量值。
    /// </summary>
    public decimal? MeteringValue { get; set; }

    /// <summary>
    /// 成本。
    /// </summary>
    public decimal? Cost { get; set; }

    /// <summary>
    /// 使用量来源。
    /// </summary>
    public string UsageSource { get; set; } = ModelInvocationUsageSources.None;

    /// <summary>
    /// 执行状态。
    /// </summary>
    public string Status { get; set; } = ModelInvocationStatuses.Succeeded;

    /// <summary>
    /// 错误代码。
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// 错误消息。
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 开始时间。
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 完成时间。
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// 耗时（毫秒）。
    /// </summary>
    public long DurationMs { get; set; }
}
