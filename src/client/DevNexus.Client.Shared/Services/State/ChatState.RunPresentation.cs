using DevNexus.Client.Shared.Helpers;
using DevNexus.Client.Shared.Models;

namespace DevNexus.Client.Shared.Services.State;

public partial class ChatState
{
    public SessionRunPresentationState GetSessionRunPresentation(Guid sessionId)
    {
        return ChatSessionRunStateDisplay.GetPresentation(ResolveSessionRunState(sessionId));
    }
}
