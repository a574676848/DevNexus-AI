using DevNexus.Domain.Entities;
using DevNexus.Core.Services.Chat;

namespace DevNexus.Core.DTOs;

/// <summary>
/// 系统经验保存结果。
/// </summary>
public sealed class ExperienceSaveResultDto
{
    /// <summary>
    /// 保存后的系统经验。
    /// </summary>
    public required SystemExperience Experience { get; init; }

    /// <summary>
    /// 是否新增了系统经验。
    /// </summary>
    public bool IsNew { get; init; }

    /// <summary>
    /// 是否跳过了重复经验。
    /// </summary>
    public bool IsDuplicate { get; init; }

    /// <summary>
    /// 向量索引是否写入成功。
    /// </summary>
    public bool VectorIndexed { get; init; }

    /// <summary>
    /// 保存结果原因。
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// 系统经验引用指纹。
    /// </summary>
    public string CitationFingerprint { get; init; } = string.Empty;

    /// <summary>
    /// 系统经验引用事实。
    /// </summary>
    public SystemExperienceMemoryCitation MemoryCitation { get; init; } = SystemExperienceMemoryCitation.Empty;

    /// <summary>
    /// 本次保存尝试的引用指纹。
    /// </summary>
    public string AttemptCitationFingerprint { get; init; } = string.Empty;

    /// <summary>
    /// 本次保存尝试的引用事实。
    /// </summary>
    public SystemExperienceMemoryCitation AttemptMemoryCitation { get; init; } = SystemExperienceMemoryCitation.Empty;
}
