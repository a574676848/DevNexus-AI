using DevNexus.Core.Abstractions;
using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using DevNexus.Core.DTOs;
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
                minRelevance: 0.8,
                limit: 1,
                cancellationToken: cancellationToken);

            if (!searchResult.Results.Any())
                return null;

            var bestMatch = searchResult.Results.First();
            if (bestMatch.Partitions.First().Relevance < 0.8)
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
            exp.UtilityScore = Math.Min(10.0, exp.UtilityScore + 0.1);
            exp.LastMatchedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<SystemExperience> SaveExperienceAsync(SystemExperience experience, CancellationToken cancellationToken = default)
    {
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "向 Kernel Memory 保存经验向量时失败");
        }

        return experience;
    }

    /// <inheritdoc />
    public async Task PruneExperiencesAsync(CancellationToken cancellationToken = default)
    {
        // 每天执行的衰减逻辑 (由 Hangfire Job 触发)
        var limitDate = DateTime.UtcNow.AddDays(-30); // 30天未使用的才衰减

        var staleExperiences = await _dbContext.SystemExperiences
            .Where(e => !e.IsPinned && e.LastMatchedAt < limitDate)
            .ToListAsync(cancellationToken);

        int deletedCount = 0;
        foreach (var exp in staleExperiences)
        {
            exp.UtilityScore *= 0.8; // 折扣衰减
            if (exp.UtilityScore < 0.2) // 效用过低则清理淘汰
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
