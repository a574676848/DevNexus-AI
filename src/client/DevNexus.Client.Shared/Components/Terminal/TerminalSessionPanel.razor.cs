using DevNexus.Client.Shared.Abstractions;
using DevNexus.Client.Shared.Helpers;
using DevNexus.Client.Shared.Models;
using DevNexus.Shared.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DevNexus.Client.Shared.Components.Terminal;

/// <summary>
/// 终端会话面板逻辑。
/// </summary>
public partial class TerminalSessionPanel
{
    [Parameter] public string Title { get; set; } = "终端";
    [Parameter] public string Eyebrow { get; set; } = "实时终端";
    [Parameter] public IReadOnlyList<TerminalRecordState> Records { get; set; } = Array.Empty<TerminalRecordState>();
    [Parameter] public TerminalRecordState? SelectedRecord { get; set; }
    [Parameter] public EventCallback<Guid> OnSelectRecord { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }
    [Parameter] public EventCallback OnOpenInModal { get; set; }
    [Parameter] public bool ShowOpenInModalButton { get; set; } = true;
    [Parameter] public bool EnableTerminate { get; set; } = true;

    private ElementReference _outputRef;
    private DotNetObjectReference<TerminalSessionPanel>? _scrollReference;
    private bool _scrollListenerAttached;
    private bool _showScrollButton;
    private bool _isAtBottom = true;
    private int _lastOutputLength;
    private Guid? _lastRecordId;
    private bool _isCopyingOutput;
    private bool _showCopyOutputSuccess;
    private bool _isTerminating;
    private bool _isRollingBack;
    private bool _isLoadingFullOutput;
    private bool _selectionPulseActive;
    private bool _waitingPulseActive;
    private bool _lastWaitingForInput;
    private bool _pendingSelectionPulse;
    private bool _pendingWaitingPulse;
    private bool _pendingAutoScroll;
    private int _selectionPulseVersion;
    private int _waitingPulseVersion;
    private DateTime _lastAutoScrollAt = DateTime.MinValue;
    private static readonly TimeSpan AutoScrollThrottleInterval = TimeSpan.FromMilliseconds(220);
    private CancellationTokenSource? _pollingCts;
    private Task? _pollingTask;
    private Guid? _pollingSessionId;
    private Guid? _pollingRecordId;
    private int _lastPolledOutputLength;
    private bool _liveOutputHydrated;
    private string? _outputOverride;
    private Guid? _outputOverrideRecordId;

    protected override void OnParametersSet()
    {
        var recordId = SelectedRecord?.RecordId;
        var outputLength = SelectedRecord?.Output?.Length ?? 0;
        var waitingForInput = SelectedRecord?.WaitingForInput == true;

        if (_lastRecordId != recordId)
        {
            _lastRecordId = recordId;
            _lastOutputLength = outputLength;
            _showScrollButton = false;
            _isAtBottom = true;
            _pendingSelectionPulse = recordId.HasValue;
            _pendingAutoScroll = recordId.HasValue;
            _lastPolledOutputLength = 0;
            _liveOutputHydrated = false;
            _outputOverride = null;
            _outputOverrideRecordId = null;
        }
        else if (outputLength != _lastOutputLength)
        {
            if (_isAtBottom)
            {
                _pendingAutoScroll = true;
            }

            _lastOutputLength = outputLength;
        }

        if (!_lastWaitingForInput && waitingForInput)
        {
            _pendingWaitingPulse = true;
        }

        _lastWaitingForInput = waitingForInput;
        ConfigurePollingLoop();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (SelectedRecord == null)
        {
            return;
        }

        if (!_scrollListenerAttached)
        {
            _scrollReference ??= DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("setupScrollListener", _outputRef, _scrollReference);
            _scrollListenerAttached = true;
        }

        if (_pendingAutoScroll && _isAtBottom)
        {
            _pendingAutoScroll = false;
            await ScrollToBottomIfDueAsync(force: true);
        }

        if (_pendingSelectionPulse)
        {
            _pendingSelectionPulse = false;
            _ = RunSelectionPulseAsync();
        }

        if (_pendingWaitingPulse)
        {
            _pendingWaitingPulse = false;
            _ = RunWaitingPulseAsync();
        }
    }

    [JSInvokable]
    public Task OnScrollPositionChanged(bool isAtBottom)
    {
        _isAtBottom = isAtBottom;
        _showScrollButton = !isAtBottom;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private string GetStatusText(TerminalRecordState record)
    {
        if (record.WaitingForInput)
        {
            return "等待输入";
        }

        return GetSessionStateText(record.SessionState);
    }

    private string GetStatusClass(TerminalRecordState record)
    {
        if (record.WaitingForInput)
        {
            return "terminal-session-panel__status--waiting";
        }

        if (record.IsFromHistory && !record.IsActive)
        {
            return "terminal-session-panel__status--history";
        }

        return record.SessionState switch
        {
            "Running" => "terminal-session-panel__status--running",
            "Completed" => "terminal-session-panel__status--completed",
            "Failed" => "terminal-session-panel__status--failed",
            "Cancelled" => "terminal-session-panel__status--cancelled",
            "Reaped" => "terminal-session-panel__status--history",
            _ => "terminal-session-panel__status--history"
        };
    }

    private static string GetRecordChipTitle(TerminalRecordState record)
    {
        if (!string.IsNullOrWhiteSpace(record.Command))
        {
            return record.Command.Length > 28 ? $"{record.Command[..28]}…" : record.Command;
        }

        return $"终端 {record.RecordId.ToString("N")[..8]}";
    }

    private string GetOutputText(TerminalRecordState record)
    {
        if (_outputOverrideRecordId == record.RecordId && _outputOverride != null)
        {
            return _outputOverride;
        }

        return string.IsNullOrWhiteSpace(record.Output) ? "等待输出" : record.Output;
    }

    private static string GetOutputModeLabel(TerminalRecordState record)
    {
        if (record.IsFromHistory && !record.IsActive)
        {
            return "历史";
        }

        return record.WaitingForInput ? "等待输入" : "实时输出";
    }

    private bool CanTerminate()
    {
        return EnableTerminate && SelectedRecord is { IsActive: true, IsFromHistory: false };
    }

    private bool CanRollback()
    {
        return EnableTerminate
            && SelectedRecord is { IsActive: false }
            && !string.IsNullOrWhiteSpace(SelectedRecord.WorkingDirectory);
    }

    private bool CanLoadFullOutput()
    {
        return SelectedRecord != null
            && (SelectedRecord.OutputWasTrimmed || SelectedRecord.HasArchivedOutput)
            && !_isTerminating
            && !_isRollingBack;
    }

    private async Task CopyOutputAsync()
    {
        if (_isCopyingOutput || SelectedRecord == null)
        {
            return;
        }

        var outputText = GetOutputText(SelectedRecord);
        var content = string.IsNullOrWhiteSpace(SelectedRecord.Command)
            ? outputText
            : $"> {SelectedRecord.Command}\n{outputText}";

        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        try
        {
            _isCopyingOutput = true;
            await JS.InvokeVoidAsync("navigator.clipboard.writeText", content);
            _showCopyOutputSuccess = true;
            StateHasChanged();

            await Task.Delay(1400);
            _showCopyOutputSuccess = false;
        }
        catch
        {
            ToastService.Error("复制输出失败，请稍后重试。");
        }
        finally
        {
            _isCopyingOutput = false;
            StateHasChanged();
        }
    }

    private async Task TerminateAsync()
    {
        if (_isTerminating || SelectedRecord == null)
        {
            return;
        }

        try
        {
            _isTerminating = true;
            StateHasChanged();
            var result = await SignalR.TerminateCliSessionAsync(SelectedRecord.SessionId);
            await SignalR.CancelGenerationAsync(SelectedRecord.SessionId);

            if (result?.State != null)
            {
                ChatState.UpdateCliExecSession(result.State);
            }

            if (result == null)
            {
                ToastService.Success("已发送停止请求。");
            }
            else if (result.Terminated)
            {
                ToastService.Success(result.Message);
            }
            else if (result.AlreadyExited)
            {
                ToastService.Success(result.Message);
            }
            else
            {
                ToastService.Warning(result.Message);
            }
        }
        catch
        {
            ToastService.Error("停止终端失败，请稍后重试。");
        }
        finally
        {
            _isTerminating = false;
            StateHasChanged();
        }
    }

    private async Task RollbackAsync()
    {
        if (_isRollingBack || SelectedRecord == null)
        {
            return;
        }

        try
        {
            _isRollingBack = true;
            StateHasChanged();

            var result = await SignalR.RollbackCliExecSessionAsync(SelectedRecord.SessionId);
            if (result?.State != null)
            {
                ChatState.UpdateCliExecSession(result.State);
            }

            if (result == null)
            {
                ToastService.Warning("当前服务端尚未支持回滚接口。");
            }
            else if (result.RolledBack)
            {
                ToastService.Success(result.Message);
            }
            else if (!result.RolledBack)
            {
                ToastService.Warning(result.Message);
            }
        }
        catch
        {
            ToastService.Error("回滚终端快照失败，请稍后重试。");
        }
        finally
        {
            _isRollingBack = false;
            StateHasChanged();
        }
    }

    private async Task LoadFullOutputAsync()
    {
        if (_isLoadingFullOutput || SelectedRecord == null)
        {
            return;
        }

        try
        {
            _isLoadingFullOutput = true;
            StateHasChanged();

            if (SelectedRecord.IsActive && !SelectedRecord.HasArchivedOutput)
            {
                var logResult = await SignalR.GetCliExecLogAsync(SelectedRecord.SessionId);
                _outputOverride = logResult?.PlainOutput ?? SelectedRecord.Output;
            }
            else
            {
                var output = await ApiService.GetTerminalOutputAsync(SelectedRecord.SessionId, SelectedRecord.RecordId);
                _outputOverride = output.Content;
            }

            _outputOverrideRecordId = SelectedRecord.RecordId;
        }
        catch
        {
            ToastService.Error("加载完整日志失败，请稍后重试。");
        }
        finally
        {
            _isLoadingFullOutput = false;
            StateHasChanged();
        }
    }

    private void ConfigurePollingLoop()
    {
        if (SelectedRecord is not { IsActive: true, IsFromHistory: false })
        {
            StopPollingLoop();
            return;
        }

        if (_pollingSessionId == SelectedRecord.SessionId
            && _pollingRecordId == SelectedRecord.RecordId
            && _pollingTask is { IsCompleted: false })
        {
            return;
        }

        StopPollingLoop();
        _pollingSessionId = SelectedRecord.SessionId;
        _pollingRecordId = SelectedRecord.RecordId;
        _pollingCts = new CancellationTokenSource();
        _pollingTask = PollCliExecSessionLoopAsync(SelectedRecord.SessionId, _pollingCts.Token);
    }

    private void StopPollingLoop()
    {
        _pollingCts?.Cancel();
        _pollingCts?.Dispose();
        _pollingCts = null;
        _pollingTask = null;
        _pollingSessionId = null;
        _pollingRecordId = null;
    }

    private async Task PollCliExecSessionLoopAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            CliExecPollResultDto? pollResult;
            try
            {
                pollResult = await SignalR.PollCliExecSessionAsync(sessionId);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                continue;
            }

            if (pollResult?.State == null)
            {
                continue;
            }

            CliExecLogResultDto? logResult = null;
            var shouldHydrateTail = !_liveOutputHydrated;
            var shouldFetchDelta = !shouldHydrateTail && pollResult.OutputLength > _lastPolledOutputLength;

            if (shouldFetchDelta)
            {
                try
                {
                    logResult = await SignalR.GetCliExecLogAsync(sessionId, _lastPolledOutputLength);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    logResult = null;
                }
            }

            await InvokeAsync(() =>
            {
                ChatState.UpdateCliExecSession(pollResult.State);

                if (shouldHydrateTail)
                {
                    ChatState.SyncCliExecLog(
                        sessionId,
                        pollResult.OutputTail,
                        wasTrimmed: pollResult.OutputLength > pollResult.OutputTail.Length);
                    _liveOutputHydrated = true;
                    _lastPolledOutputLength = pollResult.OutputLength;
                }
                else if (!string.IsNullOrWhiteSpace(logResult?.PlainOutput))
                {
                    ChatState.AppendCliExecLog(sessionId, logResult.PlainOutput);
                    _lastPolledOutputLength = logResult.OutputLength;
                }
                StateHasChanged();
            });

            if (pollResult.Exited)
            {
                break;
            }
        }
    }

    private async Task ScrollToBottomAsync(bool force = false)
    {
        await JS.InvokeVoidAsync(force ? "scrollToBottomForce" : "scrollToBottom", _outputRef, force);
        _showScrollButton = false;
        _isAtBottom = true;
        _lastAutoScrollAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 节流终端自动滚动，避免长输出期间持续抢占主线程。
    /// </summary>
    private async Task ScrollToBottomIfDueAsync(bool force = false)
    {
        var now = DateTime.UtcNow;
        if (!force && now - _lastAutoScrollAt < AutoScrollThrottleInterval)
        {
            return;
        }

        await ScrollToBottomAsync(force);
    }

    private async Task FocusComposerAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("devnexus.focusComposerInput");
        }
        catch
        {
            ToastService.Error("定位输入框失败，请稍后重试。");
        }
    }

    public async ValueTask DisposeAsync()
    {
        StopPollingLoop();

        if (_scrollListenerAttached)
        {
            try
            {
                await JS.InvokeVoidAsync("removeScrollListener", _outputRef);
            }
            catch
            {
                // 忽略终端面板销毁阶段的 JS 清理失败
            }
        }

        _scrollReference?.Dispose();
    }

    private string GetCopyOutputLabel()
    {
        if (_showCopyOutputSuccess)
        {
            return "已复制";
        }

        return _isCopyingOutput ? "复制中..." : "复制输出";
    }

    private string GetTerminateLabel()
    {
        return _isTerminating ? "停止中..." : "停止";
    }

    private string GetRollbackLabel()
    {
        return _isRollingBack ? "回滚中..." : "回滚";
    }

    private string GetLoadFullOutputLabel()
    {
        return _isLoadingFullOutput ? "加载中..." : "完整日志";
    }

    private string GetRecordChipClass(TerminalRecordState record)
    {
        return _selectionPulseActive && record.RecordId == SelectedRecord?.RecordId
            ? "is-spotlight"
            : string.Empty;
    }

    private string GetOutputShellClass()
    {
        var classes = new List<string>();

        if (_selectionPulseActive)
        {
            classes.Add("terminal-session-panel__output-shell--spotlight");
        }

        if (SelectedRecord?.WaitingForInput == true)
        {
            classes.Add("terminal-session-panel__output-shell--waiting");
        }

        return string.Join(" ", classes);
    }

    private async Task RunSelectionPulseAsync()
    {
        var version = ++_selectionPulseVersion;
        _selectionPulseActive = true;
        await InvokeAsync(StateHasChanged);
        await Task.Delay(700);

        if (version == _selectionPulseVersion)
        {
            _selectionPulseActive = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task RunWaitingPulseAsync()
    {
        var version = ++_waitingPulseVersion;
        _waitingPulseActive = true;
        await InvokeAsync(StateHasChanged);
        await Task.Delay(1600);

        if (version == _waitingPulseVersion)
        {
            _waitingPulseActive = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private static string GetSessionStateText(string? sessionState)
    {
        return TerminalDisplayHelper.FormatSessionState(sessionState);
    }

    private static string GetTerminationReasonText(string? terminationReason)
    {
        return TerminalDisplayHelper.FormatTerminationReason(terminationReason);
    }

    private static string GetLatestActivityText(TerminalRecordState record)
    {
        var label = TerminalDisplayHelper.FormatRelativeTime(record.LastActivityAt ?? record.WaitingForInputSince ?? record.StartedAt);
        return string.IsNullOrWhiteSpace(label) ? string.Empty : $"最近活跃 {label}";
    }

    private static string GetStartedAtText(TerminalRecordState record)
    {
        var label = TerminalDisplayHelper.FormatAbsoluteTime(record.StartedAt);
        return string.IsNullOrWhiteSpace(label) ? string.Empty : $"开始于 {label}";
    }
}