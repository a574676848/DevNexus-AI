using DevNexus.Domain.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevNexus.ApiService.Controllers;

/// <summary>
/// 文件资产控制器
/// </summary>
[ApiController]
[Route("api/v1/file-assets")]
[Authorize]
public class FileAssetsController : AuthenticatedControllerBase
{
    private readonly IFileAssetService _fileAssetService;

    /// <summary>
    /// 构造函数
    /// </summary>
    public FileAssetsController(IFileAssetService fileAssetService, IUserContextAccessor userContextAccessor)
        : base(userContextAccessor)
    {
        _fileAssetService = fileAssetService;
    }

    /// <summary>
    /// 获取单个文件资产
    /// </summary>
    [HttpGet("{fileAssetId:guid}")]
    public async Task<IActionResult> GetFileAsset(
        Guid fileAssetId,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireCurrentUserId();
        var asset = await _fileAssetService.GetFileAssetAsync(userId, fileAssetId, cancellationToken);

        if (asset == null)
        {
            return NotFound(new { error = "文件资产不存在" });
        }

        return Ok(asset);
    }

    /// <summary>
    /// 获取会话文件资产列表
    /// </summary>
    [HttpGet("session/{sessionId:guid}")]
    public async Task<IActionResult> GetSessionFileAssets(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireCurrentUserId();
        var assets = await _fileAssetService.GetSessionFileAssetsAsync(userId, sessionId, cancellationToken);
        return Ok(assets);
    }

    /// <summary>
    /// 批量获取文件资产
    /// </summary>
    [HttpPost("batch")]
    public async Task<IActionResult> GetFileAssetsByIds(
        [FromBody] List<Guid> fileAssetIds,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireCurrentUserId();
        var assets = await _fileAssetService.GetFileAssetsByIdsAsync(
            userId,
            fileAssetIds ?? new List<Guid>(),
            cancellationToken);
        return Ok(assets);
    }
}
