using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DevNexus.Core.Abstractions;
using DevNexus.Shared.DTOs;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;

namespace DevNexus.ApiService.Controllers;

/// <summary>
/// 聊天控制器，提供RESTful API端点
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="chatService">聊天服务</param>
    public ChatController(IChatService chatService)
    {
        _chatService = chatService;
    }
    
    /// <summary>
    /// 获取当前用户的ID
    /// </summary>
    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
        {
            throw new UnauthorizedAccessException("用户未认证");
        }
        
        return Guid.Parse(userIdClaim.Value);
    }
    
    /// <summary>
    /// 创建聊天会话
    /// </summary>
    /// <param name="request">创建会话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>创建的会话ID</returns>
    [HttpPost("sessions")]
    public async Task<IActionResult> CreateSession(
        [FromBody] CreateChatSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var sessionId = await _chatService.CreateChatSessionAsync(
            userId,
            request.Title,
            cancellationToken);
        
        return CreatedAtAction(nameof(GetSession), new { sessionId }, new { SessionId = sessionId });
    }
    
    /// <summary>
    /// 获取聊天会话列表
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>会话列表</returns>
    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions(CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
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
        var userId = GetCurrentUserId();
        var sessions = await _chatService.GetChatSessionsAsync(userId, cancellationToken);
        var session = sessions.FirstOrDefault(s => s.Id == sessionId);
        
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
        var messages = await _chatService.GetChatMessagesAsync(sessionId, cancellationToken);
        
        return Ok(messages);
    }
    
    /// <summary>
    /// 发送消息
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="request">发送消息请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发送结果</returns>
    [HttpPost("sessions/{sessionId:guid}/messages")]
    public async Task<IActionResult> SendMessage(
        Guid sessionId,
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        
        var chatRequest = new ChatRequest
        {
            SessionId = sessionId,
            Content = request.Content,
            MessageType = request.MessageType ?? "text",
            Metadata = request.Metadata ?? new Dictionary<string, object>()
        };
        
        await _chatService.SendMessageAsync(chatRequest, userId, cancellationToken);
        
        return Ok(new { Message = "消息发送成功" });
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
/// 发送消息请求
/// </summary>
public class SendMessageRequest
{
    /// <summary>
    /// 消息内容
    /// </summary>
    [Required]
    public string Content { get; set; } = string.Empty;
    
    /// <summary>
    /// 消息类型
    /// </summary>
    [MaxLength(20)]
    public string? MessageType { get; set; }
    
    /// <summary>
    /// 消息元数据
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}