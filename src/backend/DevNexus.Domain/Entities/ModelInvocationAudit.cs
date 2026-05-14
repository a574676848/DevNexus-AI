using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DevNexus.Domain.Entities.Base;
using DevNexus.Shared.DTOs;

namespace DevNexus.Domain.Entities;

/// <summary>
/// 模型调用审计实体。
/// </summary>
[Table("ModelInvocationAudits")]
public class ModelInvocationAudit : AuditableEntity
{
    [Required]
    [MaxLength(32)]
    public string OwnerType { get; set; } = "system";

    public Guid? OwnerUserId { get; set; }

    /// <summary>
    /// 用户ID镜像字段。
    /// </summary>
    public Guid? UserId { get; set; }

    [Required]
    [MaxLength(64)]
    public string InvocationKind { get; set; } = "other";

    [Required]
    [MaxLength(128)]
    public string SceneCode { get; set; } = "system.other";

    [Required]
    [MaxLength(64)]
    public string SceneCategory { get; set; } = "other";

    [Required]
    [MaxLength(64)]
    public string ResourceType { get; set; } = "none";

    [MaxLength(128)]
    public string? ResourceId { get; set; }

    public Guid? SessionId { get; set; }

    public Guid? MessageId { get; set; }

    [MaxLength(128)]
    public string? TraceId { get; set; }

    public Guid? ParentInvocationId { get; set; }

    public Guid? RootInvocationId { get; set; }

    [Required]
    [MaxLength(100)]
    public string ModelId { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string ProviderType { get; set; } = ModelInvocationProviderTypes.Llm;

    [Required]
    [MaxLength(100)]
    public string ProviderName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string ProviderId { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string MeteringType { get; set; } = "token";

    public int? InputTokens { get; set; }

    public int? OutputTokens { get; set; }

    public int? TotalTokens { get; set; }

    /// <summary>
    /// Provider 返回的缓存命中输入 Token 数。
    /// </summary>
    public int? CachedPromptTokens { get; set; }

    /// <summary>
    /// 稳定 Prompt 前缀内容指纹。
    /// </summary>
    [MaxLength(128)]
    public string? StablePrefixHash { get; set; }

    /// <summary>
    /// 工具 Schema 与排序指纹。
    /// </summary>
    [MaxLength(128)]
    public string? ToolSchemaHash { get; set; }

    /// <summary>
    /// 动态上下文 Token 数。
    /// </summary>
    public int? DynamicContextTokens { get; set; }

    /// <summary>
    /// 历史消息 Token 数。
    /// </summary>
    public int? HistoryTokens { get; set; }

    /// <summary>
    /// 工具名称，格式通常为 Plugin.Function。
    /// </summary>
    [MaxLength(256)]
    public string? ToolName { get; set; }

    /// <summary>
    /// 工具参数是否通过预验证。
    /// </summary>
    public bool? ToolArgumentsValid { get; set; }

    /// <summary>
    /// 工具失败原因。
    /// </summary>
    [MaxLength(64)]
    public string? ToolFailureReason { get; set; }

    /// <summary>
    /// 工具建议动作。
    /// </summary>
    [MaxLength(64)]
    public string? ToolSuggestedAction { get; set; }

    /// <summary>
    /// 工具失败后是否允许重试。
    /// </summary>
    public bool? ToolRetryable { get; set; }

    /// <summary>
    /// 工具执行是否需要人工介入。
    /// </summary>
    public bool? ToolRequiresHumanIntervention { get; set; }

    /// <summary>
    /// 工具退出码。
    /// </summary>
    public int? ToolExitCode { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal? MeteringValue { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal? Cost { get; set; }

    /// <summary>
    /// 请求类型镜像字段。
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string RequestType { get; set; } = "other";

    [Required]
    [MaxLength(32)]
    public string UsageSource { get; set; } = "none";

    [Required]
    [MaxLength(32)]
    public string Status { get; set; } = "succeeded";

    /// <summary>
    /// 成功标记镜像字段。
    /// </summary>
    public bool IsSuccess { get; set; } = true;

    [MaxLength(100)]
    public string? ErrorCode { get; set; }

    [MaxLength(4000)]
    public string? ErrorMessage { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public long DurationMs { get; set; }
}
