using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Core.Models.Evaluation;
using DevNexus.Shared.Enums;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 挂起交互服务接口。
/// </summary>
public interface IPendingInteractionService
{
    /// <summary>
    /// 基于工具执行记录创建或复用挂起交互。
    /// </summary>
    Task<PendingInteraction> CreateOrReuseAsync(
        Guid sessionId,
        Guid? messageId,
        ToolExecutionRecord toolRecord,
        string? evaluationFeedback,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 解决指定挂起交互。
    /// </summary>
    Task<PendingInteraction> ResolveAsync(
        Guid? userId,
        Guid sessionId,
        Guid interactionId,
        string action,
        IReadOnlyDictionary<string, string?> values,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 挂起交互服务。
/// </summary>
internal sealed class PendingInteractionService : IPendingInteractionService
{
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(30);

    private readonly IPendingInteractionRepository _repository;
    private readonly ICliApprovalGrantService _cliApprovalGrantService;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public PendingInteractionService(
        IPendingInteractionRepository repository,
        ICliApprovalGrantService cliApprovalGrantService)
    {
        _repository = repository;
        _cliApprovalGrantService = cliApprovalGrantService;
    }

    /// <inheritdoc />
    public async Task<PendingInteraction> CreateOrReuseAsync(
        Guid sessionId,
        Guid? messageId,
        ToolExecutionRecord toolRecord,
        string? evaluationFeedback,
        CancellationToken cancellationToken = default)
    {
        var definition = PendingInteractionDefinitionBuilder.Build(toolRecord, evaluationFeedback);
        var activeInteractions = await _repository.GetActiveBySessionIdAsync(sessionId, cancellationToken);
        var existing = activeInteractions.FirstOrDefault(interaction =>
            interaction.Kind == definition.Kind
            && string.Equals(interaction.SourceTool, toolRecord.ToolName, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            return existing;
        }

        var interaction = new PendingInteraction
        {
            SessionId = sessionId,
            MessageId = messageId,
            Kind = definition.Kind,
            Status = PendingInteractionStatus.Pending,
            Title = definition.Title,
            Description = definition.Description,
            SourceTool = toolRecord.ToolName,
            SuggestedAction = toolRecord.SuggestedAction,
            RequestedData = definition.RequestedData,
            ExpiresAt = DateTime.UtcNow.Add(DefaultExpiration),
            RetryToken = Guid.NewGuid().ToString("N")
        };

        await _repository.AddAsync(interaction, cancellationToken);
        return interaction;
    }

    /// <inheritdoc />
    public async Task<PendingInteraction> ResolveAsync(
        Guid? userId,
        Guid sessionId,
        Guid interactionId,
        string action,
        IReadOnlyDictionary<string, string?> values,
        CancellationToken cancellationToken = default)
    {
        var interaction = await _repository.GetByIdAsync(interactionId, cancellationToken)
            ?? throw new InvalidOperationException("挂起交互不存在。");

        if (interaction.SessionId != sessionId)
        {
            throw new InvalidOperationException("挂起交互与当前会话不匹配。");
        }

        if (interaction.Status != PendingInteractionStatus.Pending)
        {
            return interaction;
        }

        var normalizedAction = NormalizeResolutionAction(action);
        await ApplyCliApprovalGrantAsync(userId, interaction, normalizedAction, cancellationToken);
        interaction.Status = normalizedAction == "deny"
            ? PendingInteractionStatus.Cancelled
            : PendingInteractionStatus.Resolved;
        interaction.ResolutionData = values.ToDictionary(
            pair => pair.Key,
            pair => (object)(pair.Value ?? string.Empty),
            StringComparer.OrdinalIgnoreCase);
        interaction.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(interaction, cancellationToken);
        return interaction;
    }

    private static string NormalizeResolutionAction(string? action)
    {
        return action?.Trim().ToLowerInvariant() switch
        {
            "approve" => "approve-once",
            "approve-once" => "approve-once",
            "approve-pattern" => "approve-pattern",
            "deny" => "deny",
            _ => "submit"
        };
    }

    private async Task ApplyCliApprovalGrantAsync(
        Guid? userId,
        PendingInteraction interaction,
        string normalizedAction,
        CancellationToken cancellationToken)
    {
        if (interaction.Kind != PendingInteractionKind.Approval || normalizedAction == "deny")
        {
            return;
        }

        if (interaction.RequestedData == null
            || !interaction.RequestedData.TryGetValue("approval", out var approvalObj)
            || approvalObj is not Dictionary<string, object> approvalData)
        {
            return;
        }

        var sessionId = interaction.SessionId.ToString("N");
        var fingerprint = approvalData.TryGetValue("commandFingerprint", out var fingerprintValue)
            ? fingerprintValue?.ToString()
            : null;
        var pattern = approvalData.TryGetValue("commandPattern", out var patternValue)
            ? patternValue?.ToString()
            : null;

        if (normalizedAction == "approve-pattern")
        {
            await _cliApprovalGrantService.GrantPatternAsync(
                userId,
                interaction.SessionId,
                sessionId,
                pattern ?? string.Empty,
                cancellationToken);
            return;
        }

        await _cliApprovalGrantService.GrantOnceAsync(
            userId,
            interaction.SessionId,
            sessionId,
            fingerprint ?? string.Empty,
            cancellationToken);
    }
}
