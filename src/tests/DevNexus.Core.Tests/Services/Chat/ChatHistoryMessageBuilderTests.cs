using DevNexus.Core.Services.Chat;
using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Shared.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.ChatCompletion;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// 聊天历史消息构建器测试。
/// </summary>
public sealed class ChatHistoryMessageBuilderTests
{
    /// <summary>
    /// 内部修复提示不应进入模型历史，避免污染 Prompt 缓存断点。
    /// </summary>
    [Fact]
    public async Task AppendHistoryMessagesAsync_ShouldSkipInternalRepairPrompt()
    {
        var sessionId = Guid.NewGuid();
        var repository = new FakeChatMessageRepository(
        [
            CreateMessage(sessionId, ChatConstants.RoleUser, "用户原始问题"),
            CreateMessage(
                sessionId,
                ChatConstants.RoleUser,
                "内部修复提示",
                new Dictionary<string, object> { ["internalRepairPrompt"] = true }),
            CreateMessage(sessionId, ChatConstants.RoleAssistant, "助手正常回复")
        ]);
        var builder = new ChatHistoryMessageBuilder(
            repository,
            new FakeSessionSummaryService(),
            NullLogger<ChatHistoryMessageBuilder>.Instance);
        var chatHistory = new ChatHistory();

        var snapshot = await builder.AppendHistoryMessagesAsync(
            chatHistory,
            sessionId,
            providerId: null,
            tokenBudget: 8000,
            CancellationToken.None);

        chatHistory.Select(message => message.Content).Should().Equal(
            "用户原始问题",
            "助手正常回复");
        snapshot.Strategy.Should().Be(ChatHistoryGovernanceStrategies.DirectReplay);
        snapshot.SkippedInternalRepairPromptCount.Should().Be(1);
        snapshot.DirectMessageCount.Should().Be(2);
    }

    /// <summary>
    /// 未完成的助手消息不应进入模型历史，避免中断 turn 污染 replay 和 Prompt 缓存候选。
    /// </summary>
    [Fact]
    public async Task AppendHistoryMessagesAsync_ShouldSkipIncompleteAssistantMessages()
    {
        var sessionId = Guid.NewGuid();
        var repository = new FakeChatMessageRepository(
        [
            CreateMessage(sessionId, ChatConstants.RoleUser, "用户问题"),
            CreateMessage(sessionId, ChatConstants.RoleAssistant, "生成中的半截回复", status: ChatConstants.StatusInProgress),
            CreateMessage(sessionId, ChatConstants.RoleAssistant, "取消后的半截回复", status: ChatConstants.StatusCancelled),
            CreateMessage(sessionId, ChatConstants.RoleAssistant, "错误后的半截回复", status: ChatConstants.StatusError),
            CreateMessage(sessionId, ChatConstants.RoleAssistant, "完成回复", status: ChatConstants.StatusCompleted)
        ]);
        var builder = new ChatHistoryMessageBuilder(
            repository,
            new FakeSessionSummaryService(),
            NullLogger<ChatHistoryMessageBuilder>.Instance);
        var chatHistory = new ChatHistory();

        var snapshot = await builder.AppendHistoryMessagesAsync(
            chatHistory,
            sessionId,
            providerId: null,
            tokenBudget: 8000,
            CancellationToken.None);

        chatHistory.Select(message => message.Content).Should().Equal(
            "用户问题",
            "完成回复");
        snapshot.SkippedIncompleteAssistantMessageCount.Should().Be(3);
        snapshot.ReplayableMessageCount.Should().Be(2);
    }

    /// <summary>
    /// 历史回放必须清理控制序列，避免工具输出污染后续 Prompt。
    /// </summary>
    [Fact]
    public async Task AppendHistoryMessagesAsync_ShouldSanitizeReplayText()
    {
        var sessionId = Guid.NewGuid();
        var repository = new FakeChatMessageRepository(
        [
            CreateMessage(sessionId, ChatConstants.RoleUser, "\u001b[32m用户输入\u001b[0m\u0000"),
            CreateMessage(sessionId, ChatConstants.RoleAssistant, "助手回复\u0007")
        ]);
        var builder = new ChatHistoryMessageBuilder(
            repository,
            new FakeSessionSummaryService(),
            NullLogger<ChatHistoryMessageBuilder>.Instance);
        var chatHistory = new ChatHistory();

        await builder.AppendHistoryMessagesAsync(
            chatHistory,
            sessionId,
            providerId: null,
            tokenBudget: 8000,
            CancellationToken.None);

        chatHistory.Select(message => message.Content).Should().Equal(
            "用户输入",
            "助手回复");
    }

    /// <summary>
    /// 历史压缩后最近片段不应从孤立助手消息开始，避免形成缺少用户锚点的回放序列。
    /// </summary>
    [Fact]
    public async Task AppendHistoryMessagesAsync_ShouldTrimRecentSliceUntilUserAnchor()
    {
        var sessionId = Guid.NewGuid();
        var baseTime = DateTime.UtcNow.AddMinutes(-20);
        var messages = new List<ChatMessage>
        {
            CreateMessage(sessionId, ChatConstants.RoleUser, new string('早', 5000), createdAt: baseTime),
            CreateMessage(sessionId, ChatConstants.RoleAssistant, "早期回复", createdAt: baseTime.AddSeconds(1))
        };

        messages.AddRange(Enumerable.Range(0, 10).Select(index =>
        {
            var sender = index == 0
                ? ChatConstants.RoleAssistant
                : index % 2 == 0 ? ChatConstants.RoleAssistant : ChatConstants.RoleUser;
            var text = index == 0 ? "孤立助手消息" : $"最近消息 {index}";
            return CreateMessage(sessionId, sender, text, createdAt: baseTime.AddSeconds(index + 2));
        }));

        var builder = new ChatHistoryMessageBuilder(
            new FakeChatMessageRepository(messages),
            new FakeSessionSummaryService(),
            NullLogger<ChatHistoryMessageBuilder>.Instance);
        var chatHistory = new ChatHistory();

        var snapshot = await builder.AppendHistoryMessagesAsync(
            chatHistory,
            sessionId,
            providerId: Guid.NewGuid(),
            tokenBudget: 1000,
            CancellationToken.None);

        chatHistory.Select(message => message.Content).Should().NotContain("孤立助手消息");
        chatHistory.Select(message => message.Content).Should().Contain("最近消息 1");
        chatHistory.Skip(1).First().Role.Label.Should().Be("user");
        snapshot.Strategy.Should().Be(ChatHistoryGovernanceStrategies.SummaryWithRecentSlice);
        snapshot.SummaryMessageCount.Should().Be(1);
        snapshot.CompressedMessageCount.Should().BeGreaterThan(0);
        snapshot.RecentMessageCount.Should().BeGreaterThan(0);
        snapshot.CompressionIndex.HasIndex.Should().BeTrue();
        snapshot.CompressionIndex.CoveredMessageCount.Should().Be(snapshot.CompressedMessageCount);
        snapshot.CompressionIndex.SummaryFingerprint.Should().Be(PromptFingerprint.ComputeHash("摘要"));
        snapshot.CompressionIndex.TopicHints.Should().NotBeEmpty();
        snapshot.ConsumedTokens.Should().BeGreaterThan(0);
    }

    private static ChatMessage CreateMessage(
        Guid sessionId,
        string senderType,
        string text,
        Dictionary<string, object>? metadata = null,
        DateTime? createdAt = null,
        string status = ChatConstants.StatusCompleted)
    {
        var timestamp = createdAt ?? DateTime.UtcNow;
        return new ChatMessage
        {
            Id = Guid.NewGuid(),
            ChatSessionId = sessionId,
            SenderType = senderType,
            Content = new Dictionary<string, object> { ["text"] = text },
            Metadata = metadata,
            Status = status,
            CreatedAt = timestamp,
            UpdatedAt = timestamp
        };
    }

    private sealed class FakeSessionSummaryService : ISessionSummaryService
    {
        public Task<string?> GetOrCreateSummaryAsync(
            Guid sessionId,
            Guid providerId,
            string content,
            int targetChars,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<string?>("摘要");
        }
    }

    private sealed class FakeChatMessageRepository : IChatMessageRepository
    {
        private readonly IReadOnlyList<ChatMessage> _messages;

        public FakeChatMessageRepository(IReadOnlyList<ChatMessage> messages)
        {
            _messages = messages;
        }

        public Task<IReadOnlyList<ChatMessage>> ListRecentBySessionAsync(
            Guid sessionId,
            int takeCount,
            CancellationToken cancellationToken = default)
        {
            var result = _messages
                .Where(message => message.ChatSessionId == sessionId)
                .OrderByDescending(message => message.CreatedAt)
                .Take(takeCount)
                .ToList();

            return Task.FromResult<IReadOnlyList<ChatMessage>>(result);
        }

        public Task<IReadOnlyList<ChatMessage>> ListBySessionAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ChatMessage>>(_messages
                .Where(message => message.ChatSessionId == sessionId)
                .ToList());
        }

        public Task<ChatMessage?> GetByIdAsync(Guid messageId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_messages.FirstOrDefault(message => message.Id == messageId));
        }

        public Task<IReadOnlyList<Guid>> ListIdsBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Guid>>(_messages
                .Where(message => message.ChatSessionId == sessionId)
                .Select(message => message.Id)
                .ToList());
        }

        public Task<ChatMessage?> GetByIdWithSessionAsync(Guid messageId, CancellationToken cancellationToken = default)
        {
            return GetByIdAsync(messageId, cancellationToken);
        }

        public Task<IReadOnlyList<ChatMessage>> ListBySessionAndIdsAsync(
            Guid sessionId,
            IReadOnlyCollection<Guid> messageIds,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ChatMessage>>(_messages
                .Where(message => message.ChatSessionId == sessionId && messageIds.Contains(message.Id))
                .ToList());
        }

        public Task<ChatMessage?> GetLatestBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_messages.LastOrDefault(message => message.ChatSessionId == sessionId));
        }

        public Task<ChatMessage?> GetLatestBySessionAndSenderAsync(
            Guid sessionId,
            string senderType,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_messages.LastOrDefault(message =>
                message.ChatSessionId == sessionId &&
                string.Equals(message.SenderType, senderType, StringComparison.Ordinal)));
        }

        public Task<int> CountBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_messages.Count(message => message.ChatSessionId == sessionId));
        }

        public Task<Guid?> GetLatestMessageIdBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_messages.LastOrDefault(message => message.ChatSessionId == sessionId)?.Id);
        }

        public Task AddAsync(ChatMessage message, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ChatMessage message, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteAsync(ChatMessage message, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteRangeAsync(IReadOnlyCollection<ChatMessage> messages, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
