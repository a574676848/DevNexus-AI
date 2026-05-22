using DevNexus.Core.Services.Terminal;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Terminal;

/// <summary>
/// 终端输出文本清洗器测试。
/// </summary>
public sealed class TerminalOutputTextSanitizerTests
{
    /// <summary>
    /// ANSI 控制序列不应进入会话历史。
    /// </summary>
    [Fact]
    public void Clean_ShouldRemoveAnsiSequences()
    {
        var output = "\u001b[31mfailed\u001b[0m";

        var cleaned = TerminalOutputTextSanitizer.Clean(output);

        cleaned.Should().Be("failed");
    }

    /// <summary>
    /// 非文本控制字符不应进入 JSON 载荷或 UI 渲染链路。
    /// </summary>
    [Fact]
    public void Clean_ShouldRemoveUnsafeControlCharacters()
    {
        var output = "start\u0000middle\u0007end\r\nnext\tline";

        var cleaned = TerminalOutputTextSanitizer.Clean(output);

        cleaned.Should().Be("startmiddleend\r\nnext\tline");
    }

    /// <summary>
    /// 等待输入识别应在清洗后继续生效。
    /// </summary>
    [Fact]
    public void IsWaitingForInput_ShouldDetectPromptAfterSanitizing()
    {
        var output = "\u001b[33mPassword:\u001b[0m";

        TerminalOutputTextSanitizer.IsWaitingForInput(output).Should().BeTrue();
    }
}
