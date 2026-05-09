using DevNexus.Client.Shared.Models;
using DevNexus.Client.Shared.Abstractions.Chat;
using DevNexus.Client.Shared.Abstractions;
using DevNexus.Client.Shared.Helpers;
using DevNexus.Client.Shared.Services.Storage;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System.Text.Json;

namespace DevNexus.Client.Shared.Components.Chat;

/// <summary>
/// 输入框组件 - 协调者模式，核心业务逻辑容器
/// </summary>
public partial class InputBox : IDisposable
{
    #region Dependencies

    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private IApiService ApiService { get; set; } = default!;
    [Inject] private ISessionManager SessionManager { get; set; } = default!;
    [Inject] private FileUploadService FileUploadService { get; set; } = default!;
    [Inject] private ISignalRService SignalR { get; set; } = default!;
    [Inject] private IChatState ChatState { get; set; } = default!;
    [Inject] private ISessionState SessionState { get; set; } = default!;
    [Inject] private INotificationService NotificationService { get; set; } = default!;
    [Inject] private IRemoteLogService RemoteLogService { get; set; } = default!;
    [Inject] private IComposerFileBridgeService ComposerFileBridgeService { get; set; } = default!;

    #endregion

    #region Parameters

    [Parameter] public EventCallback<ChatComposerSubmission> OnSendWithProvider { get; set; }
    [Parameter] public EventCallback<string> OnSend { get; set; }
    [Parameter] public EventCallback OnCancel { get; set; }
    [Parameter] public EventCallback<Guid?> OnProviderChanged { get; set; }
    [Parameter] public EventCallback OnToggleSidekick { get; set; }
    [Parameter] public EventCallback<Guid> OnCancelQueuedMessage { get; set; }
    [Parameter] public EventCallback OnClearQueuedMessages { get; set; }
    [Parameter] public bool IsDisabled { get; set; }
    [Parameter] public string? DisabledPlaceholder { get; set; }
    [Parameter] public Guid? SessionId { get; set; }
    [Parameter] public IReadOnlyList<QueuedChatMessageDto> QueuedMessages { get; set; } = Array.Empty<QueuedChatMessageDto>();

    #endregion

    /// <summary>
    /// 转发右侧侧栏切换事件。
    /// </summary>
    private Task ToggleSidekickAsync()
    {
        return OnToggleSidekick.InvokeAsync();
    }

    #region Computed Properties

    private bool HasPendingUploads => _pastedDocuments.Any(d =>
        !d.IsAssetOnlyContext &&
        (d.SmartDocument.Status == ParsingStatus.Processing ||
         d.SmartDocument.Status == ParsingStatus.Pending));

    private TerminalPresentationState? CurrentTerminalPresentation =>
        SessionId.HasValue && SessionId.Value != Guid.Empty
            ? ChatState.GetTerminalPresentation(SessionId.Value)
            : null;

    private bool HasActiveCliSession =>
        CurrentRuntime?.HasActiveCliSession == true
        || CurrentTerminalPresentation?.IsActive == true;

    private ChatSessionRuntimeDto? CurrentRuntime =>
        SessionId.HasValue && SessionId.Value != Guid.Empty
            ? ChatState.GetSessionRuntime(SessionId.Value)
            : null;

    private bool HasPendingApprovalInteraction =>
        CurrentRuntime?.PendingInteractionCount > 0
        && CurrentRuntime.PrimaryPendingInteractionKind == PendingInteractionKind.Approval;

    private bool HasPendingInputInteraction =>
        CurrentRuntime?.PendingInteractionCount > 0;

    private SessionRunPresentationState CurrentRunPresentation =>
        SessionId.HasValue && SessionId.Value != Guid.Empty
            ? ChatState.GetSessionRunPresentation(SessionId.Value)
            : ChatSessionRunStateDisplay.GetPresentation(ChatSessionRunState.Idle);

    private bool IsInteractionBlockingSend => CurrentRunPresentation.IsInteractionBlockingSend;

    private bool CanCancelCurrentRun => CurrentRunPresentation.CanCancel;

    private bool IsBusyWithoutCancel => CurrentRunPresentation.IsBusyWithoutCancel;

    private string CurrentBusyLabel =>
        HasPendingApprovalInteraction
            ? BuildPendingApprovalPlaceholder()
            : HasPendingInputInteraction
                ? BuildPendingInputPlaceholder()
                : CurrentRunPresentation.BusyLabel;

    private bool IsCliWaitingForInput =>
        CurrentRunPresentation.RunState == ChatSessionRunState.WaitingForInput
        || CurrentTerminalPresentation?.WaitingForInput == true;

    private bool HasActiveTerminalSession => HasActiveCliSession;

    private bool CanToggleSidekick =>
        ChatState.IsSidekickVisible
        || (SessionId.HasValue
            && SessionId.Value != Guid.Empty
            && (ChatState.IsSwarmActive(SessionId.Value)
                || ChatState.CurrentArtifact != null
                || (ChatState.CurrentTerminalRecords.Count > 0
                    && ChatState.CurrentFocusedTerminalRecord != null)));

    private bool IsTerminalWaitingForInput => IsCliWaitingForInput;

    private string? CurrentTerminalCommand => CurrentTerminalPresentation?.Command;

    private ChatSessionDto? CurrentSession =>
        SessionId.HasValue && SessionId.Value != Guid.Empty
            ? SessionState.Sessions.FirstOrDefault(session => session.Id == SessionId.Value)
            : null;

    private string SelectedToolbarToolIcon => _selectedQuickTool?.Icon ?? "fa-solid fa-terminal";

    private IReadOnlyList<SkillDto> FilteredSlashSkills => _filteredSlashSkills;

    private string CurrentInputPlaceholder => IsDisabled
        ? (string.IsNullOrWhiteSpace(DisabledPlaceholder) ? "连接中..." : DisabledPlaceholder)
        : _isSending
            ? "正在发送，请稍候..."
        : HasPendingApprovalInteraction
            ? BuildPendingApprovalPlaceholder()
        : HasPendingInputInteraction
            ? BuildPendingInputPlaceholder()
        : IsTerminalWaitingForInput
            ? BuildTerminalInputPlaceholder()
        : HasActiveTerminalSession
            ? BuildActiveTerminalPlaceholder()
                : GetCurrentSessionRunPlaceholder() is string runPlaceholder && !string.IsNullOrWhiteSpace(runPlaceholder)
                    ? runPlaceholder
                : _pendingSendRequest != null
                    ? "语义准备中，已排队自动发送"
                    : BuildDefaultPlaceholder();

    private string CurrentPendingOperationStatus => "附件解析完成后将自动发送当前消息。";

    private string CurrentSendLabel => _selectedQuickTool != null && _selectedSlashSkill != null
        ? "发送并应用工具/技能"
        : _selectedQuickTool != null
        ? "使用工具发送"
        : _selectedSlashSkill != null
        ? "使用技能发送"
        : (IsTerminalWaitingForInput ? "发送输入" : "发送");

    private int CurrentQueuedPendingCount => QueuedMessages.Count(message =>
        string.Equals(message.Status, "Pending", StringComparison.OrdinalIgnoreCase));

    private bool ShouldShowQueueStrip => CurrentQueuedPendingCount > 0 && !HasBlockingComposerBanner;

    private bool ShouldCollapseQueueStrip => IsTerminalWaitingForInput || HasDenseComposerContext;

    private bool HasBlockingComposerBanner => false;

    private bool HasDenseComposerContext => false;

    #endregion

}
