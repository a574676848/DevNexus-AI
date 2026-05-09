using DevNexus.Core.Services;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevNexus.ApiService.Controllers;

/// <summary>
/// 更新观测控制器。
/// </summary>
[ApiController]
[Route("api/v1/admin/update-observability")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class AdminUpdateObservabilityController : ControllerBase
{
    private readonly IUpdateObservabilityService _observabilityService;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public AdminUpdateObservabilityController(IUpdateObservabilityService observabilityService)
    {
        _observabilityService = observabilityService;
    }

    /// <summary>
    /// 获取观测摘要。
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<UpdateObservabilitySummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<UpdateObservabilitySummaryDto>>> GetSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        var summary = await _observabilityService.GetSummaryAsync(cancellationToken);
        return Ok(ApiResponse<UpdateObservabilitySummaryDto>.Success(summary));
    }

    /// <summary>
    /// 获取观测详情。
    /// </summary>
    [HttpGet("details")]
    [ProducesResponseType(typeof(ApiResponse<UpdateObservabilityDetailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<UpdateObservabilityDetailDto>>> GetDetailsAsync(
        [FromQuery] UpdateObservabilityFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        var details = await _observabilityService.GetDetailsAsync(request, cancellationToken);
        return Ok(ApiResponse<UpdateObservabilityDetailDto>.Success(details));
    }
}
