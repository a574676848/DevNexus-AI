using DevNexus.Domain.Entities;
using DevNexus.Shared.Enums;

namespace DevNexus.Core.Services;

/// <summary>
/// 凭证运行时状态解析器。
/// </summary>
public static class CredentialRuntimeStatusResolver
{
    private static readonly TimeSpan ExpiringSoonThreshold = TimeSpan.FromMinutes(10);

    /// <summary>
    /// 根据用户集成配置解析当前凭证运行时状态。
    /// </summary>
    public static CredentialRuntimeStatus Resolve(UserIntegration integration, DateTime? now = null)
    {
        ArgumentNullException.ThrowIfNull(integration);

        if (!integration.IsActive)
        {
            return CredentialRuntimeStatus.Inactive;
        }

        var utcNow = now ?? DateTime.UtcNow;
        if (integration.CooldownUntil.HasValue && integration.CooldownUntil.Value > utcNow)
        {
            return CredentialRuntimeStatus.CoolingDown;
        }

        if (integration.ValidationStatus == ValidationStatus.Invalid)
        {
            return CredentialRuntimeStatus.Invalid;
        }
        if (integration.TokenExpiresAt.HasValue)
        {
            if (integration.TokenExpiresAt.Value <= utcNow)
            {
                return CredentialRuntimeStatus.Expired;
            }

            if (integration.TokenExpiresAt.Value <= utcNow.Add(ExpiringSoonThreshold))
            {
                return CredentialRuntimeStatus.ExpiringSoon;
            }
        }

        return integration.ValidationStatus == ValidationStatus.Valid
            ? CredentialRuntimeStatus.Ready
            : CredentialRuntimeStatus.Unknown;
    }
}
