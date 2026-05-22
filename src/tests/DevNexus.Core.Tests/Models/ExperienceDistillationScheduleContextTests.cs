using DevNexus.Domain.Models;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Models;

/// <summary>
/// 经验提纯调度上下文测试。
/// </summary>
public sealed class ExperienceDistillationScheduleContextTests
{
    /// <summary>
    /// 创建调度上下文时应裁剪空白，避免后台任务日志字段漂移。
    /// </summary>
    [Fact]
    public void Create_ShouldNormalizeValues()
    {
        var context = ExperienceDistillationScheduleContext.Create(
            " summary-compression-resolved ",
            " summary-compression ",
            " fingerprint ");

        context.CandidateReason.Should().Be("summary-compression-resolved");
        context.ContextPressureReason.Should().Be("summary-compression");
        context.ContextCompressionSummaryFingerprint.Should().Be("fingerprint");
        context.HasFacts.Should().BeTrue();
    }

    /// <summary>
    /// 空输入应生成空上下文，便于兼容历史调度入口。
    /// </summary>
    [Fact]
    public void Create_ShouldUseEmptyValues_WhenInputIsNull()
    {
        var context = ExperienceDistillationScheduleContext.Create(null, null, null);

        context.CandidateReason.Should().BeEmpty();
        context.ContextPressureReason.Should().BeEmpty();
        context.ContextCompressionSummaryFingerprint.Should().BeEmpty();
        context.HasFacts.Should().BeFalse();
    }
}
