using DevNexus.Domain.Abstractions;
using DevNexus.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevNexus.ApiService.Controllers;

/// <summary>
/// 文件任务控制器
/// </summary>
[ApiController]
[Route("api/v1/file-tasks")]
[Authorize]
public class FileTasksController : AuthenticatedControllerBase
{
    private readonly IFileTaskService _fileTaskService;
    private readonly ILogger<FileTasksController> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public FileTasksController(
        IFileTaskService fileTaskService,
        IUserContextAccessor userContextAccessor,
        ILogger<FileTasksController> logger)
        : base(userContextAccessor)
    {
        _fileTaskService = fileTaskService;
        _logger = logger;
    }

    /// <summary>
    /// 判定是否应创建文件任务
    /// </summary>
    [HttpPost("intents/decide")]
    public async Task<IActionResult> DecideFileTaskIntent(
        [FromBody] FileTaskIntentDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = RequireCurrentUserId();
            var decision = await _fileTaskService.DecideFileTaskIntentAsync(userId, request, cancellationToken);
            return Ok(decision);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "[Files.Task.API] Failed to decide file task intent");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// 创建文件任务
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateFileTask(
        [FromBody] CreateFileTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = RequireCurrentUserId();
            var task = await _fileTaskService.CreateFileTaskAsync(userId, request, cancellationToken);
            return CreatedAtAction(nameof(GetFileTask), new { fileTaskId = task.FileTaskId }, task);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "[Files.Task.API] Failed to create file task");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// 获取单个文件任务
    /// </summary>
    [HttpGet("{fileTaskId:guid}")]
    public async Task<IActionResult> GetFileTask(
        Guid fileTaskId,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireCurrentUserId();
        var task = await _fileTaskService.GetFileTaskAsync(userId, fileTaskId, cancellationToken);

        if (task == null)
        {
            return NotFound(new { error = "文件任务不存在" });
        }

        return Ok(task);
    }

    /// <summary>
    /// 获取会话文件任务列表
    /// </summary>
    [HttpGet("session/{sessionId:guid}")]
    public async Task<IActionResult> GetSessionFileTasks(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireCurrentUserId();
        var tasks = await _fileTaskService.GetSessionFileTasksAsync(userId, sessionId, cancellationToken);
        return Ok(tasks);
    }

    /// <summary>
    /// 重试文件任务
    /// </summary>
    [HttpPost("{fileTaskId:guid}/retry")]
    public async Task<IActionResult> RetryFileTask(
        Guid fileTaskId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = RequireCurrentUserId();
            var task = await _fileTaskService.RetryFileTaskAsync(userId, fileTaskId, cancellationToken);
            return Ok(task);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "[Files.Task.API] Failed to retry file task. FileTaskId={FileTaskId}", fileTaskId);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// 取消文件任务
    /// </summary>
    [HttpPost("{fileTaskId:guid}/cancel")]
    public async Task<IActionResult> CancelFileTask(
        Guid fileTaskId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = RequireCurrentUserId();
            var task = await _fileTaskService.CancelFileTaskAsync(userId, fileTaskId, cancellationToken);
            return Ok(task);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "[Files.Task.API] Failed to cancel file task. FileTaskId={FileTaskId}", fileTaskId);
            return BadRequest(new { error = ex.Message });
        }
    }
}
