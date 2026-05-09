using DevNexus.Shared.DTOs;

namespace DevNexus.Core.Abstractions;

public interface ITokenAuditService
{
    void RecordUsage(ModelInvocationAuditRecord record);

    void RecordStreamingCompletion(
        Guid? sessionId,
        Guid? messageId,
        Guid? userId,
        string modelId,
        string providerName,
        string providerType,
        string providerId,
        int inputTokens,
        int outputTokens,
        long durationMs,
        string invocationKind = ModelInvocationKinds.ChatCompletion,
        string sceneCode = ModelInvocationSceneCodes.ChatMessageReply,
        string sceneCategory = ModelInvocationSceneCategories.UserFacing,
        string resourceType = ModelInvocationResourceTypes.Message,
        string? resourceId = null,
        string usageSource = ModelInvocationUsageSources.Actual,
        string status = ModelInvocationStatuses.Succeeded,
        string? errorCode = null,
        string? errorMessage = null,
        string meteringType = ModelInvocationMeteringTypes.Token,
        decimal? meteringValue = null);
}
