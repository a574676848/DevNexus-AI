using DevNexus.Client.Shared.Services.Chat;
using DevNexus.Shared.Enums;

namespace DevNexus.Client.Shared.Components.Chat;

public partial class ChatContainer
{
    private void HandleConnectionChanged(bool connected)
    {
        ChatState.SetRealtimeConnectionState(connected);

        if (!connected)
        {
            _needsConnectionRecovery = true;
            CancelConnectionRecovery();
            _ = InvokeAsync(StateHasChanged);
            return;
        }

        var currentSessionId = ChatState.CurrentSessionId;
        var recoveryAction = ChatConnectionRecoveryPolicy.ResolveConnectedAction(
            _needsConnectionRecovery,
            currentSessionId);
        _needsConnectionRecovery = false;

        switch (recoveryAction)
        {
            case ChatConnectionRecoveryAction.RenderOnly:
                _ = InvokeAsync(StateHasChanged);
                return;
            case ChatConnectionRecoveryAction.RecoverSession:
                ScheduleConnectionRecovery(currentSessionId);
                return;
            case ChatConnectionRecoveryAction.RefreshRuntime:
                ScheduleRuntimeRefresh(currentSessionId);
                return;
            default:
                _ = InvokeAsync(StateHasChanged);
                return;
        }
    }

    private void ScheduleConnectionRecovery(Guid sessionId)
    {
        CancelConnectionRecovery();
        _connectionRecoveryCts = new CancellationTokenSource();
        var cancellationToken = _connectionRecoveryCts.Token;

        _ = InvokeAsync(async () =>
        {
            try
            {
                await Task.Delay(ConnectionRecoveryDelay, cancellationToken);
                await RefreshRuntimeSnapshotAsync(
                    sessionId,
                    refreshCliExecSession: true,
                    refreshPendingInteractions: true);

                if (cancellationToken.IsCancellationRequested || ChatState.CurrentSessionId != sessionId)
                {
                    return;
                }

                if (ChatState.GetSessionRunPresentation(sessionId).RunState.IsGenerationLike())
                {
                    await LoadSessionMessagesAsync(sessionId);
                    return;
                }

                StateHasChanged();
            }
            catch (OperationCanceledException)
            {
                // 新的恢复动作或组件释放会取消旧任务。
            }
        });
    }
}
