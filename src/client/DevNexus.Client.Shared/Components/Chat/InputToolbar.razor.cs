using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using DevNexus.Shared.Enums;

namespace DevNexus.Client.Shared.Components.Chat;

/// <summary>
/// 输入工具栏组件。
/// </summary>
public partial class InputToolbar
{
    [Parameter] public bool EnableRag { get; set; } = true;
    [Parameter] public AgentApprovalMode AgentApprovalMode { get; set; } = AgentApprovalMode.AskUser;
    [Parameter] public Guid? SelectedProviderId { get; set; }
    [Parameter] public string? SelectedProviderName { get; set; }
    [Parameter] public string? SelectedToolName { get; set; }
    [Parameter] public string? SelectedToolDescription { get; set; }
    [Parameter] public string SelectedToolIcon { get; set; } = "fa-solid fa-terminal";
    [Parameter] public string? SelectedSkillName { get; set; }
    [Parameter] public string? SelectedSkillDescription { get; set; }
    [Parameter] public bool IsUploading { get; set; }
    [Parameter] public bool IsSidekickVisible { get; set; }
    [Parameter] public bool CanToggleSidekick { get; set; }
    [Parameter] public EventCallback<bool> EnableRagChanged { get; set; }
    [Parameter] public EventCallback<AgentApprovalMode> AgentApprovalModeChanged { get; set; }
    [Parameter] public EventCallback<(Guid? ProviderId, string? ProviderName)> OnProviderChanged { get; set; }
    [Parameter] public EventCallback OnOpenQuickCommand { get; set; }
    [Parameter] public EventCallback<InputFileChangeEventArgs> OnFileSelected { get; set; }
    [Parameter] public EventCallback OnClearSelectedTool { get; set; }
    [Parameter] public EventCallback OnClearSelectedSkill { get; set; }
    [Parameter] public EventCallback OnToggleSidekick { get; set; }

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private InputFile? _fileInputRef;
    private bool HasActiveSelection => !string.IsNullOrWhiteSpace(SelectedToolName) || !string.IsNullOrWhiteSpace(SelectedSkillName);
    private string ApprovalModeTitle => AgentApprovalMode switch
    {
        AgentApprovalMode.AskUser => "审批模式：始终询问用户",
        AgentApprovalMode.AgentDecides => "审批模式：Agent 自主决策",
        AgentApprovalMode.FullAccess => "审批模式：完全放权",
        _ => "审批模式"
    };

    private string ApprovalModeIcon => AgentApprovalMode switch
    {
        AgentApprovalMode.AskUser => "fa-solid fa-hand",
        AgentApprovalMode.AgentDecides => "fa-solid fa-scale-balanced",
        AgentApprovalMode.FullAccess => "fa-solid fa-unlock",
        _ => "fa-solid fa-shield-halved"
    };

    /// <summary>
    /// 触发文件选择对话框
    /// </summary>
    private async Task TriggerFileUpload()
    {
        if (_fileInputRef != null && !IsUploading)
        {
            // 先给出一个轻量的按下反馈，再打开系统文件选择器。
            await InvokeAsync(StateHasChanged);
            await Task.Yield();
            await JS.InvokeVoidAsync("triggerFileInput", _fileInputRef.Element);
        }
    }

    /// <summary>
    /// 文件选择后触发事件
    /// </summary>
    private async Task HandleFileSelected(InputFileChangeEventArgs e)
    {
        await OnFileSelected.InvokeAsync(e);
    }

    /// <summary>
    /// 清除当前选中的工具。
    /// </summary>
    private async Task ClearSelectedToolAsync()
    {
        await OnClearSelectedTool.InvokeAsync();
    }

    /// <summary>
    /// 清除当前选中的技能。
    /// </summary>
    private async Task ClearSelectedSkillAsync()
    {
        await OnClearSelectedSkill.InvokeAsync();
    }

    /// <summary>
    /// 打开工具箱。
    /// </summary>
    private async Task OpenQuickCommandAsync()
    {
        await OnOpenQuickCommand.InvokeAsync();
    }

    /// <summary>
    /// 切换知识库引用。
    /// </summary>
    private async Task ToggleRagAsync()
    {
        EnableRag = !EnableRag;
        await EnableRagChanged.InvokeAsync(EnableRag);
    }

    /// <summary>
    /// 按 Codex 三档审批语义循环切换。
    /// </summary>
    private async Task ToggleApprovalModeAsync()
    {
        AgentApprovalMode = AgentApprovalMode switch
        {
            AgentApprovalMode.AskUser => AgentApprovalMode.AgentDecides,
            AgentApprovalMode.AgentDecides => AgentApprovalMode.FullAccess,
            _ => AgentApprovalMode.AskUser
        };

        await AgentApprovalModeChanged.InvokeAsync(AgentApprovalMode);
    }

    /// <summary>
    /// 切换侧栏显示状态。
    /// </summary>
    private async Task ToggleSidekickAsync()
    {
        await OnToggleSidekick.InvokeAsync();
    }

    /// <summary>
    /// 处理模型提供者选择。
    /// </summary>
    private async Task HandleProviderChangedAsync((Guid? ProviderId, string? ProviderName) provider)
    {
        await OnProviderChanged.InvokeAsync(provider);
    }
}
