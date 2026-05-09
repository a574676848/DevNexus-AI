using DevNexus.Core.Services;
using DevNexus.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevNexus.ApiService.Controllers;

/// <summary>
/// 客户端更新 Manifest 控制器。
/// </summary>
[ApiController]
[Route("api/update")]
public class UpdateManifestController : ControllerBase
{
    private readonly IUpdateManifestService _updateManifestService;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public UpdateManifestController(IUpdateManifestService updateManifestService)
    {
        _updateManifestService = updateManifestService;
    }

    /// <summary>
    /// 解析客户端更新 manifest。
    /// </summary>
    /// <param name="request">客户端更新决策请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>更新 manifest。</returns>
    [HttpPost("manifest")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(UpdateManifestResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<UpdateManifestResponse>> GetManifestAsync(
        [FromBody] UpdateManifestRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _updateManifestService.GetManifestAsync(request, cancellationToken);
        return Ok(response);
    }
}
