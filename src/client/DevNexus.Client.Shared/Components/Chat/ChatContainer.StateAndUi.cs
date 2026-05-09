using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using DevNexus.Client.Shared.Models;
using DevNexus.Client.Shared.Services.UI;
using Microsoft.JSInterop;

namespace DevNexus.Client.Shared.Components.Chat;

/// <summary>
/// 聊天容器组件的排队、状态同步与基础 UI 交互。
/// </summary>
public partial class ChatContainer
{
    #region 排队消息状态处理

    private void HandleQueuedMessagesReceived(List<QueuedChatMessageDto> messages)
    {
        if (messages == null)
        {
            return;
        }

        var sessionId = messages.FirstOrDefault()?.SessionId ?? ChatState.CurrentSessionId;
        if (sessionId == Guid.Empty)
        {
            return;
        }

        ChatState.SetQueuedMessages(sessionId, messages);
        ScheduleRuntimeRefresh(sessionId);
    }

    #endregion

    #region UI 交互

    private Task HandleProviderChangedAsync(Guid? providerId)
    {
        _currentSessionProviderId = providerId;
        return Task.CompletedTask;
    }

    private Task HandleToggleSidekickAsync()
    {
        var sessionId = ChatState.CurrentSessionId;
        if (sessionId == Guid.Empty)
        {
            return Task.CompletedTask;
        }

        if (ChatState.IsSidekickVisible)
        {
            ChatState.ToggleSidekick(false);
            return Task.CompletedTask;
        }

        if (ChatState.IsSwarmActive(sessionId))
        {
            ChatState.OpenSwarmSidekick(sessionId);
            return Task.CompletedTask;
        }

        if (ChatState.CurrentTerminalRecords.Count > 0 && ChatState.CurrentFocusedTerminalRecord != null)
        {
            ChatState.OpenChatTerminalSidekick(sessionId, ChatState.CurrentFocusedTerminalRecord.RecordId);
            return Task.CompletedTask;
        }

        if (ChatState.CurrentArtifact != null)
        {
            ChatState.OpenArtifactSidekick(sessionId);
        }

        return Task.CompletedTask;
    }

    [JSInvokable]
    public void OnOpenArtifact(string type, string language, string title, string content)
    {
        if (_currentArtifact == null || _currentArtifact.Type != language)
        {
            _currentArtifact = new ArtifactDto
            {
                ArtifactId = Guid.NewGuid(),
                Name = title ?? $"{language?.ToUpper()} 代码",
                Type = language ?? "code",
                Content = content ?? string.Empty,
                SessionId = ChatState.CurrentSessionId,
                MessageId = _currentMessageId
            };
        }
        else
        {
            _currentArtifact.Content = content ?? string.Empty;
        }

        ChatState.SetArtifact(_currentArtifact);
        InvokeAsync(StateHasChanged);
    }

    private void HandleArtifactClose()
    {
        _isArtifactOpen = false;
        ChatState.ToggleSidekick(false);
        StateHasChanged();
    }

    private async Task CreateNewSessionAsync()
    {
        try
        {
            const string BlankSessionTitle = "新会话";
            var blankSession = SessionState.Sessions
                .FirstOrDefault(s => s.Title == BlankSessionTitle && s.MessageCount == 0);

            if (blankSession != null)
            {
                ChatState.SetCurrentSession(blankSession.Id);
                _sessionTitle = blankSession.Title;
                _isFirstMessage = true;
                _messages.Clear();
                NavigationManager.NavigateTo($"/chat/{blankSession.Id}", replace: true);
                return;
            }

            var session = await SessionManager.CreateSessionAsync(BlankSessionTitle);
            if (session != null)
            {
                ChatState.SetCurrentSession(session.Id);
                _sessionTitle = session.Title;
                _isFirstMessage = true;
                _messages.Clear();
                NavigationManager.NavigateTo($"/chat/{session.Id}", replace: true);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"创建会话失败: {ex.Message}");
        }
    }

    private bool HasOtherGeneratingSession()
    {
        return StateSynchronizationService.HasOtherGeneratingSession(ChatState.CurrentSessionId);
    }

    private void ReturnToGeneratingSession()
    {
        var sessionId = StateSynchronizationService.GetGeneratingSessionId(ChatState.CurrentSessionId);
        if (sessionId.HasValue)
        {
            NavigationManager.NavigateTo($"/chat/{sessionId.Value}");
        }
    }

    #endregion

    #region 状态同步

    private void HandleStateChanged()
    {
        _ = InvokeAsync(HandleStateChangedAsync);
    }

    private async Task HandleStateChangedAsync()
    {
        var shouldRender = false;

        if (_isArtifactOpen != ChatState.IsSidekickVisible)
        {
            _isArtifactOpen = ChatState.IsSidekickVisible;
            shouldRender = true;
        }

        if (_isArtifactOpen && ChatState.CurrentArtifact != null)
        {
            if (_currentArtifact == null || _currentArtifact.ArtifactId != ChatState.CurrentArtifact.ArtifactId)
            {
                _currentArtifact = ChatState.CurrentArtifact;
                shouldRender = true;
            }
        }

        if (ChatState.CurrentSessionId != _lastLoadedSessionId)
        {
            if (ChatState.CurrentSessionId == Guid.Empty)
            {
                _messages.Clear();
                _currentBlocks.Clear();
                _sessionTitle = "新会话";
                _isFirstMessage = true;
                _lastLoadedSessionId = Guid.Empty;
                shouldRender = true;
            }
            else
            {
                await LoadSessionMessagesAsync(ChatState.CurrentSessionId);
                return;
            }
        }
        else if (ChatState.CurrentSessionId != Guid.Empty && CurrentRunPresentation.RunState.IsGenerationLike())
        {
            if (_currentBlocks.Count == 0 && ChatState.CurrentBlocks.Any())
            {
                var idsToRemove = MessageHandlingService.RestoreGeneratingState(
                    ChatState.CurrentSessionId, _currentBlocks);
                foreach (var id in idsToRemove)
                {
                    _messages.RemoveAll(m => m.Id == id);
                }

                shouldRender = true;
            }
        }

        if (shouldRender)
        {
            await InvokeAsync(StateHasChanged);
        }
    }

    #endregion

    #region 标题管理

    private async Task GenerateSessionTitleAsync()
    {
        try
        {
            var firstUserMessage = _messages.FirstOrDefault(m => ChatConstants.IsUserSender(m.SenderType));
            if (firstUserMessage == null)
            {
                return;
            }

            var content = firstUserMessage.Content ?? string.Empty;
            var newTitle = content.Length > 30 ? content[..30] + "..." : content;
            newTitle = newTitle.Replace("\n", " ").Replace("\r", string.Empty).Trim();

            if (string.IsNullOrEmpty(newTitle))
            {
                newTitle = "新会话";
            }

            await UpdateSessionTitleAsync(newTitle);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"生成会话标题失败: {ex.Message}");
        }
    }

    private async Task GenerateSmartTitleAsync(int messageCount)
    {
        try
        {
            if (messageCount < 2)
            {
                return;
            }

            var newTitle = await SessionManager.GenerateSmartTitleAsync(
                ChatState.CurrentSessionId, _sessionTitle);

            if (!string.IsNullOrEmpty(newTitle))
            {
                _sessionTitle = newTitle;
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"智能生成标题失败: {ex.Message}");
        }
    }

    private async Task UpdateSessionTitleAsync(string newTitle)
    {
        try
        {
            await SessionManager.UpdateSessionTitleAsync(ChatState.CurrentSessionId, newTitle);
            _sessionTitle = newTitle;
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"更新会话标题失败: {ex.Message}");
        }
    }

    #endregion
}
