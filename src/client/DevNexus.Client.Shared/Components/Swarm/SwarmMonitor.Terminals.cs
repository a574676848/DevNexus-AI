using DevNexus.Client.Shared.Models;

namespace DevNexus.Client.Shared.Components.Swarm;

public partial class SwarmMonitor
{
    private Guid ParsedSessionId => Guid.TryParse(SessionId, out var parsed) ? parsed : Guid.Empty;

    private IReadOnlyList<TerminalRecordState> SwarmTerminalRecords =>
        ParsedSessionId == Guid.Empty ? Array.Empty<TerminalRecordState>() : ChatState.GetTerminalRecords(ParsedSessionId);

    private TerminalRecordState? SelectedTerminalRecord =>
        ParsedSessionId == Guid.Empty ? null : ChatState.GetFocusedTerminalRecord(ParsedSessionId);

    private bool HasSwarmTerminalRecords => SwarmTerminalRecords.Count > 0;

    private IReadOnlyList<TerminalRecordState> GetSelectedPackageTerminalRecords()
    {
        if (SelectedPackage == null)
        {
            return Array.Empty<TerminalRecordState>();
        }

        return SwarmTerminalRecords
            .Where(record => string.Equals(record.PackageId, SelectedPackage.Id, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private Task HandleTerminalRecordSelected(Guid recordId)
    {
        if (ParsedSessionId != Guid.Empty)
        {
            ChatState.FocusTerminalRecord(ParsedSessionId, recordId, openSidekick: false);
        }

        return Task.CompletedTask;
    }

    private Task OpenTerminalModal()
    {
        if (ParsedSessionId != Guid.Empty)
        {
            ChatState.OpenTerminalModal();
        }

        return Task.CompletedTask;
    }
}
