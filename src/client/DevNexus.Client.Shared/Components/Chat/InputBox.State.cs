using DevNexus.Client.Shared.Models;
using DevNexus.Client.Shared.Abstractions.Chat;
using DevNexus.Client.Shared.Abstractions;
using DevNexus.Client.Shared.Services.Storage;
using DevNexus.Shared.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System.Text.Json;

namespace DevNexus.Client.Shared.Components.Chat;

public partial class InputBox
{
    #region State Fields

    private Components.Shared.ResizablePanel? _resizablePanel;
    private ElementReference _textareaRef;
    private ElementReference _slashSkillPickerListRef;
    private DotNetObjectReference<InputBox>? _dotNetRef;

    // 核心状态
    private string _content = "";
    private List<PastedDocument> _pastedDocuments = new();
    private List<string> _attachments = new();

    // 配置状态
    private Guid? _selectedProviderId;
    private string? _selectedProviderName;
    private bool _enableRag = true;
    private bool _enterToSend = true;

    // UI 状态
    private bool _showQuickCommandModal;
    private bool _showExpandedInputModal;
    private bool _isUploading;
    private bool _isSending;
    private int _pastedDocumentCounter;
    private bool _isFocused; // 输入框聚焦状态 - 用于显示绿色下边框
    private bool _isLoadingSkills;
    private List<SkillDto> _availableSkills = new();
    private List<SkillDto> _filteredSlashSkills = new();
    private QuickCommand? _selectedQuickTool;
    private SkillDto? _selectedSlashSkill;
    private bool _showSlashSkillPicker;
    private string _slashSkillQuery = string.Empty;
    private string? _activeSlashSkillName;
    private bool _shouldEnsureActiveSlashSkillVisible;
    private bool _isComposingInput;
    private bool _shouldSyncTextareaValue;
    private bool _shouldMoveCaretToEndAfterSync;
    private PendingSendRequest? _pendingSendRequest;
    private CancellationTokenSource? _artifactStatusPollingCts;
    private Task? _artifactStatusPollingTask;
    private Guid? _lastSessionId;
    private bool _lastObservedCancelableRun;
    private bool _lastObservedTerminalWaitingForInput;
    private bool _terminalWaitingPulseActive;
    private int _terminalWaitingPulseVersion;
    private bool _isEscCancelArmed;
    private CancellationTokenSource? _escCancelHintCts;
    private bool _isQueueExpanded;

    #endregion

    #region Constants

    private const int MaxLinesThreshold = 20;
    private const int MaxCharsThreshold = 1500;
    private const long MaxFileSize = 20 * 1024 * 1024; // 20MB
    private const int SlashSkillMaxResults = 8;
    private const int ArtifactStatusPollIntervalMs = 2500;
    private const int ArtifactStatusPollBurstSize = 5;
    private const int EscCancelHintDurationMs = 2000;

    private sealed class PendingSendRequest
    {
        public string Content { get; init; } = string.Empty;
        public Guid? ProviderId { get; init; }
        public bool EnableRag { get; init; } = true;
        public string? SelectedSkillName { get; init; }
        public Dictionary<string, object>? Metadata { get; init; }
    }

    #endregion
}
