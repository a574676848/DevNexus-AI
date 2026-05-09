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
    #region Lifecycle

    protected override void OnParametersSet()
    {
        if (_isEscCancelArmed && !CanCancelCurrentRun)
        {
            _ = DisarmEscCancelAsync();
        }

        if (_lastObservedCancelableRun && !CanCancelCurrentRun)
        {
        }

        _lastObservedCancelableRun = CanCancelCurrentRun;

        if (!_lastObservedTerminalWaitingForInput && IsTerminalWaitingForInput)
        {
            _ = TriggerTerminalWaitingPulseAsync();
        }

        _lastObservedTerminalWaitingForInput = IsTerminalWaitingForInput;

        if (_lastSessionId == SessionId)
        {
            return;
        }

        _lastSessionId = SessionId;

        // 会话切换时清空输入态，避免上一个会话的附件/排队请求串入当前会话。
        _content = string.Empty;
        RequestTextareaSync();
        _pastedDocuments.Clear();
        _attachments.Clear();
        _pendingSendRequest = null;
        _selectedQuickTool = null;
        _selectedSlashSkill = null;
        _showQuickCommandModal = false;
        _showExpandedInputModal = false;
        CloseSlashSkillPicker();
        StopArtifactStatusPolling();
        _escCancelHintCts?.Cancel();
        _escCancelHintCts?.Dispose();
        _escCancelHintCts = null;
        _isEscCancelArmed = false;
        _isQueueExpanded = false;
        _lastObservedTerminalWaitingForInput = false;
        _terminalWaitingPulseActive = false;
        AdjustHeightForDocuments();
    }

    protected override async Task OnInitializedAsync()
    {
        SignalR.OnArtifactStatusReceived += HandleArtifactStatusReceived;
        ComposerFileBridgeService.FileAssetQueued += HandleQueuedFileAssetAsync;
        await LoadSettingsAsync();
        await LoadAvailableSkillsAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            await InitializeDragDropAsync();
            await JS.InvokeVoidAsync("devnexus.registerSlashSkillKeydownInterceptor", _textareaRef);
            await FocusInputAsync();
        }

        if (_shouldSyncTextareaValue)
        {
            var moveCaretToEnd = _shouldMoveCaretToEndAfterSync;
            _shouldSyncTextareaValue = false;
            _shouldMoveCaretToEndAfterSync = false;
            await SyncTextareaValueAsync(moveCaretToEnd);
        }

        if (_shouldEnsureActiveSlashSkillVisible && _showSlashSkillPicker)
        {
            _shouldEnsureActiveSlashSkillVisible = false;
            try
            {
                await JS.InvokeVoidAsync("devnexus.ensureActiveOptionVisible", _slashSkillPickerListRef);
            }
            catch (Microsoft.JSInterop.JSException)
            {
                // 静态资源可能仍在旧缓存中，本次仅跳过滚动增强，避免输入框交互被中断
            }
        }
    }

    public void Dispose()
    {
        SignalR.OnArtifactStatusReceived -= HandleArtifactStatusReceived;
        ComposerFileBridgeService.FileAssetQueued -= HandleQueuedFileAssetAsync;
        StopArtifactStatusPolling();
        _escCancelHintCts?.Cancel();
        _escCancelHintCts?.Dispose();
        _dotNetRef?.Dispose();
    }

    private async Task TriggerTerminalWaitingPulseAsync()
    {
        var version = ++_terminalWaitingPulseVersion;
        _terminalWaitingPulseActive = true;
        await InvokeAsync(StateHasChanged);
        await Task.Delay(1400);

        if (version == _terminalWaitingPulseVersion)
        {
            _terminalWaitingPulseActive = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    #endregion

}
