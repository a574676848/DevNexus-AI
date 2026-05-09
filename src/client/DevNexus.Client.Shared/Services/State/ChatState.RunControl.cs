using DevNexus.Client.Shared.Helpers;
using DevNexus.Client.Shared.Models;

namespace DevNexus.Client.Shared.Services.State;

public partial class ChatState
{
    public SessionRunControlState GetSessionRunControl(Guid sessionId)
    {
        return ChatSessionRunStateDisplay.GetControl(ResolveSessionRunState(sessionId));
    }
}
