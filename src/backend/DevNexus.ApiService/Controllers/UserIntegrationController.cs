using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DevNexus.Domain.Abstractions;
using DevNexus.Core.Services;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;

namespace DevNexus.ApiService.Controllers;

/// <summary>
/// 用户外部系统集成管理API
/// </summary>
[ApiController]
[Route("api/v1/user/integrations")]
[Authorize]
public class UserIntegrationController : AuthenticatedControllerBase
{
    private readonly IUserIntegrationService _service;
    private readonly ILogger<UserIntegrationController> _logger;

    public UserIntegrationController(
        IUserIntegrationService service,
        ILogger<UserIntegrationController> logger,
        IUserContextAccessor userContextAccessor)
        : base(userContextAccessor)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// 获取当前用户的所有集成
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<UserIntegrationResponse>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetUserIntegrations(
        [FromQuery] IntegrationType? type = null,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireCurrentUserId();
        var integrations = await _service.GetUserIntegrationsAsync(
            userId,
            type,
            includeInactive,
            cancellationToken);

        var response = integrations.Select(MapToResponse);
        return Ok(response);
    }

    /// <summary>
    /// 获取所有用户的集成（管理员专用）
    /// </summary>
    [HttpGet("all")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(IEnumerable<UserIntegrationDetailedResponse>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> GetAllIntegrations(
        [FromQuery] IntegrationType? type = null,
        [FromQuery] bool includeInactive = false,
        [FromQuery] Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _service.GetAllIntegrationDetailsAsync(type, includeInactive, userId, cancellationToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取所有集成失败");
            return StatusCode(500, new { error = "获取集成列表失败" });
        }
    }

    /// <summary>
    /// 获取集成详情
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UserIntegrationResponse), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetIntegrationById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireCurrentUserId();
        var integration = await _service.GetUserIntegrationByIdAsync(
            userId,
            id,
            cancellationToken);

        if (integration == null)
        {
            return NotFound();
        }

        return Ok(MapToResponse(integration));
    }

    /// <summary>
    /// 创建新的集成
    /// 管理员可以为其他用户创建集成（通过 request.UserId）
    /// 普通用户只能为自己创建集成
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(UserIntegrationResponse), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> CreateIntegration(
        [FromBody] CreateUserIntegrationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var currentUserId = RequireCurrentUserId();
            var isAdmin = IsAdmin();
            
            // 确定目标用户ID
            Guid targetUserId;
            bool isAdminCreate = false;
            
            if (request.UserId.HasValue)
            {
                // 如果请求中指定了用户ID，检查是否为管理员
                if (!isAdmin)
                {
                    return Forbid(); // 403 - 非管理员不能为其他用户创建集成
                }
                
                targetUserId = request.UserId.Value;
                isAdminCreate = true;
            }
            else
            {
                // 未指定用户ID，使用当前用户
                targetUserId = currentUserId;
            }
            
            var integration = await _service.CreateIntegrationAsync(
                targetUserId,
                request,
                isAdminCreate,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetIntegrationById),
                new { id = integration.Id },
                MapToResponse(integration));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// 更新集成
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(UserIntegrationResponse), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> UpdateIntegration(
        Guid id,
        [FromBody] UpdateUserIntegrationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = RequireCurrentUserId();
            var integration = await _service.UpdateIntegrationAsync(
                userId,
                id,
                request,
                cancellationToken);

            return Ok(MapToResponse(integration));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// 删除集成
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> DeleteIntegration(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireCurrentUserId();
        var result = await _service.DeleteIntegrationAsync(
            userId,
            id,
            cancellationToken);

        return result ? NoContent() : NotFound();
    }

    /// <summary>
    /// 设置为默认集成
    /// </summary>
    [HttpPost("{id:guid}/set-default")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> SetAsDefault(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireCurrentUserId();
        var result = await _service.SetAsDefaultAsync(
            userId,
            id,
            cancellationToken);

        return result ? NoContent() : NotFound();
    }

    /// <summary>
    /// 验证集成连接
    /// </summary>
    [HttpPost("{id:guid}/validate")]
    [ProducesResponseType(typeof(ValidateUserIntegrationResponse), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> ValidateIntegration(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = RequireCurrentUserId();
            var result = await _service.ValidateIntegrationAsync(
                userId,
                id,
                cancellationToken);

            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// 测试集成连接（创建前测试）
    /// </summary>
    [HttpPost("test")]
    [ProducesResponseType(typeof(ValidateUserIntegrationResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> TestConnection(
        [FromBody] ValidateUserIntegrationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.TestIntegrationAsync(
            request,
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// 获取用户集成统计
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(UserIntegrationStatsResponse), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetIntegrationStats(
        CancellationToken cancellationToken = default)
    {
        var userId = RequireCurrentUserId();
        var stats = await _service.GetUserIntegrationStatsAsync(
            userId,
            cancellationToken);

        return Ok(stats);
    }

    #region Private Methods

    /// <summary>
    /// 映射到响应DTO
    /// </summary>
    private static UserIntegrationResponse MapToResponse(Domain.Entities.UserIntegration integration)
    {
        return new UserIntegrationResponse
        {
            Id = integration.Id,
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
    }

    #endregion
}
