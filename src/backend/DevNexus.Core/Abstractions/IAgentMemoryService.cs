using DevNexus.Domain.Entities;
using DevNexus.Core.DTOs;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;

namespace DevNexus.Core.Abstractions;

/// <summary>
/// 智能体全局语义记忆服务 (SOP 与经验库抽取归档)
/// </summary>
public interface IAgentMemoryService
{
    /// <summary>
    /// 根据用户意图搜索最匹配的系统经验
    /// </summary>
    Task<ExperienceMatchDto?> SearchExperienceAsync(string intent, ExperienceType type, CancellationToken cancellationToken = default);

    /// <summary>
    /// 提升指定经验的效用评分与使用次数 (强化学习反馈)
    /// </summary>
    Task BoostExperienceAsync(Guid experienceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 归档保存新提取的高质量经验
    /// </summary>
    Task<ExperienceSaveResultDto> SaveExperienceAsync(SystemExperience experience, CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行后台定期修剪：效用评分衰减与低分清理
    /// </summary>
    Task PruneExperiencesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取系统经验分页列表。
    /// </summary>
    Task<PagedResult<SystemExperienceDto>> GetSystemExperiencesAsync(
        ExperienceType? type,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 切换系统经验固定状态。
    /// </summary>
    Task<bool?> TogglePinExperienceAsync(Guid experienceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新系统经验效用评分。
    /// </summary>
    Task<double?> UpdateExperienceScoreAsync(Guid experienceId, double score, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除系统经验。
    /// </summary>
    Task<bool> DeleteExperienceAsync(Guid experienceId, CancellationToken cancellationToken = default);
}
