using DevNexus.Domain.Abstractions;
using DevNexus.ApiService.Services;
using DevNexus.Infrastructure.Services.Parsing;
using DevNexus.Shared.DTOs;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;

namespace DevNexus.ApiService.Controllers;

/// <summary>
/// Artifact 控制器，提供文档资产 CRUD API 端点
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class ArtifactController : AuthenticatedControllerBase
{
    private readonly IArtifactService _artifactService;
    private readonly ILogger<ArtifactController> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="artifactService">Artifact 服务</param>
    /// <param name="documentParser">文档解析器</param>
    /// <param name="userContextAccessor">用户上下文访问器</param>
    /// <param name="logger">日志服务</param>
    public ArtifactController(
        IArtifactService artifactService,
        ISmartDocumentParser documentParser,
        IUserContextAccessor userContextAccessor,
        ILogger<ArtifactController> logger)
        : base(userContextAccessor)
    {
        _artifactService = artifactService;
        _logger = logger;
    }

    /// <summary>
    /// 创建 Artifact
    /// </summary>
    /// <param name="request">创建请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>创建的 Artifact</returns>
    [HttpPost]
    public async Task<IActionResult> CreateArtifact(
        [FromBody] CreateArtifactRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireCurrentUserId();

        var artifact = new ArtifactDto
        {
            ArtifactId = Guid.NewGuid(),
            SemanticId = request.SemanticId,
            Version = request.Version,
            ParentArtifactId = request.ParentArtifactId,
            Type = request.Type,
            Name = request.Name,
            Content = request.Content,
            FileAssetId = request.FileAssetId,
            FileVersionId = request.FileVersionId,
            SessionId = request.SessionId,
            MessageId = request.MessageId ?? Guid.Empty,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Metadata = request.Metadata
        };

        var created = await _artifactService.CreateArtifactAsync(artifact, cancellationToken);

        _logger.LogInformation(
            "[Artifact.API] Artifact created | Id={Id} SessionId={SessionId}",
            created.ArtifactId,
            request.SessionId);

        return CreatedAtAction(nameof(GetArtifact), new { artifactId = created.ArtifactId }, created);
    }

    /// <summary>
    /// 获取指定 Artifact
    /// </summary>
    /// <param name="artifactId">Artifact ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>Artifact 信息</returns>
    [HttpGet("{artifactId:guid}")]
    public async Task<IActionResult> GetArtifact(
        Guid artifactId,
        CancellationToken cancellationToken = default)
    {
        var artifact = await _artifactService.GetArtifactAsync(artifactId, cancellationToken);

        if (artifact == null)
        {
            return NotFound(new { error = "Artifact 不存在" });
        }

        return Ok(artifact);
    }

    /// <summary>
    /// 获取会话的所有 Artifacts
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>Artifact 列表</returns>
    [HttpGet("session/{sessionId:guid}")]
    public async Task<IActionResult> GetSessionArtifacts(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var artifacts = await _artifactService.GetSessionArtifactsAsync(sessionId, cancellationToken);

        return Ok(artifacts);
    }

    /// <summary>
    /// 获取消息的所有 Artifacts
    /// </summary>
    /// <param name="messageId">消息 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>Artifact 列表</returns>
    [HttpGet("message/{messageId:guid}")]
    public async Task<IActionResult> GetMessageArtifacts(
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        var artifacts = await _artifactService.GetMessageArtifactsAsync(messageId, cancellationToken);

        return Ok(artifacts);
    }

    /// <summary>
    /// 更新 Artifact 内容
    /// </summary>
    /// <param name="artifactId">Artifact ID</param>
    /// <param name="request">更新请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新后的 Artifact</returns>
    [HttpPut("{artifactId:guid}")]
    public async Task<IActionResult> UpdateArtifact(
        Guid artifactId,
        [FromBody] UpdateArtifactRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var updated = await _artifactService.UpdateArtifactAsync(
                artifactId,
                request.Content,
                cancellationToken);

            _logger.LogInformation(
                "[Artifact.API] Artifact updated | Id={Id}",
                artifactId);

            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// 删除 Artifact
    /// </summary>
    /// <param name="artifactId">Artifact ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>删除结果</returns>
    [HttpDelete("{artifactId:guid}")]
    public async Task<IActionResult> DeleteArtifact(
        Guid artifactId,
        CancellationToken cancellationToken = default)
    {
        var deleted = await _artifactService.DeleteArtifactAsync(artifactId, cancellationToken);

        if (!deleted)
        {
            return NotFound(new { error = "Artifact 不存在" });
        }

        _logger.LogInformation(
            "[Artifact.API] Artifact deleted | Id={Id}",
            artifactId);

        return NoContent();
    }

    /// <summary>
    /// 解析文档 (异步)
    /// 触发后台任务进行解析，立即返回 TraceId
    /// </summary>
    [HttpPost("parse")]
    [RequestSizeLimit(20 * 1024 * 1024)] // 20MB 限制
    public async Task<IActionResult> ParseDocument(
        [FromBody] ParseDocumentRequest request,
        [FromServices] IBackgroundJobClient backgroundJobClient,
        [FromServices] IFileStorageService storageService,
        [FromServices] FileMimeValidationService fileMimeValidationService,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var traceId = Guid.NewGuid().ToString("N");
            var userId = RequireCurrentUserId();
            var sessionId = request.SessionId;

            _logger.LogInformation(
                "[Artifact.API] 接收到解析请求 | TraceId={TraceId} UserId={UserId} FileName={FileName}",
                traceId,
                userId,
                request.FileName);

            string fileUrl;

            // 1. 优先使用已上传文件引用，否则回退到 Base64 上传
            if (!string.IsNullOrWhiteSpace(request.FileUrl))
            {
                fileUrl = request.FileUrl;
            }
            else
            {
                try
                {
                    var fileBytes = Convert.FromBase64String(request.Base64Content);
                    var mimeValidation = fileMimeValidationService.Validate(request.FileName, request.MimeType, fileBytes);
                    if (!mimeValidation.IsValid)
                    {
                        _logger.LogWarning(
                            "[Artifact.API] Parse rejected by MIME validation | FileName={FileName} MimeType={MimeType} Error={Error}",
                            request.FileName,
                            request.MimeType,
                            mimeValidation.ErrorMessage);
                        return BadRequest(new ParseDocumentResponse
                        {
                            Success = false,
                            ErrorMessage = mimeValidation.ErrorMessage
                        });
                    }

                    using var ms = new MemoryStream(fileBytes);

                    // Upload to Storage
                    // Use a dedicated folder for uploads
                    var objectKey = $"uploads/{userId}/{traceId}/{request.FileName}";
                    fileUrl = await storageService.UploadFileAsync(
                        ms,
                        objectKey,
                        mimeValidation.EffectiveMimeType,
                        cancellationToken);
                }
                catch (FormatException)
                {
                    return BadRequest(new ParseDocumentResponse
                    {
                        Success = false,
                        ErrorMessage = "Base64 内容格式无效"
                    });
                }
            }

            // 2. Enqueue Job
            var options = new ParsingOptions
            {
                ProviderId = request.ProviderId,
                SessionId = sessionId,
                DeclaredMimeType = request.MimeType
            };

            // 使用 Hangfire 触发后台任务 (传递 UserId 和 FileUrl)
            backgroundJobClient.Enqueue<Infrastructure.Services.Jobs.DocumentParsingJob>(job =>
                job.ExecuteAsync(traceId, request.FileName, fileUrl, userId.ToString(), options, sessionId, CancellationToken.None));

            // 3. 立即返回 202 Accepted
            return Accepted(new ParseDocumentResponse
            {
                Success = true,
                TraceId = traceId,
                SmartDocument = null,
                ErrorMessage = null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Artifact.API] 解析请求提交失败 | FileName={FileName}", request.FileName);
            return StatusCode(500, new ParseDocumentResponse
            {
                Success = false,
                ErrorMessage = $"任务提交失败: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// 查询异步解析状态（SignalR 丢事件时用于前端兜底轮询）。
    /// </summary>
    [HttpGet("parse-status/{traceId}")]
    public async Task<IActionResult> GetParseStatus(
        string traceId,
        [FromServices] IDistributedCache cache,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(traceId))
        {
            return BadRequest(new { error = "traceId 不能为空" });
        }

        var userId = RequireCurrentUserId().ToString();
        var cacheKey = ArtifactStatusPublisher.BuildCacheKey(userId, traceId);
        var payload = await cache.GetStringAsync(cacheKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(payload))
        {
            return NotFound(new { error = "状态不存在或已过期" });
        }

        try
        {
            var status = JsonSerializer.Deserialize<ArtifactStatusDto>(payload);
            return status == null
                ? NotFound(new { error = "状态不存在或已过期" })
                : Ok(status);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "[Artifact.API] Failed to deserialize parse status | TraceId={TraceId}", traceId);
            return StatusCode(500, new { error = "状态数据损坏" });
        }
    }
}

/// <summary>
/// 创建 Artifact 请求
/// </summary>
public class CreateArtifactRequest
{
    /// <summary>
    /// 语义标识符（由 LLM 指定，用于引用和增量更新）
    /// </summary>
    [MaxLength(100)]
    public string? SemanticId { get; set; }

    /// <summary>
    /// 版本号
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// 父 Artifact ID（用于版本链）
    /// </summary>
    public Guid? ParentArtifactId { get; set; }

    /// <summary>
    /// Artifact 类型（如 Markdown, CSharp, Html 等）
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Type { get; set; } = "Markdown";

    /// <summary>
    /// Artifact 名称
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Artifact 内容
    /// </summary>
    [Required]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 关联的文件资产 ID
    /// </summary>
    public Guid? FileAssetId { get; set; }

    /// <summary>
    /// 关联的文件版本 ID
    /// </summary>
    public Guid? FileVersionId { get; set; }

    /// <summary>
    /// 关联的会话 ID
    /// </summary>
    public Guid? SessionId { get; set; }

    /// <summary>
    /// 关联的消息 ID
    /// </summary>
    public Guid? MessageId { get; set; }

    /// <summary>
    /// Artifact 元数据
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// 更新 Artifact 请求
/// </summary>
public class UpdateArtifactRequest
{
    /// <summary>
    /// 新内容
    /// </summary>
    [Required]
    public string Content { get; set; } = string.Empty;
}
