using DevNexus.Shared.DTOs;

namespace DevNexus.Infrastructure.Services.LLM;

public partial class KernelService
{
    /// <inheritdoc />
    public async Task<T> RunWithAuditScopeAsync<T>(ModelInvocationScopeDto scope, Func<Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(action);

        var previousContext = TokenAuditContext.Current;
        TokenAuditContext.Current = BuildAuditContext(previousContext, scope, ModelInvocationKinds.Other);

        try
        {
            return await action();
        }
        finally
        {
            TokenAuditContext.Current = previousContext;
        }
    }

    /// <inheritdoc />
    public async Task RunWithAuditScopeAsync(ModelInvocationScopeDto scope, Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(action);

        var previousContext = TokenAuditContext.Current;
        TokenAuditContext.Current = BuildAuditContext(previousContext, scope, ModelInvocationKinds.Other);

        try
        {
            await action();
        }
        finally
        {
            TokenAuditContext.Current = previousContext;
        }
    }

    private static TokenAuditContext BuildAuditContext(
        TokenAuditContext? previousContext,
        ModelInvocationScopeDto? scope,
        string invocationKind)
    {
        return new TokenAuditContext
        {
            OwnerType = scope?.OwnerType ?? previousContext?.OwnerType ?? ModelInvocationOwnerTypes.System,
            OwnerUserId = scope?.OwnerUserId ?? previousContext?.OwnerUserId,
            SessionId = scope?.SessionId ?? previousContext?.SessionId,
            MessageId = scope?.MessageId ?? previousContext?.MessageId,
            InvocationKind = invocationKind,
            SceneCode = scope?.SceneCode ?? previousContext?.SceneCode ?? ModelInvocationSceneCodes.SystemOther,
            SceneCategory = scope?.SceneCategory ?? previousContext?.SceneCategory ?? ModelInvocationSceneCategories.Other,
            ResourceType = scope?.ResourceType ?? previousContext?.ResourceType ?? ModelInvocationResourceTypes.None,
            ResourceId = scope?.ResourceId ?? previousContext?.ResourceId,
            TraceId = previousContext?.TraceId,
            ParentInvocationId = previousContext?.ParentInvocationId,
            RootInvocationId = previousContext?.RootInvocationId,
            ModelName = previousContext?.ModelName ?? string.Empty,
            ProviderType = previousContext?.ProviderType ?? ModelInvocationProviderTypes.Llm,
            ProviderName = previousContext?.ProviderName ?? string.Empty,
            ProviderId = previousContext?.ProviderId ?? string.Empty,
            LLMProviderId = previousContext?.LLMProviderId ?? Guid.Empty,
            PromptCacheKey = previousContext?.PromptCacheKey,
            StablePrefixHash = previousContext?.StablePrefixHash,
            ToolSchemaHash = previousContext?.ToolSchemaHash,
            DynamicContextTokens = previousContext?.DynamicContextTokens,
            HistoryTokens = previousContext?.HistoryTokens,
            CacheMarkerCandidateCount = previousContext?.CacheMarkerCandidateCount,
            CacheDoubleMarkerReady = previousContext?.CacheDoubleMarkerReady,
            CacheMarkerReadinessReason = previousContext?.CacheMarkerReadinessReason,
            StablePrefixManifest = previousContext?.StablePrefixManifest
                ?? Array.Empty<PromptFragmentManifestItemDto>(),
            DynamicContextManifest = previousContext?.DynamicContextManifest
                ?? Array.Empty<PromptFragmentManifestItemDto>()
        };
    }

    private static TokenAuditContext AttachProviderInfo(
        TokenAuditContext context,
        string modelName,
        string providerName,
        string providerId,
        Guid llmProviderId,
        PromptOptimizationMetadataDto? promptMetadata = null)
    {
        return new TokenAuditContext
        {
            OwnerType = context.OwnerType,
            OwnerUserId = context.OwnerUserId,
            SessionId = context.SessionId,
            MessageId = context.MessageId,
            InvocationKind = context.InvocationKind,
            SceneCode = context.SceneCode,
            SceneCategory = context.SceneCategory,
            ResourceType = context.ResourceType,
            ResourceId = context.ResourceId,
            TraceId = context.TraceId,
            ParentInvocationId = context.ParentInvocationId,
            RootInvocationId = context.RootInvocationId,
            ModelName = modelName,
            ProviderType = context.ProviderType,
            ProviderName = providerName,
            ProviderId = providerId,
            LLMProviderId = llmProviderId,
            PromptCacheKey = promptMetadata?.PromptCacheKey ?? context.PromptCacheKey,
            StablePrefixHash = promptMetadata?.StablePrefixHash ?? context.StablePrefixHash,
            ToolSchemaHash = promptMetadata?.ToolSchemaHash ?? context.ToolSchemaHash,
            DynamicContextTokens = promptMetadata?.DynamicContextTokens ?? context.DynamicContextTokens,
            HistoryTokens = promptMetadata?.HistoryTokens ?? context.HistoryTokens,
            CacheMarkerCandidateCount = promptMetadata?.CacheMarkerCandidateCount ?? context.CacheMarkerCandidateCount,
            CacheDoubleMarkerReady = promptMetadata?.CacheDoubleMarkerReady ?? context.CacheDoubleMarkerReady,
            CacheMarkerReadinessReason = promptMetadata?.CacheMarkerReadinessReason ?? context.CacheMarkerReadinessReason,
            StablePrefixManifest = promptMetadata?.StablePrefixManifest.Count > 0
                ? promptMetadata.StablePrefixManifest
                : context.StablePrefixManifest,
            DynamicContextManifest = promptMetadata?.DynamicContextManifest.Count > 0
                ? promptMetadata.DynamicContextManifest
                : context.DynamicContextManifest
        };
    }
}
