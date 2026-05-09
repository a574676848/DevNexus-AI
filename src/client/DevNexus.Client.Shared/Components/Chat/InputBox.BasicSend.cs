using DevNexus.Client.Shared.Models;

namespace DevNexus.Client.Shared.Components.Chat;

/// <summary>
/// InputBox 基础发送链路。
/// 负责普通聊天输入的最小提交流程。
/// </summary>
public partial class InputBox
{
    private string BuildDevModePlaceholder()
    {
        return "输入消息，或键入 / 调用技能";
    }

    private async Task HandleSendAsync()
    {
        if (_isSending || string.IsNullOrWhiteSpace(_content))
        {
            return;
        }

        _isSending = true;

        try
        {
            var submission = new ChatComposerSubmission
            {
                Content = _content.Trim(),
                ProviderId = _selectedProviderId,
                EnableRag = _enableRag,
                SelectedSkillName = _selectedSlashSkill?.Name,
                Metadata = BuildComposerMetadata(new List<string>())
            };

            _content = string.Empty;
            RequestTextareaSync();

            if (OnSendWithProvider.HasDelegate)
            {
                await OnSendWithProvider.InvokeAsync(submission);
            }
            else if (OnSend.HasDelegate)
            {
                await OnSend.InvokeAsync(submission.Content);
            }
        }
        finally
        {
            _pendingSendRequest = null;
            _isSending = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private Task TryDispatchPendingSendAsync()
    {
        return Task.CompletedTask;
    }
}
