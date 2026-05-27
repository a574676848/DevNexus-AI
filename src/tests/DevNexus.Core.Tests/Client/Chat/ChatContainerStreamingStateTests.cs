using System.Reflection;
using DevNexus.Client.Shared.Abstractions.Chat;
using DevNexus.Client.Shared.Components.Chat;
using DevNexus.Client.Shared.Services.Chat;
using DevNexus.Client.Shared.Services.State;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DevNexus.Core.Tests.Client.Chat;

public sealed class ChatContainerStreamingStateTests
{
    [Fact]
    public void ClearBlocksWithCache_ShouldResetCurrentStreamingMessageId()
    {
        var container = new ChatContainer();
        var messageId = Guid.NewGuid();
        var currentBlocks = new List<BlockDto>
        {
            new()
            {
                MessageId = messageId,
                BlockType = BlockType.TextDelta,
                Content = "迟到块"
            }
        };
        var blockIndexer = new BlockIndexer();
        blockIndexer.AddBlock(currentBlocks[0]);

        SetPrivateField(container, "_currentMessageId", messageId);
        SetPrivateField(container, "_currentBlocks", currentBlocks);
        SetPrivateField(container, "_blockIndexer", blockIndexer);
        SetPrivateField(container, "_streamingMessage", new ChatMessageDto { Id = messageId });

        InvokePrivateMethod(container, "ClearBlocksWithCache");

        GetPrivateField<Guid>(container, "_currentMessageId").Should().Be(Guid.Empty);
        GetPrivateField<List<BlockDto>>(container, "_currentBlocks").Should().BeEmpty();
        GetPrivateField<BlockIndexer>(container, "_blockIndexer").GetOrderedBlocks().Should().BeEmpty();
        GetField("_streamingMessage").GetValue(container).Should().BeNull();
    }

    [Fact]
    public async Task SolidifyGenerationFailedMessageAsync_ShouldAddErrorMessageAndClearStreamingState()
    {
        var container = new ChatContainer();
        var sessionId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var currentBlocks = new List<BlockDto>
        {
            new()
            {
                MessageId = messageId,
                SessionId = sessionId,
                BlockType = BlockType.TextDelta,
                Content = "已生成内容"
            }
        };
        var errorMessage = new ChatMessageDto
        {
            Id = messageId,
            ChatSessionId = sessionId,
            SenderType = ChatConstants.RoleAssistant,
            Content = "生成失败：模型异常"
        };

        SetPrivateField(container, "_currentMessageId", messageId);
        SetPrivateField(container, "_currentBlocks", currentBlocks);
        SetPrivateField(container, "_blockIndexer", new BlockIndexer());
        SetInjectedProperty(container, "MessageHandlingService", new StubMessageHandlingService(errorMessage));

        var result = await InvokePrivateAsync<ChatMessageDto?>(
            container,
            "SolidifyGenerationFailedMessageAsync",
            new ServerEvent { SessionId = sessionId, EventType = ServerEventType.GenerationFailed },
            "模型异常");

        result.Should().BeSameAs(errorMessage);
        GetPrivateField<List<ChatMessageDto>>(container, "_messages").Should().ContainSingle()
            .Which.Should().BeSameAs(errorMessage);
        GetPrivateField<List<BlockDto>>(container, "_currentBlocks").Should().BeEmpty();
        GetPrivateField<Guid>(container, "_currentMessageId").Should().Be(Guid.Empty);
    }

    [Fact]
    public void ResetStreamingStateForAcceptedUserMessage_ShouldKeepStreamingBlocks_WhenSessionIsGenerating()
    {
        var container = new ChatContainer();
        var sessionId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var chatState = new ChatState(NullLogger<ChatState>.Instance);
        var currentBlocks = new List<BlockDto>
        {
            new()
            {
                MessageId = messageId,
                SessionId = sessionId,
                BlockType = BlockType.TextDelta,
                Content = "生成中"
            }
        };
        chatState.SetCurrentSession(sessionId);
        chatState.SetSessionRuntime(sessionId, new ChatSessionRuntimeDto { RunState = ChatSessionRunState.Generating });

        SetInjectedProperty(container, "ChatState", chatState);
        SetPrivateField(container, "_currentMessageId", messageId);
        SetPrivateField(container, "_currentBlocks", currentBlocks);

        var reset = InvokePrivate<bool>(
            container,
            "ResetStreamingStateForAcceptedUserMessage",
            new ChatMessageDto
            {
                ChatSessionId = sessionId,
                SenderType = ChatConstants.RoleUser
            });

        reset.Should().BeFalse();
        GetPrivateField<List<BlockDto>>(container, "_currentBlocks").Should().ContainSingle();
        GetPrivateField<Guid>(container, "_currentMessageId").Should().Be(messageId);
    }

    [Fact]
    public void ResetStreamingStateForAcceptedUserMessage_ShouldClearStreamingState_WhenSessionIsIdle()
    {
        var container = new ChatContainer();
        var sessionId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var chatState = new ChatState(NullLogger<ChatState>.Instance);
        var currentBlocks = new List<BlockDto>
        {
            new()
            {
                MessageId = messageId,
                SessionId = sessionId,
                BlockType = BlockType.TextDelta,
                Content = "旧内容"
            }
        };
        chatState.SetCurrentSession(sessionId);

        SetInjectedProperty(container, "ChatState", chatState);
        SetPrivateField(container, "_currentMessageId", messageId);
        SetPrivateField(container, "_currentBlocks", currentBlocks);
        SetPrivateField(container, "_blockIndexer", new BlockIndexer());

        var reset = InvokePrivate<bool>(
            container,
            "ResetStreamingStateForAcceptedUserMessage",
            new ChatMessageDto
            {
                ChatSessionId = sessionId,
                SenderType = ChatConstants.RoleUser
            });

        reset.Should().BeTrue();
        GetPrivateField<List<BlockDto>>(container, "_currentBlocks").Should().BeEmpty();
        GetPrivateField<Guid>(container, "_currentMessageId").Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void FlushPendingBlocks_ShouldApplyBufferedBlocksBeforeTerminalEvent()
    {
        var container = new ChatContainer();
        var sessionId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var chatState = new ChatState(NullLogger<ChatState>.Instance);
        var pendingBlocks = new List<BlockDto>
        {
            new()
            {
                MessageId = messageId,
                SessionId = sessionId,
                BlockType = BlockType.TextDelta,
                Content = "最后一段"
            }
        };
        chatState.SetCurrentSession(sessionId);

        SetInjectedProperty(container, "ChatState", chatState);
        SetPrivateField(container, "_pendingBlocks", pendingBlocks);
        SetPrivateField(container, "_hasPendingBlockFlush", true);
        SetPrivateField(container, "_blockIndexer", new BlockIndexer());

        var flushed = InvokePrivate<bool>(container, "FlushPendingBlocks");

        flushed.Should().BeTrue();
        GetPrivateField<List<BlockDto>>(container, "_pendingBlocks").Should().BeEmpty();
        GetPrivateField<bool>(container, "_hasPendingBlockFlush").Should().BeFalse();
        GetPrivateField<List<BlockDto>>(container, "_currentBlocks").Should().ContainSingle()
            .Which.Content.Should().Be("最后一段");
        GetPrivateField<List<ChatMessageDto>>(container, "_messages").Should().ContainSingle()
            .Which.Content.Should().Be("最后一段");
    }

    private static void SetPrivateField<T>(ChatContainer container, string fieldName, T value)
    {
        GetField(fieldName).SetValue(container, value);
    }

    private static void SetInjectedProperty<T>(ChatContainer container, string propertyName, T value)
    {
        var property = typeof(ChatContainer)
            .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        property.Should().NotBeNull();
        property!.SetValue(container, value);
    }

    private static T GetPrivateField<T>(ChatContainer container, string fieldName)
    {
        var value = GetField(fieldName).GetValue(container);
        value.Should().BeOfType<T>();
        return (T)value!;
    }

    private static void InvokePrivateMethod(ChatContainer container, string methodName)
    {
        var method = typeof(ChatContainer)
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull();
        method!.Invoke(container, Array.Empty<object>());
    }

    private static T InvokePrivate<T>(
        ChatContainer container,
        string methodName,
        params object[] args)
    {
        var method = typeof(ChatContainer)
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull();
        var result = method!.Invoke(container, args);
        result.Should().BeOfType<T>();
        return (T)result!;
    }

    private static async Task<T> InvokePrivateAsync<T>(
        ChatContainer container,
        string methodName,
        params object[] args)
    {
        var method = typeof(ChatContainer)
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull();
        var taskObject = method!.Invoke(container, args);
        taskObject.Should().BeAssignableTo<Task<T>>();
        var task = (Task<T>)taskObject!;
        return await task;
    }

    private static FieldInfo GetField(string fieldName)
    {
        var field = typeof(ChatContainer)
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull();
        return field!;
    }

    private sealed class StubMessageHandlingService(ChatMessageDto? solidifiedErrorMessage) : IMessageHandlingService
    {
        private readonly ChatMessageDto? _solidifiedErrorMessage = solidifiedErrorMessage;

        public Task<List<ChatMessageDto>> LoadSessionMessagesAsync(Guid sessionId) => Task.FromResult(new List<ChatMessageDto>());

        public CliSessionStateDto? RestoreCliExecSession(Guid sessionId, IReadOnlyList<ChatMessageDto> messages) => null;

        public Task HandleMessageReceivedAsync(
            ChatMessageDto message,
            List<BlockDto> currentBlocks,
            List<ArtifactDto> completedArtifacts,
            ArtifactDto? currentArtifact) => Task.CompletedTask;

        public Task<(bool shouldGenerateTitle, bool shouldGenerateSmartTitle)> HandleGenerationCompleteAsync(
            Guid sessionId,
            int messageCount,
            bool isFirstMessage) => Task.FromResult((false, false));

        public Task<ChatMessageDto?> HandleGenerationErrorAsync(
            Guid sessionId,
            string errorMessage,
            List<BlockDto> currentBlocks,
            Guid currentMessageId) => Task.FromResult(_solidifiedErrorMessage);

        public Task<ChatMessageDto?> HandleGenerationCancelledAsync(
            Guid sessionId,
            List<BlockDto> currentBlocks,
            Guid currentMessageId) => Task.FromResult<ChatMessageDto?>(null);

        public List<Guid> RestoreGeneratingState(Guid sessionId, List<BlockDto> currentBlocks) => new();
    }
}
