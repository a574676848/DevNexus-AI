using DevNexus.Core.Abstractions;
using DevNexus.Core.Services.Chat;
using DevNexus.Domain.Abstractions;
using DevNexus.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace DevNexus.ApiService.Controllers;

/// <summary>
/// 聊天控制器，提供RESTful API端点
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class ChatController : AuthenticatedControllerBase
{
    private readonly IChatService _chatService;
    private readonly IPendingInteractionService _pendingInteractionService;
    private readonly IRuntimeEventNotifier _runtimeEventNotifier;
    private readonly IChatSessionRuntimeService _chatSessionRuntimeService;
    private readonly IElasticsearchSearchService _searchService;
    private readonly IChatQueueService _chatQueueService;
    private readonly ILogger<ChatController> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="chatService">聊天服务</param>
    /// <param name="pendingInteractionService">挂起交互服务</param>
    /// <param name="runtimeEventNotifier">运行时结构化事件通知服务</param>
    /// <param name="chatSessionRuntimeService">会话统一运行态服务</param>
    /// <param name="searchService">搜索服务</param>
    /// <param name="chatQueueService">聊天消息排队服务</param>
    /// <param name="userContextAccessor">用户上下文访问器</param>
    /// <param name="logger">日志服务</param>
    public ChatController(
        IChatService chatService,
        IPendingInteractionService pendingInteractionService,
        IRuntimeEventNotifier runtimeEventNotifier,
        IChatSessionRuntimeService chatSessionRuntimeService,
        IElasticsearchSearchService searchService,
        IChatQueueService chatQueueService,
        IUserContextAccessor userContextAccessor,
        ILogger<ChatController> logger)
        : base(userContextAccessor)
    {
        _chatService = chatService;
        _pendingInteractionService = pendingInteractionService;
        _runtimeEventNotifier = runtimeEventNotifier;
        _chatSessionRuntimeService = chatSessionRuntimeService;
        _searchService = searchService;
        _chatQueueService = chatQueueService;
        _logger = logger;
    }

    /// <summary>
    /// 创建聊天会话
    /// </summary>
    /// <param name="request">创建会话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>创建的会话信息</returns>
    [HttpPost("sessions")]
    public async Task<IActionResult> CreateSession(
        [FromBody] CreateChatSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireCurrentUserId();
        var sessionId = await _chatService.CreateChatSessionAsync(
            userId,
            request.Title,
            cancellationToken);

        // 返回完整的 ChatSessionDto，包含时间戳
        var now = DateTime.UtcNow;
        var session = new ChatSessionDto
        {
            Id = sessionId,
            Title = request.Title,
            CreatedAt = now,
            UpdatedAt = now,
            IsActive = true,
            MessageCount = 0,
            LastMessage = null
        };

        return CreatedAtAction(nameof(GetSession), new { sessionId }, session);
    }

    /// <summary>
    /// 获取聊天会话列表
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>会话列表</returns>
    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions(CancellationToken cancellationToken = default)
    {
        var userId = RequireCurrentUserId();
        var sessions = await _chatService.GetChatSessionsAsync(userId, cancellationToken);

        return Ok(sessions);
    }

    /// <summary>
    /// 获取指定会话
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>会话信息</returns>
    [HttpGet("sessions/{sessionId:guid}")]
    public async Task<IActionResult> GetSession(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireCurrentUserId();
        var session = await _chatService.GetChatSessionAsync(sessionId, userId, cancellationToken);

        if (session == null)
        {
            return NotFound();
        }

        return Ok(session);
    }

    /// <summary>
    /// 获取指定会话的消息列表
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>消息列表</returns>
    [HttpGet("sessions/{sessionId:guid}/messages")]
    public async Task<IActionResult> GetMessages(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireCurrentUserId();
        var session = await _chatService.GetChatSessionAsync(sessionId, userId, cancellationToken);

        if (session == null)
        {
            return NotFound(new { error = "会话不存在或无权访问" });
        }

        var messages = await _chatService.GetChatMessagesAsync(sessionId, cancellationToken);

        return Ok(messages);
    }

    /// <summary>
    /// 获取指定会话当前活跃的终端记录。
    /// </summary>
    [HttpGet("sessions/{sessionId:guid}/active-terminals")]
    public async Task<IActionResult> GetActiveTerminals(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireCurrentUserId();
        var session = await _chatService.GetChatSessionAsync(sessionId, userId, cancellationToken);

        if (session == null)
        {
            return NotFound(new { error = "会话不存在或无权访问" });
        }

        var records = await _chatService.GetActiveTerminalRecordsAsync(sessionId, cancellationToken);
        return Ok(records);
    }

    /// <summary>
    /// 获取指定终端记录的完整输出内容。
    /// </summary>
    [HttpGet("sessions/{sessionId:guid}/terminals/{recordId:guid}/output")]
    public async Task<IActionResult> GetTerminalOutput(
        Guid sessionId,
        Guid recordId,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireCurrentUserId();
        var session = await _chatService.GetChatSessionAsync(sessionId, userId, cancellationToken);

        if (session == null)
        {
            return NotFound(new { error = "会话不存在或无权访问" });
        }

        var output = await _chatService.GetTerminalOutputAsync(sessionId, recordId, cancellationToken);
        if (output == null)
        {
            return NotFound(new { error = "终端记录不存在" });
        }

        return Ok(output);
    }

    /// <summary>
    /// 获取指定会话当前活跃的挂起交互。
    /// </summary>
    [HttpGet("sessions/{sessionId:guid}/pending-interactions")]
    public async Task<IActionResult> GetPendingInteractions(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireCurrentUserId();
        var session = await _chatService.GetChatSessionAsync(sessionId, userId, cancellationToken);

        if (session == null)
        {
            return NotFound(new { error = "会话不存在或无权访问" });
        }

        var interactions = await _chatService.GetActivePendingInteractionsAsync(sessionId, cancellationToken);
        return Ok(interactions);
    }

    /// <summary>
    /// 获取指定会话当前统一运行态。
    /// </summary>
    [HttpGet("sessions/{sessionId:guid}/runtime")]
    public async Task<IActionResult> GetSessionRuntime(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireCurrentUserId();
        var session = await _chatService.GetChatSessionAsync(sessionId, userId, cancellationToken);

        if (session == null)
        {
            return NotFound(new { error = "会话不存在或无权访问" });
        }

        var runtime = await _chatSessionRuntimeService.GetRuntimeAsync(userId, sessionId, cancellationToken);
        return Ok(runtime);
    }

    /// <summary>
    /// 解决指定挂起交互并准备恢复执行。
    /// </summary>
    [HttpPost("sessions/{sessionId:guid}/pending-interactions/{interactionId:guid}/resolve")]
    public async Task<IActionResult> ResolvePendingInteraction(
        Guid sessionId,
        Guid interactionId,
        [FromBody] PendingInteractionResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireCurrentUserId();
        var session = await _chatService.GetChatSessionAsync(sessionId, userId, cancellationToken);

        if (session == null)
        {
            return NotFound(new { error = "会话不存在或无权访问" });
        }

        var interaction = await _pendingInteractionService.ResolveAsync(
            userId,
            sessionId,
            interactionId,
            request.Action,
            request.Values,
            cancellationToken);
        await _runtimeEventNotifier.NotifyAsync(
            userId,
            sessionId,
            interaction.Status == PendingInteractionStatus.Expired
                ? ServerEventType.PendingInteractionExpired
                : ServerEventType.PendingInteractionResolved,
            new
            {
                InteractionId = interaction.Id,
                Status = interaction.Status.ToWireValue(),
                request.Action
            },
            cancellationToken);

        return Ok(new PendingInteractionResolutionResponse
        {
            InteractionId = interaction.Id,
            ShouldResume = interaction.Status == PendingInteractionStatus.Resolved,
            ResumeMessage = interaction.Status == PendingInteractionStatus.Resolved
                ? request.Action switch
                {
                    "approve-pattern" => "我已允许当前会话中的同类命令继续执行，请继续。",
                    "approve" or "approve-once" => "我已允许本次命令执行，请继续。",
                    _ => "我已补充所需信息，请继续。"
                }
                : null
        });
    }



    /// <summary>
    /// 取消消息生成
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>取消结果</returns>
    [HttpPost("sessions/{sessionId:guid}/cancel")]
    public async Task<IActionResult> CancelMessageGeneration(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        await _chatService.CancelMessageGenerationAsync(sessionId, cancellationToken);

        return Ok(new { Message = "消息生成已取消" });
    }

    /// <summary>
    /// 删除会话
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>删除结果</returns>
    [HttpDelete("sessions/{sessionId:guid}")]
    public async Task<IActionResult> DeleteSession(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireCurrentUserId();

        try
        {
            await _chatService.DeleteChatSessionAsync(sessionId, userId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// 删除消息
    /// </summary>
    /// <param name="sessionId">会话ID(用于鉴权校验)</param>
    /// <param name="messageId">消息ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>删除结果</returns>
    [HttpDelete("sessions/{sessionId:guid}/messages/{messageId:guid}")]
    public async Task<IActionResult> DeleteMessage(
        Guid sessionId,
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireCurrentUserId();

        try
        {
            // 虽然 Service 方法只需 messageId 和 userId，但 Controller 路径带 sessionId 是 RESTful 规范
            // 可以先校验 message 是否属于 session，或者信赖 Service 的校验
            await _chatService.DeleteChatMessageAsync(messageId, userId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// 批量删除消息（用于编辑/重新发送场景，提升性能）
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="request">批量删除请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>删除结果</returns>
    [HttpPost("sessions/{sessionId:guid}/messages/batch-delete")]
    public async Task<IActionResult> BatchDeleteMessages(
        Guid sessionId,
        [FromBody] BatchDeleteMessagesRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireCurrentUserId();

        if (request.MessageIds == null || request.MessageIds.Count == 0)
        {
            return BadRequest(new { error = "消息ID列表不能为空" });
        }

        try
        {
            var deletedCount = await _chatService.DeleteChatMessagesAsync(
                sessionId, 
                request.MessageIds, 
                userId, 
                cancellationToken);
            
            return Ok(new { deletedCount, message = $"成功删除 {deletedCount} 条消息" });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// 更新会话信息（标题等）
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="request">更新请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新结果</returns>
    [HttpPatch("sessions/{sessionId:guid}")]
    public async Task<IActionResult> UpdateSession(
        Guid sessionId,
        [FromBody] ChatSessionUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireCurrentUserId();

        try
        {
            var session = await _chatService.UpdateChatSessionAsync(sessionId, userId, request, cancellationToken);
            return Ok(session);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// 使用 LLM 智能生成会话标题
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>生成的智能标题</returns>
    [HttpPost("sessions/{sessionId:guid}/generate-title")]
    public async Task<IActionResult> GenerateSmartTitle(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireCurrentUserId();

        try
        {
            var newTitle = await _chatService.GenerateSmartTitleAsync(sessionId, userId, cancellationToken);
            return Ok(new { title = newTitle });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成智能标题时发生错误");
            return StatusCode(500, new { error = "生成标题失败，请稍后重试" });
        }
    }

    /// <summary>
    /// 搜索会话
    /// </summary>
    /// <param name="query">搜索关键词</param>
    /// <param name="searchContent">是否同时搜索消息内容</param>
    /// <param name="skip">跳过数量</param>
    /// <param name="take">获取数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>搜索结果</returns>
    [HttpGet("sessions/search")]
    public async Task<IActionResult> SearchSessions(
        [FromQuery] string query,
        [FromQuery] bool searchContent = true,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest(new { error = "搜索关键词不能为空" });
        }

        var userId = RequireCurrentUserId();

        try
        {
            // 先检查 Elasticsearch 是否可用
            var esAvailable = await _searchService.IsAvailableAsync(cancellationToken);
            if (!esAvailable)
            {
                _logger.LogWarning("Elasticsearch 服务不可用，回退到数据库搜索");
                // 回退到数据库搜索
                var dbSessions = await _chatService.GetChatSessionsAsync(userId, cancellationToken);
                var filtered = dbSessions
                    .Where(s => s.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                               (s.LastMessage?.Content?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
                    .Skip(skip)
                    .Take(take)
                    .ToList();
                return Ok(new SearchResponseDto
                {
                    Sessions = filtered.Select(s => new SessionSearchDocumentDto
                    {
                        Id = s.Id.ToString(),
                        UserId = userId.ToString(),
                        Title = s.Title,
                        CreatedAt = s.CreatedAt,
                        UpdatedAt = s.UpdatedAt,
                        LastMessagePreview = s.LastMessage?.Content
                    }).ToList(),
                    Total = filtered.Count,
                    Source = "database"
                });
            }

            // 使用 Elasticsearch 搜索
            var result = await _searchService.SearchSessionsAsync(
                userId.ToString(),
                query,
                skip,
                take,
                cancellationToken);

            // 如果需要同时搜索消息内容
            if (searchContent && result.Sessions.Count < take)
            {
                var sessionIds = await _searchService.SearchSessionsByMessageContentAsync(
                    userId.ToString(),
                    query,
                    0,
                    take - result.Sessions.Count,
                    cancellationToken);

                // 合并结果，去重
                var existingIds = result.Sessions.Select(s => s.Id).ToHashSet();
                var additionalSessionIds = sessionIds.Where(id => !existingIds.Contains(id)).ToList();

                if (additionalSessionIds.Count > 0)
                {
                    // 获取额外会话的详细信息
                    var allDbSessions = await _chatService.GetChatSessionsAsync(userId, cancellationToken);
                    var additionalSessions = allDbSessions
                        .Where(s => additionalSessionIds.Contains(s.Id.ToString()))
                        .Select(s => new SessionSearchDocumentDto
                        {
                            Id = s.Id.ToString(),
                            UserId = userId.ToString(),
                            Title = s.Title,
                            CreatedAt = s.CreatedAt,
                            UpdatedAt = s.UpdatedAt,
                            LastMessagePreview = s.LastMessage?.Content,
                            MessageCount = 0
                        })
                        .ToList();

                    result.Sessions.AddRange(additionalSessions);
                    result.Total += additionalSessions.Count;
                }
            }

            return Ok(new SearchResponseDto
            {
                Sessions = result.Sessions,
                Total = result.Total,
                Source = "elasticsearch"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "搜索会话时发生错误");
            return StatusCode(500, new { error = "搜索服务暂时不可用" });
        }
    }

    /// <summary>
    /// 检查 Elasticsearch 服务状态
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>服务状态</returns>
    [HttpGet("search/health")]
    public async Task<IActionResult> GetSearchHealth(CancellationToken cancellationToken = default)
    {
        var available = await _searchService.IsAvailableAsync(cancellationToken);
        return Ok(new { available, service = "elasticsearch" });
    }

    /// <summary>
    /// 获取指定会话的排队消息列表。
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>排队消息列表</returns>
    [HttpGet("sessions/{sessionId:guid}/queued-messages")]
    public async Task<IActionResult> GetQueuedMessages(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireCurrentUserId();

        // 验证会话权限
        var session = await _chatService.GetChatSessionAsync(sessionId, userId, cancellationToken);
        if (session == null)
        {
            return NotFound(new { error = "会话不存在或无权访问" });
        }

        var queuedMessages = await _chatQueueService.GetQueueAsync(sessionId, userId, cancellationToken);
        return Ok(queuedMessages);
    }

    /// <summary>
    /// 取消一条排队消息。
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="queuedMessageId">排队消息 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>取消结果</returns>
    [HttpDelete("sessions/{sessionId:guid}/queued-messages/{queuedMessageId:guid}")]
    public async Task<IActionResult> CancelQueuedMessage(
        Guid sessionId,
        Guid queuedMessageId,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireCurrentUserId();

        var success = await _chatQueueService.CancelQueuedMessageAsync(
            sessionId,
            queuedMessageId,
            userId,
            cancellationToken);

        if (!success)
        {
            return NotFound(new { error = "排队消息不存在或已不在等待状态" });
        }

        return Ok(new { message = "已取消排队消息" });
    }

    /// <summary>
    /// 清空指定会话中所有等待中的排队消息。
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>清空结果</returns>
    [HttpPost("sessions/{sessionId:guid}/queued-messages/clear")]
    public async Task<IActionResult> ClearQueue(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireCurrentUserId();

        var session = await _chatService.GetChatSessionAsync(sessionId, userId, cancellationToken);
        if (session == null)
        {
            return NotFound(new { error = "会话不存在或无权访问" });
        }

        var clearedCount = await _chatQueueService.ClearQueueAsync(sessionId, cancellationToken);

        return Ok(new { clearedCount, message = $"已清空 {clearedCount} 条排队消息" });
    }

    /// <summary>
    /// 获取指定会话的排队消息数量。
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>排队数量</returns>
    [HttpGet("sessions/{sessionId:guid}/queued-messages/count")]
    public async Task<IActionResult> GetQueuedMessageCount(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireCurrentUserId();

        var session = await _chatService.GetChatSessionAsync(sessionId, userId, cancellationToken);
        if (session == null)
        {
            return NotFound(new { error = "会话不存在或无权访问" });
        }

        var count = await _chatQueueService.GetPendingCountAsync(sessionId, cancellationToken);

        return Ok(new { count });
    }
}

/// <summary>
/// 创建聊天会话请求
/// </summary>
public class CreateChatSessionRequest
{
    /// <summary>
    /// 会话标题
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Title { get; set; } = string.Empty;
}



/// <summary>
/// 批量删除消息请求
/// </summary>
public class BatchDeleteMessagesRequest
{
    /// <summary>
    /// 要删除的消息ID列表
    /// </summary>
    [Required]
    public List<Guid> MessageIds { get; set; } = new();
}

