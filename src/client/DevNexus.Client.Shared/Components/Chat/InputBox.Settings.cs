using DevNexus.Client.Shared.Models;
using DevNexus.Client.Shared.Abstractions.Chat;
using DevNexus.Client.Shared.Abstractions;
using DevNexus.Client.Shared.Services.Storage;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System.Text.Json;

namespace DevNexus.Client.Shared.Components.Chat;

public partial class InputBox
{
    #region Settings Persistence

    private async Task LoadSettingsAsync()
    {
        try
        {
            var savedRagState = await JS.InvokeAsync<string>("localStorage.getItem", "devnexus_rag_enabled");
            if (!string.IsNullOrEmpty(savedRagState))
                _enableRag = savedRagState.Equals("true", StringComparison.OrdinalIgnoreCase);

            var savedEnterMode = await JS.InvokeAsync<string>("localStorage.getItem", "devnexus_enter_to_send");
            if (!string.IsNullOrEmpty(savedEnterMode))
                _enterToSend = savedEnterMode.Equals("true", StringComparison.OrdinalIgnoreCase);

            var savedApprovalMode = await JS.InvokeAsync<string>("localStorage.getItem", "devnexus_agent_approval_mode");
            if (Enum.TryParse<AgentApprovalMode>(savedApprovalMode, ignoreCase: true, out var approvalMode))
                _agentApprovalMode = approvalMode;
        }
        catch { /* 使用默认值 */ }
    }

    private async Task SaveSettingAsync(string key, string value)
    {
        try
        {
            await JS.InvokeVoidAsync("localStorage.setItem", key, value);
        }
        catch { /* 忽略 */ }
    }

    #endregion

    #region Event Handlers - From Child Components

    private async Task OnEnableRagChanged(bool value)
    {
        _enableRag = value;
        await SaveSettingAsync("devnexus_rag_enabled", value.ToString().ToLower());
    }

    private async Task OnAgentApprovalModeChanged(AgentApprovalMode value)
    {
        _agentApprovalMode = value;
        await SaveSettingAsync("devnexus_agent_approval_mode", value.ToString());
    }

    private async Task OnEnterToSendChanged(bool value)
    {
        _enterToSend = value;
        await SaveSettingAsync("devnexus_enter_to_send", value.ToString().ToLower());
    }

    private void OnHeightChanged(double height)
    {
        // 高度变化由 ResizablePanel 内部处理
    }

    #endregion

    #region SignalR Handlers

    private void HandleArtifactStatusReceived(ArtifactStatusDto status)
    {
        InvokeAsync(async () =>
        {
            var targetDoc = _pastedDocuments.FirstOrDefault(d => d.SmartDocument.TraceId == status.TraceId);
            if (targetDoc == null) return;
            var failedMessage = ApplyArtifactStatusToDocument(targetDoc, status);
            if (!string.IsNullOrWhiteSpace(failedMessage))
            {
                await NotificationService.ShowAsync("解析失败", $"文件 {targetDoc.FileName} 解析失败：{failedMessage}");
            }

            await TryDispatchPendingSendAsync();
            EnsureArtifactStatusPolling();
            StateHasChanged();
        });
    }

    #endregion

}

