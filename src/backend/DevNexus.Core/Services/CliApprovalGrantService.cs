using DevNexus.Core.Abstractions;
using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Shared.Enums;

namespace DevNexus.Core.Services;

/// <summary>
/// CLI 审批授权服务实现。
/// </summary>
public sealed class CliApprovalGrantService : ICliApprovalGrantService
{
    private readonly ICliApprovalGrantRepository _repository;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public CliApprovalGrantService(ICliApprovalGrantRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task<bool> IsApprovedAsync(
        string sessionId,
        string commandFingerprint,
        string commandPattern,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(commandFingerprint))
        {
            var onceGrant = await _repository.GetActiveGrantAsync(
                NormalizeSessionScopeKey(sessionId),
                CliApprovalGrantScope.Once,
                NormalizeMatchValue(commandFingerprint),
                cancellationToken);
            if (onceGrant != null)
            {
                onceGrant.ConsumedAt = DateTime.UtcNow;
                onceGrant.UpdatedAt = DateTime.UtcNow;
                await _repository.UpdateAsync(onceGrant, cancellationToken);
                return true;
            }
        }

        if (!string.IsNullOrWhiteSpace(commandPattern))
        {
            var patternGrant = await _repository.GetActiveGrantAsync(
                NormalizeSessionScopeKey(sessionId),
                CliApprovalGrantScope.Pattern,
                NormalizeMatchValue(commandPattern),
                cancellationToken);
            if (patternGrant != null)
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public async Task GrantOnceAsync(
        Guid? userId,
        Guid? chatSessionId,
        string sessionId,
        string commandFingerprint,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(commandFingerprint))
        {
            return;
        }

        await _repository.AddAsync(
            new CliApprovalGrant
            {
                UserId = userId,
                ChatSessionId = chatSessionId,
                SessionScopeKey = NormalizeSessionScopeKey(sessionId),
                Scope = CliApprovalGrantScope.Once,
                MatchValue = NormalizeMatchValue(commandFingerprint),
                ApprovedAt = DateTime.UtcNow
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task GrantPatternAsync(
        Guid? userId,
        Guid? chatSessionId,
        string sessionId,
        string commandPattern,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(commandPattern))
        {
            return;
        }

        var normalizedSessionScopeKey = NormalizeSessionScopeKey(sessionId);
        var normalizedPattern = NormalizeMatchValue(commandPattern);
        var existing = await _repository.GetActiveGrantAsync(
            normalizedSessionScopeKey,
            CliApprovalGrantScope.Pattern,
            normalizedPattern,
            cancellationToken);
        if (existing != null)
        {
            return;
        }

        await _repository.AddAsync(
            new CliApprovalGrant
            {
                UserId = userId,
                ChatSessionId = chatSessionId,
                SessionScopeKey = normalizedSessionScopeKey,
                Scope = CliApprovalGrantScope.Pattern,
                MatchValue = normalizedPattern,
                ApprovedAt = DateTime.UtcNow
            },
            cancellationToken);
    }

    private static string NormalizeSessionScopeKey(string sessionId)
    {
        return sessionId.Trim().ToLowerInvariant();
    }

    private static string NormalizeMatchValue(string value)
    {
        return value.Trim().ToLowerInvariant();
    }
}
