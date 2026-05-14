using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using DevNexus.Domain.Abstractions;
using DevNexus.Core.Services;
using DevNexus.Shared.Enums;

namespace DevNexus.Infrastructure.Services.Plugins;

/// <summary>
/// 集成管理插件 (Semantic Kernel Plugin)
/// 支持根据集成类型获取用户集成的基本信息及解密后的凭证
/// </summary>
public class IntegrationPlugin : SessionContextPluginBase
{
    private readonly IUserIntegrationService _integrationService;
    private readonly ILogger<IntegrationPlugin> _logger;

    public IntegrationPlugin(
        IUserIntegrationService integrationService,
        ILogger<IntegrationPlugin> logger)
        : base(logger)
    {
        _integrationService = integrationService;
        _logger = logger;
    }

    /// <summary>
    /// 设置上下文 (每次请求前由框架调用)
    /// </summary>
    public void SetContext(Guid sessionId, Guid userId)
    {
        SetSessionContext(sessionId, userId, nameof(IntegrationPlugin));
    }

    /// <summary>
    /// 根据集成类型获取用户的默认集成信息（包含解密凭证）
    /// </summary>
    [KernelFunction, Description("根据集成类型获取当前用户的默认集成配置和凭证信息。")]
    public async Task<string> GetDefaultIntegrationAsync(
        [Description("集成类型 (2: CodeRepository/代码仓库, 3: CloudStorage/云存储, 4: ProjectManagement/项目管理, 5: Communication/通讯工具, 6: Calendar/日历服务, 7: Email/邮件服务, 8: CICD/持续集成部署, 9: Monitoring/监控服务, 99: Custom/其他)")] int type)
    {
        try
        {
            var userId = GetSessionUserId();
            var integrationType = (IntegrationType)type;

            _logger.LogDebug("[IntegrationPlugin] GetDefaultIntegrationAsync called | Type={Type}, UserId={UserId}", 
                integrationType, userId);

            if (userId == Guid.Empty)
            {
                return JsonSerializer.Serialize(new { success = false, error = "用户身份未识别，请确保在会话上下文中运行。" });
            }

            var integration = await _integrationService.GetDefaultIntegrationAsync(userId, integrationType);
            if (integration == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"未找到类型为 {integrationType} 的默认集成配置。" });
            }

            // 获取解密后的凭证
            var credential = await _integrationService.GetDecryptedCredentialAsync(userId, integration.Id);

            // 该插件会把可用凭证直接交给后续工具链继续访问外部系统，属于一次真实集成使用。
            await _integrationService.RecordUsageAsync(userId, integration.Id);

            return JsonSerializer.Serialize(new
            {
                success = true,
                integrationId = integration.Id,
                displayName = integration.DisplayName,
                providerName = integration.ProviderName,
                endpoint = integration.Endpoint,
                authType = integration.AuthType.ToString(),
                credentialStatus = CredentialRuntimeStatusResolver.Resolve(integration).ToString(),
                tokenExpiresAt = integration.TokenExpiresAt,
                lastCredentialRefreshAt = integration.LastCredentialRefreshAt,
                cooldownUntil = integration.CooldownUntil,
                consecutiveAuthFailureCount = integration.ConsecutiveAuthFailureCount,
                validationError = integration.ValidationError,
                credential = credential, // 解密后的 Token/Key
                configuration = integration.Configuration
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[IntegrationPlugin] Error getting default integration");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 获取用户的所有激活集成列表
    /// </summary>
    [KernelFunction, Description("获取当前用户所有已激活的外部系统集成列表。")]
    public async Task<string> GetAllIntegrationsAsync()
    {
        try
        {
            var userId = GetSessionUserId();
            _logger.LogDebug("[IntegrationPlugin] GetAllIntegrationsAsync called | UserId={UserId}", userId);

            if (userId == Guid.Empty)
            {
                return JsonSerializer.Serialize(new { success = false, error = "用户身份未识别。" });
            }

            var integrations = await _integrationService.GetUserIntegrationsAsync(userId, includeInactive: false);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                count = integrations.Count(),
                integrations = integrations.Select(i => new
                {
                    id = i.Id,
                    type = i.IntegrationType.ToString(),
                    displayName = i.DisplayName,
                    providerName = i.ProviderName,
                    isDefault = i.IsDefault,
                    lastValidatedAt = i.LastValidatedAt,
                    credentialStatus = CredentialRuntimeStatusResolver.Resolve(i).ToString(),
                    tokenExpiresAt = i.TokenExpiresAt,
                    cooldownUntil = i.CooldownUntil,
                    consecutiveAuthFailureCount = i.ConsecutiveAuthFailureCount
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[IntegrationPlugin] Error listing integrations");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }
}
