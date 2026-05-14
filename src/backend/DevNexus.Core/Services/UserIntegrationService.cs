using Microsoft.Extensions.Logging;
using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using DevNexus.Core.Abstractions;

namespace DevNexus.Core.Services;

/// <summary>
/// 用户外部系统集成管理服务
/// </summary>
public class UserIntegrationService : IUserIntegrationService
{
    private const int AuthFailureCooldownThreshold = 3;
    private static readonly TimeSpan AuthFailureCooldownDuration = TimeSpan.FromMinutes(10);

    private readonly IUserIntegrationStore _userIntegrationStore;
    private readonly IUserIdentityService _userIdentityService;
    private readonly ILLMProviderManagementService _llmProviderManagementService;
    private readonly IStorageProviderManagementService _storageProviderManagementService;
    private readonly IEncryptionService _encryptionService;
    private readonly IIntegrationValidatorFactory _validatorFactory;
    private readonly ILogger<UserIntegrationService> _logger;

    public UserIntegrationService(
        IUserIntegrationStore userIntegrationStore,
        IUserIdentityService userIdentityService,
        ILLMProviderManagementService llmProviderManagementService,
        IStorageProviderManagementService storageProviderManagementService,
        IEncryptionService encryptionService,
        IIntegrationValidatorFactory validatorFactory,
        ILogger<UserIntegrationService> logger)
    {
        _userIntegrationStore = userIntegrationStore;
        _userIdentityService = userIdentityService;
        _llmProviderManagementService = llmProviderManagementService;
        _storageProviderManagementService = storageProviderManagementService;
        _encryptionService = encryptionService;
        _validatorFactory = validatorFactory;
        _logger = logger;
    }

    /// <summary>
    /// 获取用户的所有集成
    /// </summary>
    public async Task<IEnumerable<UserIntegration>> GetUserIntegrationsAsync(
        Guid userId,
        IntegrationType? type = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _userIntegrationStore.ListByUserAsync(userId, type, includeInactive, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取用户集成列表失败. UserId: {UserId}, Type: {Type}", userId, type);
            throw;
        }
    }

    /// <summary>
    /// 获取所有用户的集成（管理员专用）
    /// </summary>
    public async Task<IEnumerable<UserIntegration>> GetAllIntegrationsAsync(
        IntegrationType? type = null,
        bool includeInactive = false,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _userIntegrationStore.ListAllAsync(type, includeInactive, userId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取所有集成列表失败. Type: {Type}, UserId: {UserId}", type, userId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<UserIntegrationDetailedResponse>> GetAllIntegrationDetailsAsync(
        IntegrationType? type = null,
        bool includeInactive = false,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        var integrations = (await GetAllIntegrationsAsync(type, includeInactive, userId, cancellationToken)).ToList();
        var users = await _userIdentityService.GetUserInfosByIdsAsync(
            integrations.Select(integration => integration.UserId),
            cancellationToken);

        return integrations.Select(integration =>
        {
            users.TryGetValue(integration.UserId, out var userInfo);
            return new UserIntegrationDetailedResponse
            {
                Id = integration.Id,
                UserId = integration.UserId,
                Username = userInfo?.Username ?? "Unknown",
                UserEmail = userInfo?.Email ?? string.Empty,
                IntegrationType = integration.IntegrationType,
                IntegrationTypeName = integration.IntegrationType.ToString(),
                ProviderId = integration.ProviderId,
                ProviderName = integration.ProviderName,
                DisplayName = integration.DisplayName,
                Endpoint = integration.Endpoint,
                AuthType = integration.AuthType,
                IsActive = integration.IsActive,
                IsDefault = integration.IsDefault,
                ValidationStatus = integration.ValidationStatus,
                CredentialRuntimeStatus = CredentialRuntimeStatusResolver.Resolve(integration),
                LastValidatedAt = integration.LastValidatedAt,
                TokenExpiresAt = integration.TokenExpiresAt,
                LastCredentialRefreshAt = integration.LastCredentialRefreshAt,
                ValidationError = integration.ValidationError,
                ConsecutiveAuthFailureCount = integration.ConsecutiveAuthFailureCount,
                LastAuthFailureAt = integration.LastAuthFailureAt,
                CooldownUntil = integration.CooldownUntil,
                LastUsedAt = integration.LastUsedAt,
                UsageCount = integration.UsageCount,
                CreatedAt = integration.CreatedAt,
                UpdatedAt = integration.UpdatedAt
            };
        });
    }

    /// <summary>
    /// 获取用户集成详情
    /// </summary>
    public async Task<UserIntegration?> GetUserIntegrationByIdAsync(
        Guid userId,
        Guid integrationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _userIntegrationStore.GetByIdAsync(userId, integrationId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取用户集成详情失败. UserId: {UserId}, IntegrationId: {IntegrationId}", userId, integrationId);
            throw;
        }
    }

    /// <summary>
    /// 获取用户的默认集成
    /// </summary>
    public async Task<UserIntegration?> GetDefaultIntegrationAsync(
        Guid userId,
        IntegrationType type,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _userIntegrationStore.GetDefaultAsync(userId, type, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取默认集成失败. UserId: {UserId}, Type: {Type}", userId, type);
            throw;
        }
    }

    /// <summary>
    /// 创建用户集成
    /// </summary>
    public async Task<UserIntegration> CreateIntegrationAsync(
        Guid userId,
        CreateUserIntegrationRequest request,
        bool isAdminCreate = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 如果是管理员创建但没有指定用户ID，抛出异常
            if (isAdminCreate && !request.UserId.HasValue)
            {
                throw new InvalidOperationException("管理员创建集成必须指定用户ID");
            }
            
            // 如果不是管理员创建，忽略请求中的UserId
            if (!isAdminCreate && request.UserId.HasValue && request.UserId.Value != userId)
            {
                _logger.LogWarning("非管理员用户 {CurrentUserId} 尝试为其他用户 {TargetUserId} 创建集成", 
                    userId, request.UserId.Value);
                throw new UnauthorizedAccessException("没有权限为其他用户创建集成");
            }
            
            // 如果设置为默认，先取消该类型的其他默认设置
            if (request.IsDefault)
            {
                await ClearDefaultIntegrationsAsync(userId, request.IntegrationType, cancellationToken);
            }

            // 加密凭证
            var encryptedCredential = _encryptionService.Encrypt(request.Credential);
            var encryptedSecondaryCredential = !string.IsNullOrEmpty(request.SecondaryCredential)
                ? _encryptionService.Encrypt(request.SecondaryCredential)
                : null;

            var integration = new UserIntegration
            {
                UserId = userId,
                IntegrationType = request.IntegrationType,
                ProviderId = request.ProviderId,
                ProviderName = request.ProviderId.HasValue ? await GetProviderNameAsync(request.ProviderId.Value, cancellationToken) : string.Empty,
                DisplayName = request.DisplayName,
                Endpoint = request.Endpoint,
                AuthType = request.AuthType,
                Credential = encryptedCredential,
                SecondaryCredential = encryptedSecondaryCredential,
                IsActive = request.IsActive,
                IsDefault = request.IsDefault,
                Configuration = request.Configuration ?? new Dictionary<string, object>(),
                ValidationStatus = ValidationStatus.Unknown
            };

            await _userIntegrationStore.AddAsync(integration, cancellationToken);
            await _userIntegrationStore.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("创建用户集成成功. UserId: {UserId}, IntegrationId: {IntegrationId}, Type: {Type}, IsAdminCreate: {IsAdminCreate}",
                userId, integration.Id, integration.IntegrationType, isAdminCreate);

            return integration;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建用户集成失败. UserId: {UserId}, Type: {Type}", userId, request.IntegrationType);
            throw;
        }
    }

    /// <summary>
    /// 更新用户集成
    /// </summary>
    public async Task<UserIntegration> UpdateIntegrationAsync(
        Guid userId,
        Guid integrationId,
        UpdateUserIntegrationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var integration = await _userIntegrationStore.GetByIdAsync(userId, integrationId, cancellationToken);

            if (integration == null)
            {
                throw new KeyNotFoundException($"集成不存在: {integrationId}");
            }

            // 如果设置为默认，先取消该类型的其他默认设置
            if (request.IsDefault == true && !integration.IsDefault)
            {
                await ClearDefaultIntegrationsAsync(userId, integration.IntegrationType, cancellationToken);
            }

            // 更新字段
            if (request.DisplayName != null)
                integration.DisplayName = request.DisplayName;

            if (request.Endpoint != null)
                integration.Endpoint = request.Endpoint;

            if (request.Credential != null)
                integration.Credential = _encryptionService.Encrypt(request.Credential);

            if (request.SecondaryCredential != null)
                integration.SecondaryCredential = _encryptionService.Encrypt(request.SecondaryCredential);

            if (request.IsActive.HasValue)
                integration.IsActive = request.IsActive.Value;

            if (request.IsDefault.HasValue)
                integration.IsDefault = request.IsDefault.Value;

            if (request.Configuration != null)
                integration.Configuration = request.Configuration;

            // 如果凭证更新，重置验证状态
            if (request.Credential != null || request.SecondaryCredential != null)
            {
                integration.ValidationStatus = ValidationStatus.Unknown;
                integration.LastValidatedAt = null;
                integration.ValidationError = null;
            }

            integration.UpdatedAt = DateTime.UtcNow;

            await _userIntegrationStore.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "更新用户集成成功. UserId: {UserId}, IntegrationId: {IntegrationId}",
                userId, integrationId);

            return integration;
        }
        catch (KeyNotFoundException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新用户集成失败. UserId: {UserId}, IntegrationId: {IntegrationId}", userId, integrationId);
            throw;
        }
    }

    /// <summary>
    /// 删除用户集成
    /// </summary>
    public async Task<bool> DeleteIntegrationAsync(
        Guid userId,
        Guid integrationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var integration = await _userIntegrationStore.GetByIdAsync(userId, integrationId, cancellationToken);

            if (integration == null)
            {
                return false;
            }

            await _userIntegrationStore.RemoveAsync(integration, cancellationToken);
            await _userIntegrationStore.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "删除用户集成成功. UserId: {UserId}, IntegrationId: {IntegrationId}",
                userId, integrationId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除用户集成失败. UserId: {UserId}, IntegrationId: {IntegrationId}", userId, integrationId);
            throw;
        }
    }

    /// <summary>
    /// 设置为默认集成
    /// </summary>
    public async Task<bool> SetAsDefaultAsync(
        Guid userId,
        Guid integrationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var integration = await _userIntegrationStore.GetByIdAsync(userId, integrationId, cancellationToken);

            if (integration == null)
            {
                return false;
            }

            // 取消该类型的其他默认设置
            await ClearDefaultIntegrationsAsync(userId, integration.IntegrationType, cancellationToken);

            // 设置为默认
            integration.IsDefault = true;
            integration.UpdatedAt = DateTime.UtcNow;

            await _userIntegrationStore.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "设置默认集成成功. UserId: {UserId}, IntegrationId: {IntegrationId}",
                userId, integrationId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "设置默认集成失败. UserId: {UserId}, IntegrationId: {IntegrationId}", userId, integrationId);
            throw;
        }
    }

    /// <summary>
    /// 验证集成连接
    /// </summary>
    public async Task<ValidateUserIntegrationResponse> ValidateIntegrationAsync(
        Guid userId,
        Guid integrationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var integration = await _userIntegrationStore.GetByIdAsync(userId, integrationId, cancellationToken);

            if (integration == null)
            {
                throw new KeyNotFoundException($"集成不存在: {integrationId}");
            }

            // 解密凭证
            var credential = _encryptionService.Decrypt(integration.Credential);
            var secondaryCredential = !string.IsNullOrEmpty(integration.SecondaryCredential)
                ? _encryptionService.Decrypt(integration.SecondaryCredential)
                : null;

            // 执行实际验证
            var validationResult = await PerformValidationAsync(
                integration.IntegrationType,
                integration.Endpoint,
                integration.AuthType,
                credential,
                secondaryCredential,
                cancellationToken);

            // 更新验证状态
            integration.ValidationStatus = validationResult.IsValid ? ValidationStatus.Valid : ValidationStatus.Invalid;
            integration.LastValidatedAt = validationResult.ValidatedAt;
            integration.ValidationError = validationResult.ErrorMessage;
            integration.UpdatedAt = DateTime.UtcNow;
            ApplyCredentialValidationOutcome(integration, validationResult);

            await _userIntegrationStore.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "验证集成连接完成. UserId: {UserId}, IntegrationId: {IntegrationId}, IsValid: {IsValid}",
                userId, integrationId, validationResult.IsValid);

            return validationResult;
        }
        catch (KeyNotFoundException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "验证集成连接失败. UserId: {UserId}, IntegrationId: {IntegrationId}", userId, integrationId);
            throw;
        }
    }

    /// <summary>
    /// 测试集成连接（创建前测试）
    /// </summary>
    public async Task<ValidateUserIntegrationResponse> TestIntegrationAsync(
        ValidateUserIntegrationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var validationResult = await PerformValidationAsync(
                request.IntegrationType,
                request.Endpoint,
                request.AuthType,
                request.Credential,
                request.SecondaryCredential,
                cancellationToken);

            _logger.LogInformation(
                "测试集成连接完成. Type: {Type}, IsValid: {IsValid}",
                request.IntegrationType, validationResult.IsValid);

            return validationResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "测试集成连接失败. Type: {Type}", request.IntegrationType);
            throw;
        }
    }

    /// <summary>
    /// 记录集成使用
    /// </summary>
    public async Task RecordUsageAsync(
        Guid userId,
        Guid integrationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var integration = await _userIntegrationStore.GetByIdAsync(userId, integrationId, cancellationToken);

            if (integration != null)
            {
                integration.LastUsedAt = DateTime.UtcNow;
                integration.UsageCount++;
                integration.UpdatedAt = DateTime.UtcNow;

                await _userIntegrationStore.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "记录集成使用失败. UserId: {UserId}, IntegrationId: {IntegrationId}", userId, integrationId);
            // 不抛出异常，避免影响主流程
        }
    }

    /// <summary>
    /// 获取用户集成统计
    /// </summary>
    public async Task<UserIntegrationStatsResponse> GetUserIntegrationStatsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var integrations = await _userIntegrationStore.ListByUserAsync(userId, null, true, cancellationToken);

            var stats = new UserIntegrationStatsResponse
            {
                TotalIntegrations = integrations.Count,
                ActiveIntegrations = integrations.Count(i => i.IsActive),
                IntegrationsByType = integrations
                    .GroupBy(i => i.IntegrationType)
                    .ToDictionary(g => g.Key, g => g.Count()),
                LastUsedAt = integrations
                    .Where(i => i.LastUsedAt.HasValue)
                    .Max(i => i.LastUsedAt)
            };

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取用户集成统计失败. UserId: {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// 获取解密后的凭证（仅内部服务使用）
    /// </summary>
    public async Task<string> GetDecryptedCredentialAsync(
        Guid userId,
        Guid integrationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var integration = await _userIntegrationStore.GetByIdAsync(userId, integrationId, cancellationToken);

            if (integration == null)
            {
                throw new KeyNotFoundException($"集成不存在: {integrationId}");
            }

            if (integration.CooldownUntil.HasValue && integration.CooldownUntil.Value > DateTime.UtcNow)
            {
                throw new InvalidOperationException(
                    $"当前凭证处于冷却期，请在 {integration.CooldownUntil.Value.ToLocalTime():MM-dd HH:mm} 后重试。");
            }

            return _encryptionService.Decrypt(integration.Credential);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取解密凭证失败. UserId: {UserId}, IntegrationId: {IntegrationId}", userId, integrationId);
            throw;
        }
    }

    #region Private Methods

    /// <summary>
    /// 清除该类型的其他默认设置
    /// </summary>
    private async Task ClearDefaultIntegrationsAsync(
        Guid userId,
        IntegrationType type,
        CancellationToken cancellationToken)
    {
        var defaultIntegrations = await _userIntegrationStore.ListDefaultsByTypeAsync(userId, type, cancellationToken);

        foreach (var integration in defaultIntegrations)
        {
            integration.IsDefault = false;
            integration.UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// 获取供应商名称
    /// </summary>
    private async Task<string> GetProviderNameAsync(Guid providerId, CancellationToken cancellationToken)
    {
        var llmProvider = await _llmProviderManagementService.GetProviderByIdAsync(providerId, cancellationToken);
        if (llmProvider != null)
        {
            return llmProvider.DisplayName;
        }

        var storageProvider = await _storageProviderManagementService.GetProviderByIdAsync(providerId, cancellationToken);
        if (storageProvider != null)
        {
            return storageProvider.DisplayName;
        }

        return string.Empty;
    }

    private static void ApplyCredentialValidationOutcome(
        UserIntegration integration,
        ValidateUserIntegrationResponse validationResult)
    {
        if (validationResult.IsValid)
        {
            integration.ConsecutiveAuthFailureCount = 0;
            integration.LastAuthFailureAt = null;
            integration.CooldownUntil = null;
            integration.LastCredentialRefreshAt = DateTime.UtcNow;
            return;
        }

        if (!IsCredentialFailure(validationResult.ErrorMessage))
        {
            return;
        }

        integration.ConsecutiveAuthFailureCount += 1;
        integration.LastAuthFailureAt = validationResult.ValidatedAt;

        if (integration.ConsecutiveAuthFailureCount >= AuthFailureCooldownThreshold)
        {
            integration.CooldownUntil = validationResult.ValidatedAt.Add(AuthFailureCooldownDuration);
        }
    }

    private static bool IsCredentialFailure(string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            return false;
        }

        var lowered = errorMessage.ToLowerInvariant();
        return lowered.Contains("401", StringComparison.OrdinalIgnoreCase)
               || lowered.Contains("403", StringComparison.OrdinalIgnoreCase)
               || lowered.Contains("unauthorized", StringComparison.OrdinalIgnoreCase)
               || lowered.Contains("forbidden", StringComparison.OrdinalIgnoreCase)
               || lowered.Contains("token", StringComparison.OrdinalIgnoreCase)
               || lowered.Contains("credential", StringComparison.OrdinalIgnoreCase)
               || lowered.Contains("认证", StringComparison.OrdinalIgnoreCase)
               || lowered.Contains("凭证", StringComparison.OrdinalIgnoreCase)
               || lowered.Contains("未授权", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 执行实际的集成验证
    /// </summary>
    private async Task<ValidateUserIntegrationResponse> PerformValidationAsync(
        IntegrationType integrationType,
        string? endpoint,
        IntegrationAuthType authType,
        string credential,
        string? secondaryCredential,
        CancellationToken cancellationToken)
    {
        // 使用验证器工厂执行实际的验证逻辑
        var validator = _validatorFactory.GetValidator(integrationType);
        
        return await validator.ValidateAsync(
            endpoint,
            authType,
            credential,
            secondaryCredential,
            cancellationToken);
    }

    #endregion
}
