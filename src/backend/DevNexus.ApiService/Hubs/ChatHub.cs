using DevNexus.Core.Abstractions;
using DevNexus.ApiService.Services;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DevNexus.ApiService.Hubs;

/// <summary>
/// 聊天中心，处理实时聊天通信
/// 支持多端同步：同一用户的所有设备都能接收消息
/// </summary>
public partial class ChatHub : Hub
{
    private readonly IChatService _chatService;
    private readonly IUserContextAccessor _userContextAccessor;
    private readonly ICliRuntimeCoordinator _cliRuntimeCoordinator;
    private readonly IChatQueueService _chatQueueService;
    private readonly IChatQueueDispatcher _chatQueueDispatcher;
    private readonly IRuntimeEventNotifier _runtimeEventNotifier;
    private readonly ILogger<ChatHub> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="chatService">聊天服务</param>
    /// <param name="userContextAccessor">用户上下文访问器</param>
    /// <param name="cliRuntimeCoordinator">CLI 运行时协调器</param>
    /// <param name="chatQueueService">聊天消息排队服务</param>
    /// <param name="chatQueueDispatcher">聊天队列调度器</param>
    /// <param name="runtimeEventNotifier">运行时结构化事件通知器</param>
    /// <param name="logger">日志记录器</param>
    public ChatHub(
        IChatService chatService,
        IUserContextAccessor userContextAccessor,
        ICliRuntimeCoordinator cliRuntimeCoordinator,
        IChatQueueService chatQueueService,
        IChatQueueDispatcher chatQueueDispatcher,
        IRuntimeEventNotifier runtimeEventNotifier,
        ILogger<ChatHub> logger)
    {
        _chatService = chatService;
        _userContextAccessor = userContextAccessor;
        _cliRuntimeCoordinator = cliRuntimeCoordinator;
        _chatQueueService = chatQueueService;
        _chatQueueDispatcher = chatQueueDispatcher;
        _runtimeEventNotifier = runtimeEventNotifier;
        _logger = logger;
    }

    /// <summary>
    /// 获取用户组名称
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>组名称</returns>
    private static string GetUserGroupName(Guid userId) => $"user:{userId}";

    /// <summary>
    /// 打断消息生成
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    [Authorize]
    public async Task CancelMessageGeneration(Guid sessionId)
    {
        var userId = GetCurrentUserId();
        var userGroup = GetUserGroupName(userId);

        _logger.LogDebug(
            "[SignalR.Chat] CancelMessageGeneration | UserId={UserId} SessionId={SessionId}",
            userId,
            sessionId);

        await _chatService.CancelMessageGenerationAsync(
            sessionId,
            Context.ConnectionAborted);

        await _runtimeEventNotifier.NotifyAsync(
            userId,
            sessionId,
            ServerEventType.GenerationCancelled,
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

        _logger.LogDebug(
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
        var userId = GetCurrentUserId();
        var sessions = await _chatService.GetChatSessionsAsync(userId, Context.ConnectionAborted);
        if (!sessions.Any(session => session.Id == sessionId))
        {
            await Clients.Caller.SendAsync(
                "ChatMessagesReceived",
                new List<ChatMessageDto>(),
                Context.ConnectionAborted);
            return;
        }

        var messages = await _chatService.GetChatMessagesAsync(
            sessionId,
            Context.ConnectionAborted);

        // 仅返回给当前调用者
        await Clients.Caller.SendAsync(
            "ChatMessagesReceived",
            messages,
            Context.ConnectionAborted);
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

            _logger.LogDebug(
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

                // 连接断开时不立即销毁 CLI，会话恢复交由重连与 Reaper 协同处理。
                _logger.LogDebug(
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

    /// <summary>
    /// 获取指定会话的排队消息列表（SignalR 入口）。
    /// </summary>
    [Authorize]
    public async Task GetQueuedMessages(Guid sessionId)
    {
        var userId = GetCurrentUserId();

        var session = await _chatService.GetChatSessionAsync(sessionId, userId, Context.ConnectionAborted);
        if (session == null)
        {
            await Clients.Caller.SendAsync("QueuedMessagesReceived", Array.Empty<object>(), Context.ConnectionAborted);
            return;
        }

        var queuedMessages = await _chatQueueService.GetQueueAsync(sessionId, userId, Context.ConnectionAborted);

        await Clients.Caller.SendAsync(
            "QueuedMessagesReceived",
            queuedMessages,
            Context.ConnectionAborted);
    }

    /// <summary>
    /// 取消一条排队消息（SignalR 入口）。
    /// </summary>
    [Authorize]
    public async Task CancelQueuedMessage(Guid sessionId, Guid queuedMessageId)
    {
        var userId = GetCurrentUserId();
        var userGroup = GetUserGroupName(userId);

        var success = await _chatQueueService.CancelQueuedMessageAsync(
            sessionId,
            queuedMessageId,
            userId,
            Context.ConnectionAborted);

        if (success)
        {
            await _runtimeEventNotifier.NotifyAsync(
                userId,
                sessionId,
                ServerEventType.QueueStateChanged,
                new { QueuedMessageId = queuedMessageId, SessionId = sessionId, Action = "removed" },
                Context.ConnectionAborted);
        }
    }

    /// <summary>
    /// 清空指定会话的所有排队消息（SignalR 入口）。
    /// </summary>
    [Authorize]
    public async Task ClearQueuedMessages(Guid sessionId)
    {
        var userId = GetCurrentUserId();
        var userGroup = GetUserGroupName(userId);

        var clearedCount = await _chatQueueService.ClearQueueAsync(sessionId, Context.ConnectionAborted);

        if (clearedCount > 0)
        {
            await _runtimeEventNotifier.NotifyAsync(
                userId,
                sessionId,
                ServerEventType.QueueStateChanged,
                new { SessionId = sessionId, ClearedCount = clearedCount, Action = "cleared" },
                Context.ConnectionAborted);
        }
    }

    /// <summary>
    /// 获取指定聊天会话当前的 CLI 状态快照。
    /// </summary>
    [Authorize]
    public async Task<CliSessionStateDto?> GetCliExecSession(Guid sessionId)
    {
        var userId = GetCurrentUserId();
        var session = await _cliRuntimeCoordinator.GetSessionAsync(userId, sessionId, Context.ConnectionAborted);
        return session?.State;
    }

    /// <summary>
    /// 轮询指定聊天会话当前的 CLI 执行结果。
    /// </summary>
    [Authorize]
    public async Task<CliExecSessionDto?> PollCliExecSession(Guid sessionId)
    {
        return await _cliRuntimeCoordinator.GetSessionAsync(
            GetCurrentUserId(),
            sessionId,
            Context.ConnectionAborted);
    }

    /// <summary>
    /// 获取指定聊天会话的 CLI 执行日志。
    /// </summary>
    [Authorize]
    public async Task<CliExecLogChunkDto?> GetCliExecLog(Guid sessionId, int startIndex = 0)
    {
        return await _cliRuntimeCoordinator.GetLogChunkAsync(
            GetCurrentUserId(),
            sessionId,
            startIndex,
            Context.ConnectionAborted);
    }

    /// <summary>
    /// 等待指定聊天会话的 CLI 执行进入终态。
    /// </summary>
    [Authorize]
    public async Task<CliExecSessionDto?> WaitCliExecSession(Guid sessionId, int timeoutMs = 10000)
    {
        var userId = GetCurrentUserId();
        return await _cliRuntimeCoordinator.WaitForExitAsync(
            userId,
            sessionId,
            TimeSpan.FromMilliseconds(Math.Max(timeoutMs, 1000)),
            Context.ConnectionAborted);
    }

    /// <summary>
    /// 向当前聊天会话关联的 CLI 运行时写入输入。
    /// </summary>
    [Authorize]
    public async Task SendCliInput(Guid sessionId, string input)
    {
        var userId = GetCurrentUserId();
        var userGroup = GetUserGroupName(userId);
        SetUserContext(userId, sessionId);

        await ForwardToRuntimeInputAsync(userId, sessionId, userGroup, input);
    }

    /// <summary>
    /// 终止当前聊天会话关联的 CLI 运行时。
    /// </summary>
    [Authorize]
    public async Task<CliExecTerminateResultDto> TerminateCliSession(Guid sessionId)
    {
        var userId = GetCurrentUserId();
        _logger.LogInformation(
            "[SignalR.Chat] TerminateCliSession | UserId={UserId} SessionId={SessionId}",
            userId,
            sessionId);
        return await _cliRuntimeCoordinator.TerminateAsync(userId, sessionId, Context.ConnectionAborted);
    }

    /// <summary>
    /// 回滚当前聊天会话最近一次高风险 CLI 快照。
    /// </summary>
    [Authorize]
    public async Task<CliExecRollbackResultDto> RollbackCliExecSession(Guid sessionId)
    {
        var userId = GetCurrentUserId();
        _logger.LogInformation(
            "[SignalR.Chat] RollbackCliExecSession | UserId={UserId} SessionId={SessionId}",
            userId,
            sessionId);
        return await _cliRuntimeCoordinator.RollbackAsync(userId, sessionId, Context.ConnectionAborted);
    }
}
