using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DevNexus.Domain.Abstractions;
using DevNexus.Core.Abstractions;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;

namespace DevNexus.ApiService.Controllers;

/// <summary>
/// 笔记操作API (需要用户集成)
/// </summary>
[ApiController]
[Route("api/v1/notes")]
[Authorize]
public class NoteController : AuthenticatedControllerBase
{
    /// <summary>
    /// 笔记服务上下文
    /// </summary>
    private sealed class NoteServiceContext
    {
        /// <summary>
        /// 用户 ID
        /// </summary>
        public Guid UserId { get; init; }

        /// <summary>
        /// 集成 ID
        /// </summary>
        public Guid IntegrationId { get; init; }

        /// <summary>
        /// 笔记服务实例
        /// </summary>
        public required INoteService NoteService { get; init; }
    }

    private readonly ILogger<NoteController> _logger;
    private readonly IUserIntegrationService _integrationService;
    private readonly INoteProviderManagementService _noteProviderManagementService;
    private readonly INoteServiceFactory _noteServiceFactory;

    public NoteController(
        ILogger<NoteController> logger,
        IUserIntegrationService integrationService,
        INoteProviderManagementService noteProviderManagementService,
        INoteServiceFactory noteServiceFactory,
        IUserContextAccessor userContextAccessor)
        : base(userContextAccessor)
    {
        _logger = logger;
        _integrationService = integrationService;
        _noteProviderManagementService = noteProviderManagementService;
        _noteServiceFactory = noteServiceFactory;
    }

    /// <summary>
    /// 获取用户的笔记服务实例
    /// </summary>
    private async Task<NoteServiceContext> GetNoteServiceContextAsync(Guid userId)
    {
        // 获取用户的默认笔记集成
        var integration = await _integrationService.GetDefaultIntegrationAsync(userId, IntegrationType.NoteSystem);
        if (integration == null)
        {
            throw new InvalidOperationException("未配置笔记系统集成，请在个人设置中添加");
        }

        // 检查集成是否关联了Provider
        if (!integration.ProviderId.HasValue)
        {
            throw new InvalidOperationException("笔记集成配置不完整，请重新配置");
        }

        // 获取解密后的凭证
        var accessToken = await _integrationService.GetDecryptedCredentialAsync(userId, integration.Id);
        if (string.IsNullOrEmpty(accessToken))
        {
            throw new InvalidOperationException("无法获取笔记系统凭证，请检查配置");
        }

        // 获取Provider实体
        var provider = await _noteProviderManagementService.GetProviderByIdAsync(integration.ProviderId.Value);
        if (provider == null)
        {
            throw new InvalidOperationException("笔记系统供应商不存在，请联系管理员");
        }

        return new NoteServiceContext
        {
            UserId = userId,
            IntegrationId = integration.Id,
            NoteService = _noteServiceFactory.CreateNoteService(provider, accessToken)
        };
    }

    /// <summary>
    /// 在笔记调用成功后记录集成使用情况
    /// </summary>
    private async Task<T> ExecuteWithUsageTrackingAsync<T>(
        NoteServiceContext context,
        Func<INoteService, Task<T>> action,
        CancellationToken cancellationToken)
    {
        var result = await action(context.NoteService);

        // 只有真实调用成功后才累计使用次数，避免读取配置也被统计为一次使用。
        await _integrationService.RecordUsageAsync(context.UserId, context.IntegrationId, cancellationToken);
        return result;
    }

    /// <summary>
    /// 在笔记调用成功后记录集成使用情况
    /// </summary>
    private async Task ExecuteWithUsageTrackingAsync(
        NoteServiceContext context,
        Func<INoteService, Task> action,
        CancellationToken cancellationToken)
    {
        await action(context.NoteService);

        // 只有真实调用成功后才累计使用次数，避免读取配置也被统计为一次使用。
        await _integrationService.RecordUsageAsync(context.UserId, context.IntegrationId, cancellationToken);
    }

    /// <summary>
    /// 搜索笔记
    /// </summary>
    [HttpPost("search")]
    [ProducesResponseType(typeof(SearchNotesResponse), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> SearchNotes(
        [FromBody] SearchNotesRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = RequireCurrentUserId();
            var context = await GetNoteServiceContextAsync(userId);
            var response = await ExecuteWithUsageTrackingAsync(
                context,
                noteService => noteService.SearchNotesAsync(request, cancellationToken),
                cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to search notes for user");
            return BadRequest(new
            {
                error = ex.Message,
                action = "configure_integration",
                integration_type = "NoteSystem"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching notes");
            return StatusCode(500, new { error = "搜索笔记时发生错误，请稍后重试。" });
        }
    }

    /// <summary>
    /// 创建笔记
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(NoteDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> CreateNote(
        [FromBody] CreateNoteRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = RequireCurrentUserId();
            var context = await GetNoteServiceContextAsync(userId);
            var note = await ExecuteWithUsageTrackingAsync(
                context,
                noteService => noteService.CreateNoteAsync(request, cancellationToken),
                cancellationToken);
            return CreatedAtAction(nameof(GetNote), new { noteId = note.Id }, note);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to create note for user");
            return BadRequest(new
            {
                error = ex.Message,
                action = "configure_integration",
                integration_type = "NoteSystem"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating note");
            return StatusCode(500, new { error = "创建笔记时发生错误，请稍后重试。" });
        }
    }

    /// <summary>
    /// 更新笔记
    /// </summary>
    [HttpPut("{noteId}")]
    [ProducesResponseType(typeof(NoteDto), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> UpdateNote(
        string noteId,
        [FromBody] UpdateNoteRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = RequireCurrentUserId();
            var context = await GetNoteServiceContextAsync(userId);
            var note = await ExecuteWithUsageTrackingAsync(
                context,
                noteService => noteService.UpdateNoteAsync(noteId, request, cancellationToken),
                cancellationToken);
            return Ok(note);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to update note for user");
            return BadRequest(new
            {
                error = ex.Message,
                action = "configure_integration",
                integration_type = "NoteSystem"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating note");
            return StatusCode(500, new { error = "更新笔记时发生错误，请稍后重试。" });
        }
    }

    /// <summary>
    /// 删除笔记
    /// </summary>
    [HttpDelete("{noteId}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> DeleteNote(
        string noteId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = RequireCurrentUserId();
            var context = await GetNoteServiceContextAsync(userId);
            await ExecuteWithUsageTrackingAsync(
                context,
                noteService => noteService.DeleteNoteAsync(noteId, cancellationToken),
                cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to delete note for user");
            return BadRequest(new
            {
                error = ex.Message,
                action = "configure_integration",
                integration_type = "NoteSystem"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting note");
            return StatusCode(500, new { error = "删除笔记时发生错误，请稍后重试。" });
        }
    }

    /// <summary>
    /// 获取单个笔记
    /// </summary>
    [HttpGet("{noteId}")]
    [ProducesResponseType(typeof(NoteDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetNote(
        string noteId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = RequireCurrentUserId();
            var context = await GetNoteServiceContextAsync(userId);
            var note = await ExecuteWithUsageTrackingAsync(
                context,
                noteService => noteService.GetNoteAsync(noteId, cancellationToken),
                cancellationToken);
            if (note == null)
            {
                return NotFound(new { error = "笔记不存在或已被删除。" });
            }

            return Ok(note);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to get note for user");
            return BadRequest(new
            {
                error = ex.Message,
                action = "configure_integration",
                integration_type = "NoteSystem"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting note");
            return StatusCode(500, new { error = "获取笔记时发生错误，请稍后重试。" });
        }
    }
}
