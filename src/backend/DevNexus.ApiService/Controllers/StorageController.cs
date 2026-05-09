using DevNexus.Domain.Abstractions;
using DevNexus.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevNexus.ApiService.Controllers;

/// <summary>
/// 文件存储控制器
/// 支持 S3 直传（生产环境）和本地存储（开发环境）
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class StorageController : ControllerBase
{
    private readonly IFileStorageService _storageService;
    private readonly ILogger<StorageController> _logger;

    public StorageController(
        IFileStorageService storageService,
        ILogger<StorageController> logger)
    {
        _storageService = storageService;
        _logger = logger;
    }

    /// <summary>
    /// 获取预签名上传 URL（用于客户端直传或服务端上传）
    /// </summary>
    /// <param name="request">上传请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>预签名 URL 信息</returns>
    [HttpPost("presigned-upload-url")]
    [ProducesResponseType(typeof(PresignedUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PresignedUploadResponse>> GetPresignedUploadUrl(
        [FromBody] PresignedUploadRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.FileName))
            {
                return BadRequest("File name is required");
            }

            // 生成预签名上传信息
            var uploadInfo = await _storageService.GeneratePresignedUploadAsync(
                request.FileName,
                request.ContentType,
                request.Folder,
                cancellationToken);

            var response = new PresignedUploadResponse
            {
                UploadUrl = uploadInfo.UploadUrl,
                FileUrl = uploadInfo.FileUrl,
                ObjectKey = uploadInfo.ObjectKey,
                ExpiresAt = uploadInfo.ExpiresAt,
                UploadMethod = uploadInfo.UploadMethod
            };

            _logger.LogInformation(
                "[Storage.PresignedUrl] Generated | Provider={Provider} FileName={FileName} ObjectKey={ObjectKey} Method={Method}",
                _storageService.Provider,
                request.FileName,
                uploadInfo.ObjectKey,
                uploadInfo.UploadMethod);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Storage.PresignedUrl] Failed | FileName={FileName}", request.FileName);
            return StatusCode(500, "Failed to generate presigned URL");
        }
    }

    /// <summary>
    /// 上传文件（仅开发环境本地存储模式使用）
    /// S3 模式下客户端直接上传到 S3，不经过此端点
    /// </summary>
    /// <param name="file">上传的文件</param>
    /// <param name="objectKey">对象键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>上传结果</returns>
    [HttpPost("upload")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [RequestSizeLimit(100 * 1024 * 1024)] // 100MB
    public async Task<IActionResult> UploadFile(
        IFormFile file,
        [FromQuery] string objectKey,
        CancellationToken cancellationToken)
    {
        try
        {
            // 仅本地存储模式支持此端点
            // S3 模式下应使用 presigned-upload-url 端点获取预签名 URL 后直传
            if (_storageService.Provider != "Local")
            {
                return BadRequest("This endpoint is only available in Local storage mode. Use presigned URL for S3.");
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded");
            }

            if (string.IsNullOrWhiteSpace(objectKey))
            {
                return BadRequest("Object key is required");
            }

            await using var stream = file.OpenReadStream();
            var fileUrl = await _storageService.UploadFileAsync(
                stream,
                objectKey,
                file.ContentType,
                cancellationToken);

            _logger.LogInformation(
                "[Storage.Upload] File uploaded | ObjectKey={ObjectKey} Size={Size}",
                objectKey,
                file.Length);

            return Ok(new { objectKey, fileUrl, fileSize = file.Length, confirmed = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Storage.Upload] Failed | ObjectKey={ObjectKey}", objectKey);
            return StatusCode(500, "Failed to upload file");
        }
    }

    /// <summary>
    /// 确认文件上传完成
    /// </summary>
    /// <param name="objectKey">对象键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>确认结果</returns>
    [HttpPost("confirm-upload")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConfirmUpload(
        [FromQuery] string objectKey,
        CancellationToken cancellationToken)
    {
        try
        {
            // 根据 Provider 构造文件 URL
            var uploadInfo = await _storageService.GeneratePresignedUploadAsync(
                "temp.txt", // 临时文件名，仅用于获取 URL 格式
                "text/plain",
                null,
                cancellationToken);

            // 从 objectKey 构造实际的文件 URL
            var baseUrl = uploadInfo.FileUrl.Substring(0, uploadInfo.FileUrl.LastIndexOf('/'));
            baseUrl = baseUrl.Substring(0, baseUrl.LastIndexOf('/') + 1); // 回退到根目录
            var fileUrl = baseUrl.TrimEnd('/') + "/" + objectKey.Replace("//", "/");

            // 检查文件是否存在
            var exists = await _storageService.FileExistsAsync(fileUrl, cancellationToken);

            if (!exists)
            {
                return NotFound("File not found");
            }

            // 获取文件大小
            var fileSize = await _storageService.GetFileSizeAsync(fileUrl, cancellationToken);

            _logger.LogInformation(
                "[Storage.Confirm] Upload confirmed | ObjectKey={ObjectKey} Size={Size}",
                objectKey,
                fileSize);

            return Ok(new { objectKey, fileUrl, fileSize, confirmed = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Storage.Confirm] Failed | ObjectKey={ObjectKey}", objectKey);
            return StatusCode(500, "Failed to confirm upload");
        }
    }

    /// <summary>
    /// 获取存储服务信息
    /// </summary>
    /// <returns>存储服务信息</returns>
    [HttpGet("info")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetStorageInfo()
    {
        return Ok(new
        {
            provider = _storageService.Provider,
            uploadMethod = _storageService.Provider == "S3" ? "Direct" : "Server",
            description = _storageService.Provider == "S3"
                ? "Files are uploaded directly to S3 using presigned URLs"
                : "Files are uploaded through the server (development mode)"
        });
    }
}
