using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using DevNexus.Client.Shared.Models;

namespace DevNexus.Client.Shared.Services.State;

/// <summary>
/// 聊天状态管理的会话内部状态能力。
/// </summary>
public partial class ChatState
{
    private class SessionChatState
    {
        public Guid SessionId { get; }
        public List<BlockDto> Blocks { get; } = new();
        public bool IsGeneratingOptimistic { get; private set; }
        public bool IsSwarmActive { get; set; }
        public ArtifactDto? CurrentArtifact { get; private set; }
        public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;
        public Guid? FocusedTerminalRecordId { get; set; }
        public List<QueuedChatMessageDto> QueuedMessages { get; } = new();
        public List<PendingInteractionDto> PendingInteractions { get; } = new();
        public AgentTurnEventsUpdatedDto? AgentTurnEvents { get; set; }

        public SessionChatState(Guid sessionId)
        {
            SessionId = sessionId;
        }

        public void AddBlock(BlockDto block)
        {
            Blocks.Add(block);
            LastActiveAt = DateTime.UtcNow;
        }

        public void SetGeneratingOptimistic(bool generating)
        {
            IsGeneratingOptimistic = generating;
            LastActiveAt = DateTime.UtcNow;
        }

        public void SetBlocks(IEnumerable<BlockDto> blocks)
        {
            Blocks.Clear();
            Blocks.AddRange(blocks);
            LastActiveAt = DateTime.UtcNow;
        }

        public void SetArtifact(ArtifactDto artifact)
        {
            CurrentArtifact = artifact;
            LastActiveAt = DateTime.UtcNow;
        }

        public void ClearBlocks()
        {
            Blocks.Clear();
            CurrentArtifact = null;
            IsSwarmActive = false;
            FocusedTerminalRecordId = null;
            IsGeneratingOptimistic = false;
            AgentTurnEvents = null;
            LastActiveAt = DateTime.UtcNow;
        }
    }

    private DevNexus.Client.Shared.Models.SidekickPaneKind ResolvePreferredSidekickPane(SessionChatState state)
    {
        if (state.IsSwarmActive)
        {
            return DevNexus.Client.Shared.Models.SidekickPaneKind.Swarm;
        }

        if (HasTerminalRecords(state.SessionId))
        {
            return DevNexus.Client.Shared.Models.SidekickPaneKind.ChatTerminal;
        }

        if (state.CurrentArtifact != null)
        {
            return DevNexus.Client.Shared.Models.SidekickPaneKind.Artifact;
        }

        return DevNexus.Client.Shared.Models.SidekickPaneKind.None;
    }
}
