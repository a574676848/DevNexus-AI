using DevNexus.Core.Services.Chat;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// 聊天历史回放文本清洗器测试。
/// </summary>
public sealed class ChatHistoryReplayTextSanitizerTests
{
    /// <summary>
    /// ANSI 控制序列不应进入模型历史。
    /// </summary>
    [Fact]
    public void Clean_ShouldRemoveAnsiSequences()
    {
        var cleaned = ChatHistoryReplayTextSanitizer.Clean("\u001b[31m失败\u001b[0m");

        cleaned.Should().Be("失败");
    }

    /// <summary>
    /// 非文本控制字符不应进入模型历史，但换行和制表符应保留。
    /// </summary>
    [Fact]
    public void Clean_ShouldRemoveUnsafeControlCharacters()
    {
        var cleaned = ChatHistoryReplayTextSanitizer.Clean("第一行\u0000\n\t第二行\u0007");

        cleaned.Should().Be("第一行\n\t第二行");
    }
}
