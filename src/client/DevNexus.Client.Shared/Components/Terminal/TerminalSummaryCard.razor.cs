using DevNexus.Client.Shared.Models;
using Microsoft.AspNetCore.Components;
using DevNexus.Client.Shared.Abstractions;

namespace DevNexus.Client.Shared.Components.Terminal;

/// <summary>
/// 会话级终端摘要卡片。
/// </summary>
public partial class TerminalSummaryCard
{
    [Inject] private IChatState ChatState { get; set; } = default!;

    [Parameter] public Guid SessionId { get; set; }

    [Parameter] public int RecordCount { get; set; }

    [Parameter] public EventCallback OnOpenDetail { get; set; }

    private TerminalPresentationState? Presentation =>
        SessionId != Guid.Empty
            ? ChatState.GetTerminalPresentation(SessionId)
            : null;

    private async Task OpenDetailAsync()
    {
        if (OnOpenDetail.HasDelegate)
        {
            await OnOpenDetail.InvokeAsync();
        }
    }

    private string GetStatusLabel()
    {
        return Presentation?.StatusLabel ?? "未知";
    }

    private string GetMetaLine()
    {
        return Presentation?.MetaLine ?? "查看终端详情";
    }

    private string GetToneClass()
    {
        return Presentation?.ToneClass ?? "terminal-summary-card--neutral";
    }

    private string GetCardTitle()
    {
        return $"查看终端，当前{GetStatusLabel()}";
    }
}
