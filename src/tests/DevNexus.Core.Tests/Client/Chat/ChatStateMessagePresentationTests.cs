using DevNexus.Client.Shared.Services.State;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DevNexus.Core.Tests.Client.Chat;

public sealed class ChatStateMessagePresentationTests
{
    [Theory]
    [InlineData(ChatSessionRunState.Generating, true, true)]
    [InlineData(ChatSessionRunState.Recovering, true, true)]
    [InlineData(ChatSessionRunState.Running, false, true)]
    [InlineData(ChatSessionRunState.WaitingForInput, false, false)]
    [InlineData(ChatSessionRunState.WaitingForPendingInput, false, false)]
    [InlineData(ChatSessionRunState.WaitingForApproval, false, false)]
    [InlineData(ChatSessionRunState.Queued, false, false)]
    [InlineData(ChatSessionRunState.Idle, false, false)]
    public void GetSessionMessagePresentation_SeparatesStreamingFromStatusVisibility(
        ChatSessionRunState runState,
        bool expectedStreaming,
        bool expectedAnimated)
    {
        var sessionId = Guid.NewGuid();
        var chatState = new ChatState(NullLogger<ChatState>.Instance);

        chatState.SetSessionRuntime(sessionId, new ChatSessionRuntimeDto
        {
            RunState = runState
        });

        var presentation = chatState.GetSessionMessagePresentation(sessionId);

        presentation.IsStreaming.Should().Be(expectedStreaming);
        presentation.ShouldAnimateStatus.Should().Be(expectedAnimated);
        presentation.ShouldShowStatusIndicator.Should().Be(IsRuntimeStatusVisible(runState));
    }

    private static bool IsRuntimeStatusVisible(ChatSessionRunState runState)
    {
        return runState is ChatSessionRunState.Generating
            or ChatSessionRunState.Recovering
            or ChatSessionRunState.Running
            or ChatSessionRunState.WaitingForInput
            or ChatSessionRunState.WaitingForPendingInput
            or ChatSessionRunState.WaitingForApproval;
    }
}
