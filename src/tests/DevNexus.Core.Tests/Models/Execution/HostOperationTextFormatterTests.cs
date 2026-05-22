using DevNexus.Core.Models.Execution;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Models.Execution;

/// <summary>
/// 宿主操作文本格式化测试。
/// </summary>
public sealed class HostOperationTextFormatterTests
{
    /// <summary>
    /// 仍在运行的命令以信息态返回模型，不应污染失败恢复链路。
    /// </summary>
    [Fact]
    public void FormatCommand_ShouldUseInfoTag_WhenCommandStillRunning()
    {
        var result = new HostCommandExecutionResult
        {
            Status = HostOperationStatus.Info,
            Message = "命令仍在运行，已保留终端会话用于继续查看输出。",
            Output = "[StillRunning] 本次等待预算已耗尽。",
            ExitCode = 0,
            SuggestedAction = ToolSuggestedAction.WaitForCompletion
        };

        var text = HostOperationTextFormatter.FormatCommand(result);

        result.SuggestedAction.Should().Be(ToolSuggestedAction.WaitForCompletion);
        text.Should().StartWith("[INFO]");
        text.Should().Contain("命令仍在运行");
        text.Should().Contain("[StillRunning]");
        text.Should().NotStartWith("[FAILURE]");
    }
}
