using Microsoft.AspNetCore.Components;

namespace DevNexus.Client.Shared.Components.Chat;

/// <summary>
/// 输入操作按钮组件 - 包含发送按钮和快捷键切换
/// </summary>
public partial class InputActions
{
    private bool _isSendingClick;

    [Parameter] public bool IsBusy { get; set; }
    [Parameter] public bool CanCancel { get; set; }
    [Parameter] public bool IsBlocked { get; set; }
    [Parameter] public bool IsDisabled { get; set; }
    [Parameter] public bool HasContent { get; set; }
    [Parameter] public bool HasPastedDocuments { get; set; }
    [Parameter] public bool HasPendingUploads { get; set; }
    [Parameter] public bool IsSending { get; set; }
    [Parameter] public bool EnterToSend { get; set; } = true;
    [Parameter] public string SendLabel { get; set; } = "发送";
    [Parameter] public string CancelLabel { get; set; } = "停止";
    [Parameter] public string BusyLabel { get; set; } = "处理中...";
    [Parameter] public EventCallback OnSend { get; set; }
    [Parameter] public EventCallback OnCancel { get; set; }
    [Parameter] public EventCallback<bool> EnterToSendChanged { get; set; }

    private bool IsSendDisabled =>
        IsDisabled || IsSending || _isSendingClick || IsBlocked || (!HasContent && !HasPastedDocuments);

    private string SendButtonTooltip => HasPendingUploads
        ? "语义准备中，点击后会自动排队发送"
        : IsSending
            ? "消息正在发送"
        : IsBlocked
            ? BusyLabel
        : (EnterToSend ? $"{SendLabel} (Enter)" : $"{SendLabel} (Ctrl+Enter)");

    /// <summary>
    /// 切换快捷键模式（Enter 发送 vs Ctrl+Enter 发送）
    /// </summary>
    private async Task ToggleEnterMode()
    {
        EnterToSend = !EnterToSend;
        await EnterToSendChanged.InvokeAsync(EnterToSend);
    }

    /// <summary>
    /// 发送按钮点击处理，先给出即时反馈，再交给外层处理实际发送逻辑。
    /// </summary>
    private async Task HandleSendClickAsync()
    {
        if (IsSendDisabled)
        {
            return;
        }

        _isSendingClick = true;
        await InvokeAsync(StateHasChanged);
        await Task.Yield();

        try
        {
            await OnSend.InvokeAsync();
        }
        finally
        {
            _isSendingClick = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    /// <summary>
    /// 取消按钮点击处理，保持与发送按钮一致的即时反馈节奏。
    /// </summary>
    private async Task HandleCancelClickAsync()
    {
        await InvokeAsync(StateHasChanged);
        await Task.Yield();
        await OnCancel.InvokeAsync();
    }
}
