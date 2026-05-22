using DevNexus.Core.Services.Chat;
using DevNexus.Domain.Entities;
using DevNexus.Shared.Constants;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// 挂起交互恢复上下文构建器测试。
/// </summary>
public sealed class PendingInteractionResumeContextBuilderTests
{
    /// <summary>
    /// 审批恢复上下文应输出语义化授权信息，并隐藏内部 metadata 键。
    /// </summary>
    [Fact]
    public void Build_ShouldRenderApprovalResumeContext_WithoutRawMetadataKeys()
    {
        var interaction = new PendingInteraction
        {
            Status = PendingInteractionStatus.Resolved,
            Title = "等待执行审批",
            Description = "命令需要审批。",
            ResolutionData = new Dictionary<string, object>
            {
                [PendingInteractionMetadataKeys.ResolutionAction] = PendingInteractionResolutionActions.ApprovePattern,
                [PendingInteractionMetadataKeys.ApprovalScope] = CliApprovalGrantScope.Pattern.ToString(),
                ["note"] = "继续执行"
            }
        };

        var context = PendingInteractionResumeContextBuilder.Build(interaction);

        context.Should().Contain("## 用户刚刚补充的关键信息");
        context.Should().Contain("- 解决动作: approve-pattern");
        context.Should().Contain("- 审批授权范围: Pattern");
        context.Should().Contain("- note: 继续执行");
        context.Should().NotContain(PendingInteractionMetadataKeys.ResolutionAction);
        context.Should().NotContain(PendingInteractionMetadataKeys.ApprovalScope);
    }

    /// <summary>
    /// 未解决的挂起交互不应生成恢复上下文。
    /// </summary>
    [Fact]
    public void Build_ShouldReturnNull_WhenInteractionIsPending()
    {
        var context = PendingInteractionResumeContextBuilder.Build(new PendingInteraction
        {
            Status = PendingInteractionStatus.Pending,
            ResolutionData = new Dictionary<string, object>
            {
                [PendingInteractionMetadataKeys.ResolutionAction] = PendingInteractionResolutionActions.Submit
            }
        });

        context.Should().BeNull();
    }
}
