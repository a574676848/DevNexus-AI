using DevNexus.Client.Shared.Services.Chat;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Client.Chat;

public sealed class ChatConnectionRecoveryPolicyTests
{
    [Fact]
    public void ResolveConnectedAction_ShouldRenderOnly_WhenNoCurrentSession()
    {
        var action = ChatConnectionRecoveryPolicy.ResolveConnectedAction(
            needsConnectionRecovery: true,
            currentSessionId: Guid.Empty);

        action.Should().Be(ChatConnectionRecoveryAction.RenderOnly);
    }

    [Fact]
    public void ResolveConnectedAction_ShouldRecoverSession_AfterDisconnect()
    {
        var action = ChatConnectionRecoveryPolicy.ResolveConnectedAction(
            needsConnectionRecovery: true,
            currentSessionId: Guid.NewGuid());

        action.Should().Be(ChatConnectionRecoveryAction.RecoverSession);
    }

    [Fact]
    public void ResolveConnectedAction_ShouldRefreshRuntime_OnInitialConnection()
    {
        var action = ChatConnectionRecoveryPolicy.ResolveConnectedAction(
            needsConnectionRecovery: false,
            currentSessionId: Guid.NewGuid());

        action.Should().Be(ChatConnectionRecoveryAction.RefreshRuntime);
    }
}
