using DevNexus.Shared.DTOs;

namespace DevNexus.Client.Shared.Components.Chat;

/// <summary>
/// ChatContainer 生成失败事件固化逻辑。
/// </summary>
public partial class ChatContainer
{
    private const string GenerationFailedTitle = "生成失败";
    private const string DefaultGenerationFailureMessage = "生成响应时发生错误。";

    private async Task HandleGenerationFailedEventAsync(ServerEvent serverEvent)
    {
        var errorMessage = TryGetStringProperty(serverEvent.Data, "ErrorMessage", out var parsedErrorMessage)
            && !string.IsNullOrWhiteSpace(parsedErrorMessage)
                ? parsedErrorMessage
                : DefaultGenerationFailureMessage;

        await SolidifyGenerationFailedMessageAsync(serverEvent, errorMessage);

        ChatState.ClearToolActivity(serverEvent.SessionId);
        _generationTimeoutNotified = false;
        await NotificationService.ShowDeduplicatedAsync(
            GenerationFailedTitle,
            errorMessage,
            suppressSeconds: 8,
            dedupeKey: $"runtime-generation-failed:{serverEvent.SessionId}");
        ScheduleRuntimeRefresh(serverEvent.SessionId);
        StateHasChanged();
    }

    private async Task<ChatMessageDto?> SolidifyGenerationFailedMessageAsync(
        ServerEvent serverEvent,
        string errorMessage)
    {
        var solidifiedMessage = await MessageHandlingService.HandleGenerationErrorAsync(
            serverEvent.SessionId,
            errorMessage,
            _currentBlocks,
            _currentMessageId);

        if (solidifiedMessage != null)
        {
            UpsertMessage(solidifiedMessage);
            ClearBlocksWithCache();
            return solidifiedMessage;
        }

        ChatState.SetSessionGeneratingOptimistic(serverEvent.SessionId, false);
        return null;
    }

    private void UpsertMessage(ChatMessageDto message)
    {
        var existingIndex = _messages.FindIndex(item => item.Id == message.Id);
        if (existingIndex >= 0)
        {
            PreserveStreamingPresentation(message, _messages[existingIndex]);
            _messages[existingIndex] = message;
            return;
        }

        _messages.Add(message);
    }
}
