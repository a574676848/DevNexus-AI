using DevNexus.Core.Services.Chat;
using DevNexus.Domain.Entities;
using DevNexus.Shared.Constants;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// 会话统一运行态解析器测试。
/// </summary>
public sealed class ChatSessionRuntimeResolverTests
{
    /// <summary>
    /// 已存在进行中的助手消息时，新输入必须排队，避免同一会话并发生成导致消息乱序。
    /// </summary>
    [Fact]
    public void Resolve_ShouldQueue_WhenAssistantMessageIsInProgress()
    {
        var snapshot = ChatSessionRuntimeResolver.Resolve(
            Array.Empty<PendingInteraction>(),
            cliSnapshot: null,
            queuedCount: 0,
            latestAssistantMessage: new ChatMessage
            {
                Status = ChatConstants.StatusInProgress
            });

        snapshot.RunState.Should().Be(ChatSessionRunState.Generating);
        snapshot.ExecutionDecision.Should().Be(DevNexus.Domain.Enums.ChatExecutionDecision.Queued);
    }
}
