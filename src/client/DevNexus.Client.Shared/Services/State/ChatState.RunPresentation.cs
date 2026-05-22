using DevNexus.Client.Shared.Helpers;
using DevNexus.Client.Shared.Models;

namespace DevNexus.Client.Shared.Services.State;

public partial class ChatState
{
    public SessionRunPresentationState GetSessionRunPresentation(Guid sessionId)
    {
        var presentation = ChatSessionRunStateDisplay.GetPresentation(ResolveSessionRunState(sessionId));
        if (!_sessionRuntimes.TryGetValue(sessionId, out var runtime)
            || runtime.PrimaryPendingInteractionSummary == null)
        {
            return presentation;
        }

        var summary = runtime.PrimaryPendingInteractionSummary;
        presentation.Description = summary.Description;
        presentation.CompactLabel = summary.Label;
        presentation.ConnectionLabel = summary.Label;
        presentation.InputPlaceholder = summary.InputPlaceholder;
        presentation.BusyLabel = summary.Label;
        presentation.IsInteractionBlockingSend = summary.BlocksMessageSend;
        return presentation;
    }
}
