using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// 普通聊天流完成顺序守卫。
/// </summary>
public sealed class ChatStreamingCompletionSequenceTests
{
    [Fact]
    public void StreamAiResponseAsync_ShouldSendTerminalBlockAfterPersistenceAndCompletion()
    {
        var source = ReadChatStreamingSource();

        var retryIndex = source.IndexOf(
            "if (agentLoopDecision.Action == AgentLoopAction.Retry",
            StringComparison.Ordinal);
        var finalizerIndex = source.IndexOf(
            "await _chatStreamingFinalizer.FinalizeCompletedAsync",
            StringComparison.Ordinal);
        var completionIndex = source.IndexOf(
            "await _chatMessageCompletionCoordinator.HandleCompletedAsync",
            StringComparison.Ordinal);
        var memoryIndex = source.IndexOf(
            "var memoryDecision = await TriggerMemoryConsolidationCheckAsync",
            StringComparison.Ordinal);
        var terminalIndex = source.IndexOf(
            "await WriteTerminalBlockAsync(aiMessage.Id, chatSession.Id, blockWriter, cancellationToken);",
            StringComparison.Ordinal);

        retryIndex.Should().BeGreaterThan(0);
        finalizerIndex.Should().BeGreaterThan(retryIndex);
        completionIndex.Should().BeGreaterThan(finalizerIndex);
        memoryIndex.Should().BeGreaterThan(completionIndex);
        terminalIndex.Should().BeGreaterThan(memoryIndex);
    }

    [Fact]
    public void StreamAiResponseAsync_ShouldUseSafeWriterForErrorTerminalBlock()
    {
        var source = ReadChatStreamingSource();

        var errorFinalizerIndex = source.IndexOf(
            "var renderedError = await _chatStreamingFinalizer.FinalizeErroredAsync",
            StringComparison.Ordinal);
        var catchEndIndex = source.IndexOf(
            "// 移除 throw; 使得外部当做普通消息完成",
            StringComparison.Ordinal);

        errorFinalizerIndex.Should().BeGreaterThan(0);
        catchEndIndex.Should().BeGreaterThan(errorFinalizerIndex);

        var errorCatchBlock = source[errorFinalizerIndex..catchEndIndex];
        errorCatchBlock.Should().Contain("await TryWriteErrorTerminalBlockAsync");
        errorCatchBlock.Should().NotContain("await blockWriter.WriteAsync(new BlockDto");
        source.Should().Contain("blockWriter.TryWrite(block)");
        source.Should().Contain("catch (ChannelClosedException ex)");
    }

    private static string ReadChatStreamingSource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "backend",
                "DevNexus.Core",
                "Services",
                "ChatService.Streaming.cs");

            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("未找到 ChatService.Streaming.cs 源文件。");
    }
}
