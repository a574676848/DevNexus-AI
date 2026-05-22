using DevNexus.Core.Services.Chat;
using DevNexus.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// 系统经验保存结果工厂测试。
/// </summary>
public sealed class SystemExperienceSaveResultFactoryTests
{
    /// <summary>
    /// 重复经验结果应标记为跳过。
    /// </summary>
    [Fact]
    public void Duplicate_ShouldCreateDuplicateResult()
    {
        var experience = CreateExperience();

        var result = SystemExperienceSaveResultFactory.Duplicate(experience);

        result.Experience.Should().BeSameAs(experience);
        result.IsDuplicate.Should().BeTrue();
        result.IsNew.Should().BeFalse();
        result.VectorIndexed.Should().BeFalse();
        result.Reason.Should().Be(SystemExperienceSaveReasons.DuplicateSkipped);
        result.CitationFingerprint.Should().NotBeEmpty();
        result.AttemptCitationFingerprint.Should().Be(result.CitationFingerprint);
        result.MemoryCitation.CitationFingerprint.Should().Be(result.CitationFingerprint);
        result.AttemptMemoryCitation.CitationFingerprint.Should().Be(result.AttemptCitationFingerprint);
    }

    /// <summary>
    /// 重复经验结果应同时保留已有经验和本次尝试的引用指纹。
    /// </summary>
    [Fact]
    public void Duplicate_ShouldKeepAttemptCitationFingerprint()
    {
        var existing = CreateExperience();
        var attempted = CreateExperience();

        var result = SystemExperienceSaveResultFactory.Duplicate(existing, attempted);

        result.Experience.Should().BeSameAs(existing);
        result.IsDuplicate.Should().BeTrue();
        result.CitationFingerprint.Should().NotBe(result.AttemptCitationFingerprint);
        result.CitationFingerprint.Should().Be(
            SystemExperienceMemoryCitation.FromContextTags(existing.Id, existing.ContextTags).CitationFingerprint);
        result.AttemptCitationFingerprint.Should().Be(
            SystemExperienceMemoryCitation.FromContextTags(attempted.Id, attempted.ContextTags).CitationFingerprint);
        result.MemoryCitation.ExperienceId.Should().Be(existing.Id);
        result.AttemptMemoryCitation.ExperienceId.Should().Be(attempted.Id);
        result.MemoryCitation.CitationFingerprint.Should().Be(result.CitationFingerprint);
        result.AttemptMemoryCitation.CitationFingerprint.Should().Be(result.AttemptCitationFingerprint);
    }

    /// <summary>
    /// 新增且完成索引结果应标记索引成功。
    /// </summary>
    [Fact]
    public void CreatedAndIndexed_ShouldCreateIndexedResult()
    {
        var experience = CreateExperience();

        var result = SystemExperienceSaveResultFactory.CreatedAndIndexed(experience);

        result.Experience.Should().BeSameAs(experience);
        result.IsNew.Should().BeTrue();
        result.IsDuplicate.Should().BeFalse();
        result.VectorIndexed.Should().BeTrue();
        result.Reason.Should().Be(SystemExperienceSaveReasons.CreatedAndIndexed);
        result.CitationFingerprint.Should().NotBeEmpty();
        result.AttemptCitationFingerprint.Should().Be(result.CitationFingerprint);
        result.MemoryCitation.CitationFingerprint.Should().Be(result.CitationFingerprint);
        result.AttemptMemoryCitation.CitationFingerprint.Should().Be(result.AttemptCitationFingerprint);
    }

    /// <summary>
    /// 新增但索引失败结果不应标记为重复或索引成功。
    /// </summary>
    [Fact]
    public void CreatedButIndexFailed_ShouldCreateIndexFailedResult()
    {
        var experience = CreateExperience();

        var result = SystemExperienceSaveResultFactory.CreatedButIndexFailed(experience);

        result.Experience.Should().BeSameAs(experience);
        result.IsNew.Should().BeTrue();
        result.IsDuplicate.Should().BeFalse();
        result.VectorIndexed.Should().BeFalse();
        result.Reason.Should().Be(SystemExperienceSaveReasons.CreatedButIndexFailed);
        result.CitationFingerprint.Should().NotBeEmpty();
        result.AttemptCitationFingerprint.Should().Be(result.CitationFingerprint);
        result.MemoryCitation.CitationFingerprint.Should().Be(result.CitationFingerprint);
        result.AttemptMemoryCitation.CitationFingerprint.Should().Be(result.AttemptCitationFingerprint);
    }

    private static SystemExperience CreateExperience()
    {
        return new SystemExperience
        {
            Id = Guid.NewGuid(),
            ContextTags = string.Join(
                ",",
                ExperienceDistillationOutputProtocol.ContextTag,
                ExperienceDistillationOutputProtocol.ValueSignalTagPrefix + "流程",
                ExperienceDistillationOutputProtocol.SourceSessionTagPrefix + Guid.NewGuid().ToString("D"))
        };
    }
}
