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
    #region Keyboard & Input Handling

    private void HandleTextareaFocus(FocusEventArgs e)
    {
        _isFocused = true;
        StateHasChanged();
    }

    private void HandleTextareaBlur(FocusEventArgs e)
    {
        _isFocused = false;
        StateHasChanged();
    }

    private async Task HandleKeyDownAsync(KeyboardEventArgs e)
    {
        if (_isComposingInput)
        {
            return;
        }

        if (_showSlashSkillPicker && e.Key == "Escape")
        {
            CloseSlashSkillPicker();
            StateHasChanged();
            return;
        }

        if (string.Equals(e.Key, "Escape", StringComparison.OrdinalIgnoreCase)
            && CanCancelCurrentRun)
        {
            if (_isEscCancelArmed)
            {
                await DisarmEscCancelAsync();
                await HandleCancelAsync();
            }
            else
            {
                await ArmEscCancelAsync();
            }

            return;
        }

        if (_showSlashSkillPicker)
        {
            if (e.Key == "ArrowDown")
            {
                MoveActiveSlashSkill(1);
                StateHasChanged();
                return;
            }

            if (e.Key == "ArrowUp")
            {
                MoveActiveSlashSkill(-1);
                StateHasChanged();
                return;
            }

            if (e.Key == "Enter")
            {
                if (!FilteredSlashSkills.Any())
                {
                    CloseSlashSkillPicker();
                    StateHasChanged();
                }
                else
                {
                    await TrySelectActiveSlashSkillAsync();
                    return;
                }
            }
        }

        if (e.Key != "Enter") return;

        if (IsInteractionBlockingSend)
        {
            return;
        }

        if (IsTerminalWaitingForInput)
        {
            if (!e.ShiftKey)
            {
                await HandleSendAsync();
            }

            return;
        }

        var shouldSend = _enterToSend ? !e.ShiftKey : e.CtrlKey;
        if (shouldSend && !_showSlashSkillPicker)
        {
            await HandleSendAsync();
        }
    }

    private Task HandleInputChanged(ChangeEventArgs e)
    {
        var nextValue = e.Value?.ToString() ?? string.Empty;

        if (LooksLikeSlashSkillSearch(nextValue))
        {
            nextValue = nextValue.Replace("\r", string.Empty).Replace("\n", string.Empty);
            _selectedSlashSkill = null;
            _selectedQuickTool = null;
        }

        _content = nextValue;
        if (!_isComposingInput)
        {
            UpdateSlashSkillPicker();
        }
        return Task.CompletedTask;
    }

    private void HandleCompositionStart(EventArgs _)
    {
        _isComposingInput = true;
    }

    private Task HandleCompositionEnd(EventArgs _)
    {
        _isComposingInput = false;
        UpdateSlashSkillPicker();
        return Task.CompletedTask;
    }

    private async Task HandleCancelAsync()
    {
        await DisarmEscCancelAsync();

        if (HasActiveCliSession && SessionId.HasValue)
        {
            // 终止 CLI 只是打断了当前终端进程；这里继续取消本轮生成，避免后端流程再次拉起新的 CLI。
            ChatState.SetSessionGeneratingOptimistic(SessionId.Value, false);
            var terminateResult = await SignalR.TerminateCliSessionAsync(SessionId.Value);
            if (terminateResult?.State != null)
            {
                ChatState.UpdateCliExecSession(terminateResult.State);
            }
            await SignalR.CancelGenerationAsync(SessionId.Value);
            return;
        }

        if (OnCancel.HasDelegate)
        {
            await OnCancel.InvokeAsync();
        }
    }

    private async Task ArmEscCancelAsync()
    {
        _escCancelHintCts?.Cancel();
        _escCancelHintCts?.Dispose();

        _isEscCancelArmed = true;
        var cts = new CancellationTokenSource();
        _escCancelHintCts = cts;

        await InvokeAsync(StateHasChanged);

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(EscCancelHintDurationMs, cts.Token);
                await InvokeAsync(() =>
                {
                    if (_escCancelHintCts == cts)
                    {
                        _isEscCancelArmed = false;
                        StateHasChanged();
                    }
                });
            }
            catch (TaskCanceledException)
            {
                // 用户已确认取消或状态已回收，忽略超时任务。
            }
        });
    }

    private Task DisarmEscCancelAsync()
    {
        _escCancelHintCts?.Cancel();
        _escCancelHintCts?.Dispose();
        _escCancelHintCts = null;

        if (!_isEscCancelArmed)
        {
            return Task.CompletedTask;
        }

        _isEscCancelArmed = false;
        return InvokeAsync(StateHasChanged);
    }

    #endregion

}
