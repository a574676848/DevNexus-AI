using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;

namespace DevNexus.Client.Shared.Components.Chat;

/// <summary>
/// ChatContainer 组件的UI状态管理类
/// </summary>
public class ChatComponentState
{
    public List<ChatMessageDto> Messages { get; set; } = new();
    public List<BlockDto> CurrentBlocks { get; set; } = new();
    public bool IsArtifactOpen { get; set; } = false;
    public ArtifactDto? CurrentArtifact { get; set; }
    public List<ArtifactDto> CompletedArtifacts { get; set; } = new(); // 支持多个 Artifact
    public Guid CurrentMessageId { get; set; }
    public string SessionTitle { get; set; } = "新会话";
    public bool IsFirstMessage { get; set; } = true;
    public Guid LastLoadedSessionId { get; set; } = Guid.Empty; // 跟踪已加载的会话，修复切换不更新问题
    public bool IsLoadingMessages { get; set; } = false; // 防止并发加载
    public Guid? CurrentSessionProviderId { get; set; } = null; // 跟踪当前会话选择的 Provider ID
}