using DevNexus.Core.Services;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevNexus.ApiService.Controllers;

/// <summary>
/// 发布中心管理控制器。
/// </summary>
[ApiController]
[Route("api/v1/admin/releases")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class AdminReleasesController : ControllerBase
{
    private readonly IUpdateReleaseManagementService _releaseManagementService;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public AdminReleasesController(IUpdateReleaseManagementService releaseManagementService)
    {
        _releaseManagementService = releaseManagementService;
    }

    /// <summary>
    /// 获取发布版本列表。
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ReleaseDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ReleaseDto>>>> GetReleasesAsync(
        CancellationToken cancellationToken = default)
    {
        var releases = await _releaseManagementService.GetReleasesAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ReleaseDto>>.Success(releases));
    }

    /// <summary>
    /// 创建或更新发布版本。
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ReleaseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ReleaseDto>>> SaveReleaseAsync(
        [FromBody] SaveReleaseRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var release = await _releaseManagementService.SaveReleaseAsync(request, cancellationToken);
            return Ok(ApiResponse<ReleaseDto>.Success(release));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Error(ex.Message, StatusCodes.Status400BadRequest));
        }
    }

    /// <summary>
    /// 导入发布元数据。
    /// </summary>
    [HttpPost("import-metadata")]
    [ProducesResponseType(typeof(ApiResponse<ImportReleaseMetadataResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ImportReleaseMetadataResult>>> ImportMetadataAsync(
        [FromBody] ImportReleaseMetadataRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _releaseManagementService.ImportMetadataAsync(request, cancellationToken);
            return Ok(ApiResponse<ImportReleaseMetadataResult>.Success(result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Error(ex.Message, StatusCodes.Status400BadRequest));
        }
    }

    /// <summary>
    /// 发布指定版本。
    /// </summary>
    [HttpPost("{releaseId:guid}/publish")]
    [ProducesResponseType(typeof(ApiResponse<ReleaseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ReleaseDto>>> PublishReleaseAsync(
        Guid releaseId,
        CancellationToken cancellationToken = default)
    {
        var release = await _releaseManagementService.PublishReleaseAsync(releaseId, cancellationToken);
        return Ok(ApiResponse<ReleaseDto>.Success(release));
    }

    /// <summary>
    /// 归档指定版本。
    /// </summary>
    [HttpPost("{releaseId:guid}/archive")]
    [ProducesResponseType(typeof(ApiResponse<ReleaseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ReleaseDto>>> ArchiveReleaseAsync(
        Guid releaseId,
        CancellationToken cancellationToken = default)
    {
        var release = await _releaseManagementService.ArchiveReleaseAsync(releaseId, cancellationToken);
        return Ok(ApiResponse<ReleaseDto>.Success(release));
    }

    /// <summary>
    /// 删除指定版本。
    /// </summary>
    [HttpDelete("{releaseId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> DeleteReleaseAsync(
        Guid releaseId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _releaseManagementService.DeleteReleaseAsync(releaseId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Error(ex.Message, StatusCodes.Status400BadRequest));
        }
    }
}
