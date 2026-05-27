using DevNexus.Shared.Constants;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// 聊天消息 Content 字典键协议守卫。
/// </summary>
public sealed class ChatMessageContentKeysUsageTests
{
    [Fact]
    public void ChatMessageContentKeys_ShouldExposeStablePersistenceKeys()
    {
        ChatMessageContentKeys.Text.Should().Be("text");
        ChatMessageContentKeys.Thinking.Should().Be("thinking");
        ChatMessageContentKeys.TextPartial.Should().Be("text_partial");
        ChatMessageContentKeys.ThinkingPartial.Should().Be("thinking_partial");
        ChatMessageContentKeys.ThinkingExternalPartial.Should().Be("thinking_external_partial");
    }

    [Fact]
    public void CoreChatPersistencePaths_ShouldUseChatMessageContentKeys()
    {
        foreach (var path in GetCoreChatPersistencePaths())
        {
            var source = File.ReadAllText(path);

            source.Should().NotContain("[\"text\"]", path);
            source.Should().NotContain("ContainsKey(\"text\")", path);
            source.Should().NotContain("{ \"text\",", path);
            source.Should().NotContain(", \"text\")", path);

            source.Should().NotContain("[\"thinking\"]", path);
            source.Should().NotContain("ContainsKey(\"thinking\")", path);
            source.Should().NotContain("{ \"thinking\",", path);
            source.Should().NotContain(", \"thinking\")", path);

            source.Should().NotContain("[\"text_partial\"]", path);
            source.Should().NotContain("ContainsKey(\"text_partial\")", path);
            source.Should().NotContain("\"thinking_partial\"", path);
            source.Should().NotContain("\"thinking_external_partial\"", path);
        }
    }

    private static IReadOnlyList<string> GetCoreChatPersistencePaths()
    {
        var root = FindRepositoryRoot();
        var relativePaths = new[]
        {
            "src/backend/DevNexus.Core/Services/ChatService.cs",
            "src/backend/DevNexus.Core/Services/ChatService.Message.cs",
            "src/backend/DevNexus.Core/Services/ChatService.Message.Query.cs",
            "src/backend/DevNexus.Core/Services/ChatService.Session.Dto.cs",
            "src/backend/DevNexus.Core/Services/ChatService.Streaming.cs",
            "src/backend/DevNexus.Core/Services/ChatService.TaskOrchestration.cs",
            "src/backend/DevNexus.Core/Services/Chat/ChatAgentLoopCoordinator.cs",
            "src/backend/DevNexus.Core/Services/Chat/ChatHistoryMessageBuilder.cs",
            "src/backend/DevNexus.Core/Services/Chat/ChatSearchService.cs",
            "src/backend/DevNexus.Core/Services/Chat/ChatStreamingFinalizer.cs",
            "src/backend/DevNexus.Core/Services/Chat/ChatStreamingPreparationService.cs",
            "src/backend/DevNexus.Core/Services/Chat/ChatSwarmFinalizer.cs",
            "src/backend/DevNexus.Core/Services/Chat/ChatThinkingPersistenceCoordinator.cs"
        };

        return relativePaths
            .Select(path => Path.Combine(root.FullName, path.Replace('/', Path.DirectorySeparatorChar)))
            .ToList();
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "src", "DevNexus.sln")))
            {
                return directory;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("未找到 DevNexus 仓库根目录。");
    }
}
