using DevNexus.Core.Abstractions;
using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using DevNexus.Core.DTOs;
using DevNexus.Core.Services.Chat;
using DevNexus.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;

namespace DevNexus.Infrastructure.Services.Memory;

/// <summary>
/// 智能体系统经验记忆服务实现 (System 1 快思考缓存引擎)
/// 结合了 Entity Framework 和 Kernel Memory 向量检索引擎
/// </summary>
public class AgentMemoryService : IAgentMemoryService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IKernelMemory _kernelMemory;
    private readonly ILogger<AgentMemoryService> _logger;

    private const string ExperienceIndex = DevNexus.Shared.Constants.MemoryConstants.ExperienceIndex;

    public AgentMemoryService(
        ApplicationDbContext dbContext,
        IKernelMemory kernelMemory,
        ILogger<AgentMemoryService> logger)
    {
        _dbContext = dbContext;
        _kernelMemory = kernelMemory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ExperienceMatchDto?> SearchExperienceAsync(string intent, ExperienceType type, CancellationToken cancellationToken = default)
    {
        try
        {
            // 依赖于 Kernel Memory 进行向量检索
            var searchResult = await _kernelMemory.SearchAsync(
                intent,
                index: ExperienceIndex,
                minRelevance: SystemExperienceLifecyclePolicy.MinimumSearchRelevance,
                limit: 1,
                cancellationToken: cancellationToken);

            if (!searchResult.Results.Any())
                return null;

            var bestMatch = searchResult.Results.First();
            if (!SystemExperienceLifecyclePolicy.IsSearchMatch(bestMatch.Partitions.First().Relevance))
                return null;

            // Retrieve ID from tags
            if (!bestMatch.Partitions.First().Tags.TryGetValue("ExperienceId", out var idList) || !idList.Any())
                return null;

            if (!Guid.TryParse(idList.First(), out var experienceId))
                return null;

            // Find in DB
            var experience = await _dbContext.SystemExperiences
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == experienceId && e.Type == type, cancellationToken);

            if (experience == null) return null;

            return new ExperienceMatchDto
            {
                Experience = experience,
                Similarity = bestMatch.Partitions.First().Relevance
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检索系统经验失败, 意图: {Intent}", intent);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task BoostExperienceAsync(Guid experienceId, CancellationToken cancellationToken = default)
    {
        var exp = await _dbContext.SystemExperiences.FindAsync(new object[] { experienceId }, cancellationToken);
        if (exp != null)
        {
            exp.UsageCount += 1;
            // 每次成功使用，评分微调
            exp.UtilityScore = SystemExperienceLifecyclePolicy.BoostUtilityScore(exp.UtilityScore);
            exp.LastMatchedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<ExperienceSaveResultDto> SaveExperienceAsync(SystemExperience experience, CancellationToken cancellationToken = default)
    {
        var fingerprint = SystemExperienceFingerprint.Compute(experience);
        var existingExperiences = await _dbContext.SystemExperiences
            .Where(item => item.Type == experience.Type)
            .ToListAsync(cancellationToken);
        var duplicate = existingExperiences.FirstOrDefault(existing =>
            SystemExperienceDuplicatePolicy.IsCandidate(experience, existing)
            && SystemExperienceDuplicatePolicy.IsDuplicate(experience, [existing]));
        if (duplicate != null)
        {
            SystemExperienceLifecyclePolicy.ApplyDuplicateRediscovery(duplicate, DateTime.UtcNow);
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "系统经验重复，已强化已有经验: ExistingId={ExistingId} Intent={Intent}",
                duplicate.Id,
                experience.Intent);
            return SystemExperienceSaveResultFactory.Duplicate(duplicate, experience);
        }

        experience.ContextTags = SystemExperienceFingerprint.MergeIntoContextTags(
            experience.ContextTags,
            fingerprint);
        _dbContext.SystemExperiences.Add(experience);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // 保存到向量数据库建立语义索引
        try
        {
            var documentId = experience.Id.ToString();
            await _kernelMemory.ImportTextAsync(
                experience.Intent,      // 文本内容是意图
                documentId: documentId,
                tags: new TagCollection
                {
                    { "ExperienceId", documentId },
                    { "Type", experience.Type.ToString() }
                },
                index: ExperienceIndex,
                cancellationToken: cancellationToken);

            _logger.LogInformation("成功归档新经验并建立向量索引: {Intent}", experience.Intent);
            return SystemExperienceSaveResultFactory.CreatedAndIndexed(experience);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "向 Kernel Memory 保存经验向量时失败");
            return SystemExperienceSaveResultFactory.CreatedButIndexFailed(experience);
        }
    }

    /// <inheritdoc />
    public async Task PruneExperiencesAsync(CancellationToken cancellationToken = default)
    {
        // 每天执行的衰减逻辑 (由 Hangfire Job 触发)
        var limitDate = SystemExperienceLifecyclePolicy.GetStaleBoundary(DateTime.UtcNow);

        var staleExperiences = await _dbContext.SystemExperiences
            .Where(e => !e.IsPinned && e.LastMatchedAt < limitDate)
            .ToListAsync(cancellationToken);

        int deletedCount = 0;
        foreach (var exp in staleExperiences)
        {
            if (SystemExperienceLifecyclePolicy.ApplyDecay(exp))
            {
                try
                {
                    await _kernelMemory.DeleteDocumentAsync(exp.Id.ToString(), index: ExperienceIndex, cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "清理 Kernel Memory 无效向量文档失败: {Id}", exp.Id);
                }

                _dbContext.SystemExperiences.Remove(exp);
                deletedCount++;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("经验库修剪完成：衰减 {Count} 条，彻底淘汰 {DelCount} 条", staleExperiences.Count, deletedCount);
    }

    /// <inheritdoc />
    public async Task<PagedResult<SystemExperienceDto>> GetSystemExperiencesAsync(
        ExperienceType? type,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.SystemExperiences.AsNoTracking().AsQueryable();

        if (type.HasValue)
        {
            query = query.Where(e => e.Type == type.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(e => e.Intent.Contains(search) || e.SolutionSop.Contains(search));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(e => e.IsPinned)
            .ThenByDescending(e => e.UtilityScore)
            .ThenByDescending(e => e.LastMatchedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new SystemExperienceDto
            {
                Id = e.Id,
                Type = e.Type,
                Intent = e.Intent,
                SolutionSop = e.SolutionSop,
                UtilityScore = e.UtilityScore,
                UsageCount = e.UsageCount,
                IsPinned = e.IsPinned,
                LastMatchedAt = e.LastMatchedAt,
                CreatedAt = e.CreatedAt,
                UpdatedAt = e.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<SystemExperienceDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = page,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<bool?> TogglePinExperienceAsync(Guid experienceId, CancellationToken cancellationToken = default)
    {
        var experience = await _dbContext.SystemExperiences.FindAsync(new object[] { experienceId }, cancellationToken);
        if (experience == null)
        {
            return null;
        }

        experience.IsPinned = !experience.IsPinned;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return experience.IsPinned;
    }

    /// <inheritdoc />
    public async Task<double?> UpdateExperienceScoreAsync(Guid experienceId, double score, CancellationToken cancellationToken = default)
    {
        var experience = await _dbContext.SystemExperiences.FindAsync(new object[] { experienceId }, cancellationToken);
        if (experience == null)
        {
            return null;
        }

        experience.UtilityScore = score;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return experience.UtilityScore;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteExperienceAsync(Guid experienceId, CancellationToken cancellationToken = default)
    {
        var experience = await _dbContext.SystemExperiences.FindAsync(new object[] { experienceId }, cancellationToken);
        if (experience == null)
        {
            return false;
        }

        _dbContext.SystemExperiences.Remove(experience);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
