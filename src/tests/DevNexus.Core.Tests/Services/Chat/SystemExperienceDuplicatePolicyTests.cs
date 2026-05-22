using DevNexus.Core.Services.Chat;
using DevNexus.Domain.Entities;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// 系统经验重复写入策略测试。
/// </summary>
public sealed class SystemExperienceDuplicatePolicyTests
{
    /// <summary>
    /// 同类型经验应进入重复候选判定。
    /// </summary>
    [Fact]
    public void IsCandidate_ShouldReturnTrue_WhenTypeMatches()
    {
        var candidate = CreateExperience("修复构建失败", "运行 dotnet build。");
        var existing = CreateExperience("其他意图", "其他 SOP");

        SystemExperienceDuplicatePolicy.IsCandidate(candidate, existing).Should().BeTrue();
    }

    /// <summary>
    /// 不同类型经验不应进入重复候选判定。
    /// </summary>
    [Fact]
    public void IsCandidate_ShouldReturnFalse_WhenTypeDiffers()
    {
        var candidate = CreateExperience("修复构建失败", "运行 dotnet build。");
        var existing = CreateExperience("修复构建失败", "运行 dotnet build。");
        existing.Type = ExperienceType.CodeFix;

        SystemExperienceDuplicatePolicy.IsCandidate(candidate, existing).Should().BeFalse();
    }

    /// <summary>
    /// 相同语义指纹应判定为重复。
    /// </summary>
    [Fact]
    public void IsDuplicate_ShouldReturnTrue_WhenSemanticFingerprintMatches()
    {
        var candidate = CreateExperience("修复构建失败", "运行 dotnet build 并定位错误。");
        var existing = CreateExperience(" 修复构建失败 ", "运行   DOTNET build 并定位错误。");

        SystemExperienceDuplicatePolicy.IsDuplicate(candidate, [existing]).Should().BeTrue();
    }

    /// <summary>
    /// 已携带相同指纹标签时应判定为重复。
    /// </summary>
    [Fact]
    public void IsDuplicate_ShouldReturnTrue_WhenExistingHasFingerprintTag()
    {
        var candidate = CreateExperience("修复构建失败", "运行 dotnet build。");
        var existing = CreateExperience("其他意图", "其他 SOP");
        existing.ContextTags = SystemExperienceFingerprint.MergeIntoContextTags(
            null,
            SystemExperienceFingerprint.Compute(candidate));

        SystemExperienceDuplicatePolicy.IsDuplicate(candidate, [existing]).Should().BeTrue();
    }

    /// <summary>
    /// 指纹不同时不应判定为重复。
    /// </summary>
    [Fact]
    public void IsDuplicate_ShouldReturnFalse_WhenFingerprintDiffers()
    {
        var candidate = CreateExperience("修复构建失败", "运行 dotnet build。");
        var existing = CreateExperience("配置数据库连接", "检查连接字符串。");

        SystemExperienceDuplicatePolicy.IsDuplicate(candidate, [existing]).Should().BeFalse();
    }

    private static SystemExperience CreateExperience(string intent, string sop)
    {
        return new SystemExperience
        {
            Type = ExperienceType.QA,
            Intent = intent,
            SolutionSop = sop
        };
    }
}
