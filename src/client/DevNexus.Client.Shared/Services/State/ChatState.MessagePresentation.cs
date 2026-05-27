using DevNexus.Client.Shared.Helpers;
using DevNexus.Client.Shared.Models;
using DevNexus.Shared.Enums;

namespace DevNexus.Client.Shared.Services.State;

public partial class ChatState
{
    public SessionMessagePresentationState GetSessionMessagePresentation(Guid sessionId)
    {
        var runState = ResolveSessionRunState(sessionId);

        return new SessionMessagePresentationState
        {
            SessionId = sessionId,
            RunState = runState,
            IsStreaming = runState is ChatSessionRunState.Generating or ChatSessionRunState.Recovering,
            ShouldShowStatusIndicator = ShouldShowMessageStatusIndicator(runState),
            ShouldAnimateStatus = ShouldAnimateMessageStatus(runState),
            StatusText = ChatSessionRunStateDisplay.GetDescription(runState),
            CompactStatusText = ChatSessionRunStateDisplay.GetCompactLabel(runState)
        };
    }

    private static bool ShouldShowMessageStatusIndicator(ChatSessionRunState runState)
    {
        return runState is ChatSessionRunState.Generating
            or ChatSessionRunState.Recovering
            or ChatSessionRunState.Running
            or ChatSessionRunState.WaitingForInput
            or ChatSessionRunState.WaitingForPendingInput
            or ChatSessionRunState.WaitingForApproval;
    }

    private static bool ShouldAnimateMessageStatus(ChatSessionRunState runState)
    {
        return runState is ChatSessionRunState.Generating
            or ChatSessionRunState.Recovering
            or ChatSessionRunState.Running;
    }
}
