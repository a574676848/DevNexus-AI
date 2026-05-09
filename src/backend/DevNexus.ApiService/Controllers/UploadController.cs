using DevNexus.Domain.Abstractions;
using DevNexus.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevNexus.ApiService.Controllers;

/// <summary>
/// 上传控制器
/// </summary>
[ApiController]
[Route("api/v1/uploads")]
[Authorize]
public class UploadController : AuthenticatedControllerBase
{
    private readonly IUploadSessionService _uploadSessionService;
    private readonly ILogger<UploadController> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public UploadController(
        IUploadSessionService uploadSessionService,
        IUserContextAccessor userContextAccessor,
        ILogger<UploadController> logger)
        : base(userContextAccessor)
    {
        _uploadSessionService = uploadSessionService;
        _logger = logger;
    }

    /// <summary>
    /// 创建上传会话
    /// </summary>
    [HttpPost("sessions")]
    public async Task<IActionResult> CreateUploadSession(
        [FromBody] CreateUploadSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = RequireCurrentUserId();
            var response = await _uploadSessionService.CreateUploadSessionAsync(userId, request, cancellationToken);
            return CreatedAtAction(nameof(GetUploadSession), new { uploadSessionId = response.UploadSession.UploadSessionId }, response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "[Files.Upload.API] Failed to create upload session");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// 获取上传会话
    /// </summary>
    [HttpGet("sessions/{uploadSessionId:guid}")]
    public async Task<IActionResult> GetUploadSession(
        Guid uploadSessionId,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireCurrentUserId();
        var uploadSession = await _uploadSessionService.GetUploadSessionAsync(userId, uploadSessionId, cancellationToken);

        if (uploadSession == null)
        {
            return NotFound(new { error = "上传会话不存在" });
        }

        return Ok(uploadSession);
    }

    /// <summary>
    /// 完成上传
    /// </summary>
    [HttpPost("sessions/finalize")]
    public async Task<IActionResult> FinalizeUpload(
        [FromBody] FinalizeUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = RequireCurrentUserId();
            var response = await _uploadSessionService.FinalizeUploadAsync(userId, request, cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "[Files.Upload.API] Failed to finalize upload session");
            return BadRequest(new { error = ex.Message });
        }
    }
}
