using DevNexus.Client.Shared.Components.Chat;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Client.Chat;

public sealed class MessageContentParserTests
{
    [Fact]
    public void ParseContent_ShouldReadStructuredTextAndThinkingFields()
    {
        var message = new ChatMessageDto
        {
            SenderType = ChatConstants.RoleAssistant,
            Content = "旧内容",
            TextContent = "最终答案",
            ThinkingContent = "步骤一\n步骤二"
        };

        var parsed = MessageContentParser.ParseContent(message);

        parsed.DisplayedContent.Should().Be("最终答案");
        parsed.Thoughts.Should().Equal("步骤一", "步骤二");
    }

    [Fact]
    public void ParseContent_ShouldPreferStructuredFieldsOverContent()
    {
        var message = new ChatMessageDto
        {
            SenderType = ChatConstants.RoleAssistant,
            Content = "旧正文",
            TextContent = "新正文",
            ThinkingContent = "新思考"
        };

        var parsed = MessageContentParser.ParseContent(message);

        parsed.DisplayedContent.Should().Be("新正文");
        parsed.Thoughts.Should().ContainSingle().Which.Should().Be("新思考");
    }
}
