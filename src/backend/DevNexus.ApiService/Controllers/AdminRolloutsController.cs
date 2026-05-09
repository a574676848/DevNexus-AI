using DevNexus.Core.Services;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevNexus.ApiService.Controllers;

/// <summary>
/// 投放中心管理控制器。
/// </summary>
[ApiController]
[Route("api/v1/admin/rollouts")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class AdminRolloutsController : ControllerBase
{
    private readonly IUpdateRolloutManagementService _rolloutManagementService;
    private readonly IUpdateManifestService _manifestService;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public AdminRolloutsController(
        IUpdateRolloutManagementService rolloutManagementService,
        IUpdateManifestService manifestService)
    {
        _rolloutManagementService = rolloutManagementService;
        _manifestService = manifestService;
    }

    /// <summary>
    /// 获取投放规则列表。
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<RolloutDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RolloutDto>>>> GetRolloutsAsync(
        CancellationToken cancellationToken = default)
    {
        var rollouts = await _rolloutManagementService.GetRolloutsAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<RolloutDto>>.Success(rollouts));
    }

    /// <summary>
    /// 创建或更新投放规则。
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<RolloutDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<RolloutDto>>> SaveRolloutAsync(
        [FromBody] SaveRolloutRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var rollout = await _rolloutManagementService.SaveRolloutAsync(request, cancellationToken);
            return Ok(ApiResponse<RolloutDto>.Success(rollout));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Error(ex.Message, StatusCodes.Status400BadRequest));
        }
    }

    /// <summary>
    /// 暂停投放。
    /// </summary>
    [HttpPost("{rolloutId:guid}/pause")]
    [ProducesResponseType(typeof(ApiResponse<RolloutDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<RolloutDto>>> PauseAsync(
        Guid rolloutId,
        CancellationToken cancellationToken = default)
    {
        var rollout = await _rolloutManagementService.PauseAsync(rolloutId, cancellationToken);
        return Ok(ApiResponse<RolloutDto>.Success(rollout));
    }

    /// <summary>
    /// 恢复投放。
    /// </summary>
    [HttpPost("{rolloutId:guid}/resume")]
    [ProducesResponseType(typeof(ApiResponse<RolloutDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<RolloutDto>>> ResumeAsync(
        Guid rolloutId,
        CancellationToken cancellationToken = default)
    {
        var rollout = await _rolloutManagementService.ResumeAsync(rolloutId, cancellationToken);
        return Ok(ApiResponse<RolloutDto>.Success(rollout));
    }

    /// <summary>
    /// 回滚投放。
    /// </summary>
    [HttpPost("{rolloutId:guid}/rollback")]
    [ProducesResponseType(typeof(ApiResponse<RolloutDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<RolloutDto>>> RollbackAsync(
        Guid rolloutId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var rollout = await _rolloutManagementService.RollbackAsync(rolloutId, cancellationToken);
            return Ok(ApiResponse<RolloutDto>.Success(rollout));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Error(ex.Message, StatusCodes.Status400BadRequest));
        }
    }

    /// <summary>
    /// 删除投放规则。
    /// </summary>
    [HttpDelete("{rolloutId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> DeleteAsync(
        Guid rolloutId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _rolloutManagementService.DeleteAsync(rolloutId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Error(ex.Message, StatusCodes.Status400BadRequest));
        }
    }

    /// <summary>
    /// 预演客户端命中结果。
    /// </summary>
    [HttpPost("preview")]
    [ProducesResponseType(typeof(ApiResponse<UpdateManifestResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<UpdateManifestResponse>>> PreviewAsync(
        [FromBody] UpdateManifestRequest request,
        CancellationToken cancellationToken = default)
    {
        var preview = await _manifestService.GetManifestAsync(request, cancellationToken);
        return Ok(ApiResponse<UpdateManifestResponse>.Success(preview));
    }
}
