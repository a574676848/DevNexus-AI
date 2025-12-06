using Microsoft.AspNetCore.SignalR;
using DevNexus.Core.Abstractions;
using DevNexus.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace DevNexus.ApiService.Hubs;

/// <summary>
/// 聊天中心，处理实时聊天通信
/// 支持多端同步：同一用户的所有设备都能接收消息
/// 支持人机回环：敏感操作需要用户审批
/// </summary>
public class ChatHub : Hub
{
    private readonly IChatService _chatService;
    private readonly IApprovalService _approvalService;
    private readonly ILogger<ChatHub> _logger;
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="chatService">聊天服务</param>
    /// <param name="approvalService">审批服务</param>
    /// <param name="logger">日志记录器</param>
    public ChatHub(
        IChatService chatService, 
        IApprovalService approvalService,
        ILogger<ChatHub> logger)
    {
        _chatService = chatService;
        _approvalService = approvalService;
        _logger = logger;
    }
    
    /// <summary>
    /// 获取用户组名称
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>组名称</returns>
    private static string GetUserGroupName(Guid userId) => $"user:{userId}";
    
    /// <summary>
    /// 发送消息
    /// </summary>
    /// <param name="chatRequest">聊天请求</param>
    [Authorize]
    public async Task SendMessage(ChatRequest chatRequest)
    {
        // 从认证系统获取用户ID
        var userId = GetCurrentUserId();
        var userGroup = GetUserGroupName(userId);
        
        _logger.LogInformation(
            "[SignalR.Chat] SendMessage | UserId={UserId} SessionId={SessionId}",
            userId,
            chatRequest.SessionId);
        
        // 使用流式 API，向用户所有设备广播
        await _chatService.StreamMessageAsync(
            chatRequest,
            userId,
            async (block) =>
            {
                // 向用户的所有连接推送 Block（多端同步）
                await Clients.Group(userGroup).SendAsync(
                    "ReceiveBlock",
                    block,
                    Context.ConnectionAborted);
            },
            Context.ConnectionAborted);
        
        // 发送消息列表更新事件给用户所有设备
        await BroadcastChatMessagesUpdate(userId, chatRequest.SessionId ?? Guid.Empty, Context.ConnectionAborted);
    }
    
    /// <summary>
    /// 向用户所有设备广播聊天消息更新
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="sessionId">会话ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    private async Task BroadcastChatMessagesUpdate(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        var userGroup = GetUserGroupName(userId);
        
        if (sessionId != Guid.Empty)
        {
            var messages = await _chatService.GetChatMessagesAsync(sessionId, cancellationToken);
            await Clients.Group(userGroup).SendAsync(
                "ChatMessagesReceived",
                messages,
                cancellationToken);
        }
        
        // 更新会话列表
        var sessions = await _chatService.GetChatSessionsAsync(userId, cancellationToken);
        await Clients.Group(userGroup).SendAsync(
            "ChatSessionsReceived",
            sessions,
            cancellationToken);
    }
    
    /// <summary>
    /// 打断消息生成
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    [Authorize]
    public async Task CancelMessageGeneration(Guid sessionId)
    {
        var userId = GetCurrentUserId();
        var userGroup = GetUserGroupName(userId);
        
        _logger.LogInformation(
            "[SignalR.Chat] CancelMessageGeneration | UserId={UserId} SessionId={SessionId}",
            userId,
            sessionId);
        
        await _chatService.CancelMessageGenerationAsync(
            sessionId,
            Context.ConnectionAborted);
        
        // 向用户所有设备发送取消事件
        await Clients.Group(userGroup).SendAsync(
            "MessageGenerationCancelled",
            new { SessionId = sessionId },
            Context.ConnectionAborted);
    }
    
    /// <summary>
    /// 创建聊天会话
    /// </summary>
    /// <param name="title">会话标题</param>
    [Authorize]
    public async Task CreateChatSession(string title)
    {
        var userId = GetCurrentUserId();
        var userGroup = GetUserGroupName(userId);
        
        _logger.LogInformation(
            "[SignalR.Chat] CreateChatSession | UserId={UserId} Title={Title}",
            userId,
            title);
        
        var sessionId = await _chatService.CreateChatSessionAsync(
            userId,
            title,
            Context.ConnectionAborted);
        
        // 向用户所有设备返回会话ID
        await Clients.Group(userGroup).SendAsync(
            "ChatSessionCreated",
            new { SessionId = sessionId },
            Context.ConnectionAborted);
        
        // 刷新会话列表
        var sessions = await _chatService.GetChatSessionsAsync(userId, Context.ConnectionAborted);
        await Clients.Group(userGroup).SendAsync(
            "ChatSessionsReceived",
            sessions,
            Context.ConnectionAborted);
    }
    
    /// <summary>
    /// 获取聊天会话列表
    /// </summary>
    [Authorize]
    public async Task GetChatSessions()
    {
        var userId = GetCurrentUserId();
        
        var sessions = await _chatService.GetChatSessionsAsync(
            userId,
            Context.ConnectionAborted);
        
        // 仅返回给当前调用者
        await Clients.Caller.SendAsync(
            "ChatSessionsReceived",
            sessions,
            Context.ConnectionAborted);
    }
    
    /// <summary>
    /// 获取聊天消息
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    [Authorize]
    public async Task GetChatMessages(Guid sessionId)
    {
        var messages = await _chatService.GetChatMessagesAsync(
            sessionId,
            Context.ConnectionAborted);
        
        // 仅返回给当前调用者
        await Clients.Caller.SendAsync(
            "ChatMessagesReceived",
            messages,
            Context.ConnectionAborted);
    }
    
    #region 人机回环 - 审批接口
    
    /// <summary>
    /// 批准操作
    /// </summary>
    /// <param name="actionId">操作ID</param>
    [Authorize]
    public async Task ApproveAction(Guid actionId)
    {
        var userId = GetCurrentUserId();
        var userGroup = GetUserGroupName(userId);
        
        _logger.LogInformation(
            "[SignalR.Approval] ApproveAction | UserId={UserId} ActionId={ActionId}",
            userId,
            actionId);
        
        await _approvalService.ApproveAsync(actionId, Context.ConnectionAborted);
        
        // 通知用户所有设备审批已完成
        await Clients.Group(userGroup).SendAsync(
            "ApprovalCompleted",
            new { ActionId = actionId, Approved = true },
            Context.ConnectionAborted);
    }
    
    /// <summary>
    /// 拒绝操作
    /// </summary>
    /// <param name="actionId">操作ID</param>
    /// <param name="reason">拒绝原因</param>
    [Authorize]
    public async Task RejectAction(Guid actionId, string? reason = null)
    {
        var userId = GetCurrentUserId();
        var userGroup = GetUserGroupName(userId);
        
        _logger.LogInformation(
            "[SignalR.Approval] RejectAction | UserId={UserId} ActionId={ActionId} Reason={Reason}",
            userId,
            actionId,
            reason);
        
        await _approvalService.RejectAsync(actionId, reason, Context.ConnectionAborted);
        
        // 通知用户所有设备审批已完成
        await Clients.Group(userGroup).SendAsync(
            "ApprovalCompleted",
            new { ActionId = actionId, Approved = false, Reason = reason },
            Context.ConnectionAborted);
    }
    
    /// <summary>
    /// 获取待审批操作
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    [Authorize]
    public async Task GetPendingApproval(Guid sessionId)
    {
        var pendingApproval = _approvalService.GetPendingApproval(sessionId);
        
        await Clients.Caller.SendAsync(
            "PendingApprovalReceived",
            pendingApproval,
            Context.ConnectionAborted);
    }
    
    #endregion
    
    /// <summary>
    /// 获取当前认证用户的ID
    /// </summary>
    /// <returns>用户ID</returns>
    private Guid GetCurrentUserId()
    {
        var userIdClaim = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
        {
            throw new HubException("用户未认证");
        }
        
        return Guid.Parse(userIdClaim.Value);
    }
    
    /// <summary>
    /// 客户端连接时调用 - 将用户加入其专属组
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var connectionId = Context.ConnectionId;
        
        // 如果用户已认证，将其加入用户组
        if (Context.User?.Identity?.IsAuthenticated == true)
        {
            var userId = GetCurrentUserId();
            var userGroup = GetUserGroupName(userId);
            
            await Groups.AddToGroupAsync(connectionId, userGroup);
            
            _logger.LogInformation(
                "[SignalR.Connection] User connected | UserId={UserId} ConnectionId={ConnectionId} Group={Group}",
                userId,
                connectionId,
                userGroup);
        }
        
        // 发送欢迎消息给客户端
        await Clients.Caller.SendAsync(
            "Welcome",
            new { Message = "Connected to chat hub", ConnectionId = connectionId },
            Context.ConnectionAborted);
        
        await base.OnConnectedAsync();
    }
    
    /// <summary>
    /// 客户端断开连接时调用 - 自动从组中移除
    /// </summary>
    /// <param name="exception">断开连接的异常</param>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;
        
        if (Context.User?.Identity?.IsAuthenticated == true)
        {
            try
            {
                var userId = GetCurrentUserId();
                var userGroup = GetUserGroupName(userId);
                
                // SignalR 会自动从组中移除断开的连接，这里只记录日志
                _logger.LogInformation(
                    "[SignalR.Connection] User disconnected | UserId={UserId} ConnectionId={ConnectionId} Group={Group} Exception={Exception}",
                    userId,
                    connectionId,
                    userGroup,
                    exception?.Message);
            }
            catch
            {
                // 忽略解析用户ID的错误
            }
        }
        
        await base.OnDisconnectedAsync(exception);
    }
}
