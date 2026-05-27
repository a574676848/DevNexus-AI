using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using DevNexus.Shared.DTOs;
using DevNexus.Client.Shared.Abstractions;
using DevNexus.Client.Shared.Utilities;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
namespace DevNexus.Client.Shared.Services.Communication;

/// <summary>
/// SignalR 实时通信服务实现
/// </summary>
public class SignalRService : ISignalRService
{
    private readonly IAuthService _authService;
    private readonly ISessionState _sessionState;
    private readonly ILogger<SignalRService> _logger;
    private readonly PendingGenerationCancelQueue _pendingGenerationCancels = new();
    private HubConnection? _hubConnection;
    private HubConnection? _artifactHubConnection;
    private bool _isConnected;
    private bool _isChatHubConnected;
    private bool _isArtifactHubConnected;

    /// <inheritdoc />
    public event Action<BlockDto>? OnBlockReceived;

    /// <inheritdoc />
    public event Action<ChatMessageDto>? OnMessageReceived;

    /// <inheritdoc />
    public event Action<ArtifactStatusDto>? OnArtifactStatusReceived;

    /// <inheritdoc />
    public event Action<List<ChatSessionDto>>? OnChatSessionsReceived;

    /// <inheritdoc />
    public event Action<bool>? OnConnectionChanged;

    /// <inheritdoc />
    public event Action<List<QueuedChatMessageDto>>? OnQueuedMessagesReceived;

    /// <inheritdoc />
    public event Action<ServerEvent>? OnServerEvent;

    /// <inheritdoc />
    public bool IsConnected => _isConnected;

    /// <inheritdoc />
    public bool IsChatConnected => _isChatHubConnected;

    /// <inheritdoc />
    public bool IsArtifactConnected => _isArtifactHubConnected;

    /// <inheritdoc />
    public HubConnection? HubConnection => _hubConnection;

    public SignalRService(
        IAuthService authService,
        ISessionState sessionState,
        ILogger<SignalRService> logger)
    {
        _authService = authService;
        _sessionState = sessionState;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ConnectAsync()
    {
        if (_isChatHubConnected && _isArtifactHubConnected)
        {
            return;
        }

        Exception? chatHubException = null;
        Exception? artifactHubException = null;

        try
        {
            await ConnectChatHubAsync();
        }
        catch (Exception ex)
        {
            chatHubException = ex;
            _isChatHubConnected = false;
            _logger.LogError(ex, "SignalRService.ConnectAsync.ChatHub");
        }

        try
        {
            await ConnectArtifactHubAsync();
        }
        catch (Exception ex)
        {
            artifactHubException = ex;
            _isArtifactHubConnected = false;
            _logger.LogError(ex, "SignalRService.ConnectAsync.ArtifactHub");
        }

        UpdateAggregateConnectionState();

        if (chatHubException != null)
        {
            throw new InvalidOperationException("ChatHub 连接失败", chatHubException);
        }

        if (artifactHubException != null)
        {
            // ArtifactHub 失败不阻断聊天主链路，保留日志即可。
            _logger.LogWarning(artifactHubException, "SignalRService.ConnectAsync.ArtifactHubDegraded");
        }
    }

    private async Task ConnectChatHubAsync()
    {
        if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected) return;

        var hubUrl = await _authService.GetApiBaseUrlAsync() + "/chat-hub";

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = async () => await _authService.GetAccessTokenAsync();
            })
            .WithAutomaticReconnect(new CustomRetryPolicy())
            .Build();

        _hubConnection.ServerTimeout = TimeSpan.FromMinutes(5);
        _hubConnection.KeepAliveInterval = TimeSpan.FromSeconds(30);

        RegisterEventHandlers();

        _hubConnection.Closed += OnClosed;
        _hubConnection.Reconnecting += OnReconnecting;
        _hubConnection.Reconnected += OnReconnected;

        await _hubConnection.StartAsync();
        _isChatHubConnected = true;
        _logger.LogInformation("SignalR ChatHub 已连接");
        UpdateAggregateConnectionState();
        _ = RequestSessionListRefreshAsync();
    }

    private async Task ConnectArtifactHubAsync()
    {
        if (_artifactHubConnection != null && _artifactHubConnection.State == HubConnectionState.Connected) return;

        var hubUrl = await _authService.GetApiBaseUrlAsync() + "/artifact-hub";

        _artifactHubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = async () => await _authService.GetAccessTokenAsync();
            })
            .WithAutomaticReconnect(new CustomRetryPolicy())
            .Build();

        _artifactHubConnection.ServerTimeout = TimeSpan.FromMinutes(5);
        _artifactHubConnection.KeepAliveInterval = TimeSpan.FromSeconds(30);

        _artifactHubConnection.On<ArtifactStatusDto>("ReceiveArtifactStatus", status =>
        {
             OnArtifactStatusReceived?.Invoke(status);
        });

        _artifactHubConnection.Closed += OnArtifactHubClosed;
        _artifactHubConnection.Reconnecting += OnArtifactHubReconnecting;
        _artifactHubConnection.Reconnected += OnArtifactHubReconnected;

        await _artifactHubConnection.StartAsync();
        _isArtifactHubConnected = true;
        _logger.LogInformation("SignalR ArtifactHub 已连接");
        UpdateAggregateConnectionState();
    }

    /// <inheritdoc />
    public async Task DisconnectAsync()
    {
        var tasks = new List<Task>();

        if (_hubConnection != null)
            tasks.Add(_hubConnection.StopAsync());

        if (_artifactHubConnection != null)
            tasks.Add(_artifactHubConnection.StopAsync());

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SignalRService.Disconnect.Failure");
        }
        finally
        {
            _isChatHubConnected = false;
            _isArtifactHubConnected = false;
            UpdateAggregateConnectionState();
        }
    }

    /// <inheritdoc />
    public async Task SendMessageAsync(ChatRequest request)
    {
        EnsureConnected();

        try
        {
            await _hubConnection!.InvokeAsync("SendMessage", request);
        }
        catch (TimeoutException ex)
        {
            // 超时异常：服务端响应超时
            _logger.LogError(ex, "SignalRService.SendMessageAsync.Timeout | SessionId={SessionId}", request.SessionId);

            EmitLocalRuntimeEvent(
                request.SessionId ?? Guid.Empty,
                ServerEventType.GenerationFailed,
                new
                {
                    SessionId = request.SessionId ?? Guid.Empty,
                    ErrorMessage = "请求超时，请检查网络连接或稍后重试",
                    ErrorType = nameof(TimeoutException)
                });
        }
        catch (OperationCanceledException)
        {
            // 用户主动取消，不做处理
        }
        catch (Exception ex)
        {
            // 其他异常（连接断开、服务端错误等）
            _logger.LogError(ex, "SignalRService.SendMessageAsync | SessionId={SessionId}", request.SessionId);

            EmitLocalRuntimeEvent(
                request.SessionId ?? Guid.Empty,
                ServerEventType.GenerationFailed,
                new
                {
                    SessionId = request.SessionId ?? Guid.Empty,
                    ErrorMessage = $"发送消息失败：{ex.Message}",
                    ErrorType = ex.GetType().Name
            });
        }
    }

    /// <inheritdoc />
    public async Task ResumePendingInteractionAsync(ChatRequest request)
    {
        EnsureConnected();

        try
        {
            await _hubConnection!.InvokeAsync("ResumePendingInteraction", request);
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex, "SignalRService.ResumePendingInteractionAsync.Timeout | SessionId={SessionId}", request.SessionId);

            EmitLocalRuntimeEvent(
                request.SessionId ?? Guid.Empty,
                ServerEventType.GenerationFailed,
                new
                {
                    SessionId = request.SessionId ?? Guid.Empty,
                    ErrorMessage = "恢复执行超时，请检查网络连接或稍后重试",
                    ErrorType = nameof(TimeoutException)
                });
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SignalRService.ResumePendingInteractionAsync | SessionId={SessionId}", request.SessionId);

            EmitLocalRuntimeEvent(
                request.SessionId ?? Guid.Empty,
                ServerEventType.GenerationFailed,
                new
                {
                    SessionId = request.SessionId ?? Guid.Empty,
                    ErrorMessage = $"恢复执行失败：{ex.Message}",
                    ErrorType = ex.GetType().Name
                });
        }
    }

    /// <inheritdoc />
    public async Task CancelGenerationAsync(Guid sessionId)
    {
        if (!IsChatHubConnected())
        {
            EnqueueGenerationCancel(sessionId, "ChatHub 未连接");
            return;
        }

        try
        {
            await SendCancelGenerationAsync(sessionId);
        }
        catch (Exception ex) when (ShouldReplayGenerationCancel(ex))
        {
            EnqueueGenerationCancel(sessionId, ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task SendCliInputAsync(Guid sessionId, string input)
    {
        EnsureConnected();
        await _hubConnection!.InvokeAsync("SendCliInput", sessionId, input);
    }

    /// <inheritdoc />
    public async Task<CliExecTerminateResultDto?> TerminateCliSessionAsync(Guid sessionId)
    {
        EnsureConnected();
        try
        {
            return await _hubConnection!.InvokeAsync<CliExecTerminateResultDto?>("TerminateCliSession", sessionId);
        }
        catch (HubException ex) when (ex.Message.Contains("Method does not exist", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                ex,
                "SignalRService.TerminateCliSessionAsync.MethodMissing | SessionId={SessionId}",
                sessionId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<CliExecRollbackResultDto?> RollbackCliExecSessionAsync(Guid sessionId)
    {
        EnsureConnected();
        try
        {
            return await _hubConnection!.InvokeAsync<CliExecRollbackResultDto?>("RollbackCliExecSession", sessionId);
        }
        catch (HubException ex) when (ex.Message.Contains("Method does not exist", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                ex,
                "SignalRService.RollbackCliExecSessionAsync.MethodMissing | SessionId={SessionId}",
                sessionId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<CliSessionStateDto?> GetCliExecSessionAsync(Guid sessionId)
    {
        EnsureConnected();
        return await _hubConnection!.InvokeAsync<CliSessionStateDto?>("GetCliExecSession", sessionId);
    }

    /// <inheritdoc />
    public async Task<CliExecPollResultDto?> PollCliExecSessionAsync(Guid sessionId)
    {
        EnsureConnected();
        return await _hubConnection!.InvokeAsync<CliExecPollResultDto?>("PollCliExecSession", sessionId);
    }

    /// <inheritdoc />
    public async Task<CliExecLogResultDto?> GetCliExecLogAsync(Guid sessionId, int startIndex = 0)
    {
        EnsureConnected();
        return await _hubConnection!.InvokeAsync<CliExecLogResultDto?>("GetCliExecLog", sessionId, startIndex);
    }

    /// <inheritdoc />
    public async Task<CliExecPollResultDto?> WaitCliExecSessionAsync(Guid sessionId, int timeoutMs = 10000)
    {
        EnsureConnected();
        return await _hubConnection!.InvokeAsync<CliExecPollResultDto?>("WaitCliExecSession", sessionId, timeoutMs);
    }

    /// <summary>
    /// 注册事件处理器
    /// </summary>
    private void RegisterEventHandlers()
    {
        if (_hubConnection == null)
        {
            throw new InvalidOperationException("HubConnection 未初始化");
        }

        // 接收 Block
        _hubConnection.On<BlockDto>("ReceiveBlock", block =>
        {
            OnBlockReceived?.Invoke(block);
        });

        // 接收完整消息（增量更新）
        _hubConnection.On<ChatMessageDto>("MessageReceived", message =>
        {
            OnMessageReceived?.Invoke(message);
        });

        _hubConnection.On<List<ChatSessionDto>>("ChatSessionsReceived", sessions =>
        {
            var sessionList = sessions ?? new List<ChatSessionDto>();
            _sessionState.SetSessions(sessionList);
            OnChatSessionsReceived?.Invoke(sessionList);
        });

        // 排队消息列表接收
        _hubConnection.On<List<QueuedChatMessageDto>>("QueuedMessagesReceived", items =>
        {
            OnQueuedMessagesReceived?.Invoke(items ?? new List<QueuedChatMessageDto>());
        });

        _hubConnection.On<ServerEvent>("ServerEventReceived", serverEvent =>
        {
            OnServerEvent?.Invoke(serverEvent);
        });
    }

    /// <inheritdoc />
    public async Task GetQueuedMessagesAsync(Guid sessionId)
    {
        EnsureConnected();
        await _hubConnection!.InvokeAsync("GetQueuedMessages", sessionId);
    }

    /// <inheritdoc />
    public async Task CancelQueuedMessageAsync(Guid sessionId, Guid queuedMessageId)
    {
        EnsureConnected();
        await _hubConnection!.InvokeAsync("CancelQueuedMessage", sessionId, queuedMessageId);
    }

    /// <inheritdoc />
    public async Task ClearQueuedMessagesAsync(Guid sessionId)
    {
        EnsureConnected();
        await _hubConnection!.InvokeAsync("ClearQueuedMessages", sessionId);
    }

    /// <summary>
    /// 确保已连接
    /// </summary>
    private void EnsureConnected()
    {
        if (_hubConnection == null || _hubConnection.State != HubConnectionState.Connected)
        {
            throw new InvalidOperationException("SignalR 未连接");
        }
    }

    private bool IsChatHubConnected()
    {
        return _hubConnection?.State == HubConnectionState.Connected;
    }

    private Task SendCancelGenerationAsync(Guid sessionId)
    {
        return _hubConnection!.InvokeAsync("CancelMessageGeneration", sessionId);
    }

    private void EnqueueGenerationCancel(Guid sessionId, string reason)
    {
        _pendingGenerationCancels.Enqueue(sessionId);
        _logger.LogWarning(
            "取消生成请求已进入重连重放队列 | SessionId={SessionId} Reason={Reason}",
            sessionId,
            reason);
    }

    private static bool ShouldReplayGenerationCancel(Exception ex)
    {
        return ex is InvalidOperationException
            or TimeoutException
            or HubException
            or OperationCanceledException;
    }

    private async Task ReplayPendingGenerationCancelsAsync()
    {
        if (!IsChatHubConnected())
        {
            return;
        }

        foreach (var sessionId in _pendingGenerationCancels.Drain())
        {
            try
            {
                await SendCancelGenerationAsync(sessionId);
                _logger.LogInformation(
                    "已重放取消生成请求 | SessionId={SessionId}",
                    sessionId);
            }
            catch (Exception ex) when (ShouldReplayGenerationCancel(ex))
            {
                EnqueueGenerationCancel(sessionId, ex.Message);
            }
        }
    }

    private void EmitLocalRuntimeEvent(Guid sessionId, ServerEventType eventType, object? data)
    {
        OnServerEvent?.Invoke(new ServerEvent
        {
            SessionId = sessionId,
            EventType = eventType,
            Data = data,
            Timestamp = DateTime.UtcNow
        });
    }

    private Task OnClosed(Exception? exception)
    {
        _isChatHubConnected = false;
        _logger.LogWarning(exception, "SignalR ChatHub 已关闭");
        UpdateAggregateConnectionState();

        if (exception != null)
        {
            // 连接非正常关闭时记录日志 (不等待)
            _logger.LogWarning(exception, "SignalRService.OnClosed | Exception={Exception}", exception.ToString());
        }

        return Task.CompletedTask;
    }

    private Task OnReconnecting(Exception? exception)
    {
        _isChatHubConnected = false;
        _logger.LogInformation(exception, "SignalR ChatHub 重连中");
        UpdateAggregateConnectionState();
        return Task.CompletedTask;
    }

    private Task OnReconnected(string? connectionId)
    {
        _isChatHubConnected = true;
        _logger.LogInformation("SignalR ChatHub 已重连 | ConnectionId={ConnectionId}", connectionId);
        UpdateAggregateConnectionState();
        _ = ReplayPendingGenerationCancelsAsync();
        _ = RequestSessionListRefreshAsync();
        return Task.CompletedTask;
    }

    private Task OnArtifactHubClosed(Exception? exception)
    {
        _isArtifactHubConnected = false;
        _logger.LogWarning(exception, "SignalR ArtifactHub 已关闭");
        UpdateAggregateConnectionState();

        if (exception != null)
        {
            _logger.LogWarning(exception, "SignalRService.OnArtifactHubClosed | Exception={Exception}", exception.ToString());
        }

        return Task.CompletedTask;
    }

    private Task OnArtifactHubReconnecting(Exception? exception)
    {
        _isArtifactHubConnected = false;
        _logger.LogInformation(exception, "SignalR ArtifactHub 重连中");
        UpdateAggregateConnectionState();
        return Task.CompletedTask;
    }

    private Task OnArtifactHubReconnected(string? connectionId)
    {
        _isArtifactHubConnected = true;
        _logger.LogInformation("SignalR ArtifactHub 已重连 | ConnectionId={ConnectionId}", connectionId);
        UpdateAggregateConnectionState();
        return Task.CompletedTask;
    }

    private void UpdateAggregateConnectionState()
    {
        // 聊天主链路只依赖 ChatHub。ArtifactHub 断连不应阻断消息输入与生成控制。
        var nextState = _isChatHubConnected;
        if (_isConnected == nextState)
        {
            return;
        }

        _isConnected = nextState;
        _logger.LogDebug(
            "SignalR 聚合连接状态变更 | ChatHub={ChatHubConnected} ArtifactHub={ArtifactHubConnected} Connected={Connected}",
            _isChatHubConnected,
            _isArtifactHubConnected,
            _isConnected);
        OnConnectionChanged?.Invoke(nextState);
    }

    private async Task RequestSessionListRefreshAsync()
    {
        if (_hubConnection == null || _hubConnection.State != HubConnectionState.Connected)
        {
            return;
        }

        try
        {
            await _hubConnection.InvokeAsync("GetChatSessions");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalRService.RequestSessionListRefreshAsync");
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();

        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }

        if (_artifactHubConnection != null)
        {
            await _artifactHubConnection.DisposeAsync();
        }
    }
}

/// <summary>
/// 自定义重连策略 - 指数退避
/// </summary>
public class CustomRetryPolicy : IRetryPolicy
{
    private readonly TimeSpan[] _retryDelays =
    {
        TimeSpan.Zero,
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30)
    };

    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        if (retryContext.PreviousRetryCount < _retryDelays.Length)
        {
            return _retryDelays[retryContext.PreviousRetryCount];
        }

        // 最多重试 4 次后放弃
        return null;
    }
}
