using DevNexus.Core.Models.Evaluation;
using DevNexus.Core.Services.Chat;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// 工具执行记录归一化器测试。
/// </summary>
public sealed class ToolExecutionRecordNormalizerTests
{
    /// <summary>
    /// 长错误信息摘要应保留尾部错误线索。
    /// </summary>
    [Fact]
    public void Normalize_ShouldCompressLongErrorSummaryWithTail()
    {
        var records = new[]
        {
            new ToolExecutionRecord
            {
                ToolName = "HostService.ExecuteCommand",
                Success = false,
                ErrorMessage = "HEAD-" + new string('x', 500) + "-TAIL"
            }
        };

        var normalized = ToolExecutionRecordNormalizer.Normalize(records);

        normalized[0].ErrorSummary.Should().Contain("Total output chars:");
        normalized[0].ErrorSummary.Should().Contain("HEAD-");
        normalized[0].ErrorSummary.Should().Contain("-TAIL");
        normalized[0].ErrorSummary!.Length.Should().BeLessThanOrEqualTo(200);
    }
}
