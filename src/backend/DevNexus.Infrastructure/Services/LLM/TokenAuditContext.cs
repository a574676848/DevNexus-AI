using System;
using DevNexus.Shared.DTOs;

namespace DevNexus.Infrastructure.Services.LLM;

/// <summary>
/// 模型调用审计上下文
/// 使用 AsyncLocal 在异步调用链中传递主体、场景和资源信息
/// </summary>
public sealed class TokenAuditContext
{
    /// <summary>
    /// 调用主体类型
    /// </summary>
    public string OwnerType { get; init; } = DevNexus.Shared.DTOs.ModelInvocationOwnerTypes.System;

    /// <summary>
    /// 调用主体用户 ID
    /// </summary>
    public Guid? OwnerUserId { get; init; }

    /// <summary>
    /// 会话 ID
    /// </summary>
    public Guid? SessionId { get; init; }

    /// <summary>
    /// 消息 ID
    /// </summary>
    public Guid? MessageId { get; init; }

    /// <summary>
    /// 调用技术类型
    /// </summary>
    public string InvocationKind { get; init; } = DevNexus.Shared.DTOs.ModelInvocationKinds.Other;

    /// <summary>
    /// 场景编码
    /// </summary>
    public string SceneCode { get; init; } = DevNexus.Shared.DTOs.ModelInvocationSceneCodes.SystemOther;

    /// <summary>
    /// 场景分组
    /// </summary>
    public string SceneCategory { get; init; } = DevNexus.Shared.DTOs.ModelInvocationSceneCategories.Other;

    /// <summary>
    /// 关联资源类型
    /// </summary>
    public string ResourceType { get; init; } = DevNexus.Shared.DTOs.ModelInvocationResourceTypes.None;

    /// <summary>
    /// 关联资源 ID
    /// </summary>
    public string? ResourceId { get; init; }

    /// <summary>
    /// TraceId
    /// </summary>
    public string? TraceId { get; init; }

    /// <summary>
    /// 父调用 ID
    /// </summary>
    public Guid? ParentInvocationId { get; init; }

    /// <summary>
    /// 根调用 ID
    /// </summary>
    public Guid? RootInvocationId { get; init; }

    /// <summary>
    /// 模型名称
    /// </summary>
    public string ModelName { get; init; } = string.Empty;
    
    /// <summary>
    /// 提供商类型（llm / embedding）
    /// </summary>
    public string ProviderType { get; init; } = ModelInvocationProviderTypes.Llm;

    /// <summary>
    /// 提供商名称
    /// </summary>
    public string ProviderName { get; init; } = string.Empty;

    /// <summary>
    /// 提供商 ID（供应商类型标识，如 openai-compatible, minimax 等）
    /// </summary>
    public string ProviderId { get; init; } = string.Empty;

    /// <summary>
    /// LLM 提供商数据库主键 ID
    /// </summary>
    public Guid LLMProviderId { get; init; }

    /// <summary>
    /// Provider 返回的缓存命中输入 Token 数。
    /// </summary>
    public int? CachedPromptTokens { get; init; }

    /// <summary>
    /// 稳定 Prompt 前缀内容指纹。
    /// </summary>
    public string? StablePrefixHash { get; init; }

    /// <summary>
    /// 工具 Schema 与排序指纹。
    /// </summary>
    public string? ToolSchemaHash { get; init; }

    /// <summary>
    /// 动态上下文 Token 数。
    /// </summary>
    public int? DynamicContextTokens { get; init; }

    /// <summary>
    /// 历史消息 Token 数。
    /// </summary>
    public int? HistoryTokens { get; init; }
    
    private static readonly AsyncLocal<TokenAuditContext?> _current = new();
    
    /// <summary>
    /// 获取或设置当前异步上下文中的 TokenAuditContext
    /// </summary>
    public static TokenAuditContext? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}
