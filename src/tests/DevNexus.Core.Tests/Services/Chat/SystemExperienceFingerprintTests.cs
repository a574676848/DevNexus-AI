using DevNexus.Core.Services.Chat;
using DevNexus.Domain.Entities;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// 系统经验指纹测试。
/// </summary>
public sealed class SystemExperienceFingerprintTests
{
    /// <summary>
    /// 指纹应忽略空白和大小写差异。
    /// </summary>
    [Fact]
    public void Compute_ShouldNormalizeWhitespaceAndCase()
    {
        var first = CreateExperience("Fix Build", "Run dotnet build");
        var second = CreateExperience(" fix   build ", " run DOTNET   build ");

        SystemExperienceFingerprint.Compute(second)
            .Should()
            .Be(SystemExperienceFingerprint.Compute(first));
    }

    /// <summary>
    /// 指纹应写入上下文标签并替换旧指纹。
    /// </summary>
    [Fact]
    public void MergeIntoContextTags_ShouldReplaceExistingFingerprint()
    {
        var tags = SystemExperienceFingerprint.MergeIntoContextTags(
            "dotnet,fingerprint:old",
            "new");

        tags.Should().Be("dotnet,fingerprint:new");
    }

    /// <summary>
    /// 可从上下文标签中识别指纹。
    /// </summary>
    [Fact]
    public void HasFingerprint_ShouldReadFingerprintFromContextTags()
    {
        var experience = CreateExperience("修复构建", "运行构建");
        experience.ContextTags = "dotnet,fingerprint:abc";

        SystemExperienceFingerprint.HasFingerprint(experience, "abc").Should().BeTrue();
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
