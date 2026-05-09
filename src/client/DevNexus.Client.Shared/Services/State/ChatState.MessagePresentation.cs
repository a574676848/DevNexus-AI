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
            StatusText = ChatSessionRunStateDisplay.GetDescription(runState),
            CompactStatusText = ChatSessionRunStateDisplay.GetCompactLabel(runState)
        };
    }
}
