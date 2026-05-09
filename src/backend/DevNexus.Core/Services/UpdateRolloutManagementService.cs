using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Shared.DTOs;
using Microsoft.Extensions.Logging;

namespace DevNexus.Core.Services;

/// <summary>
/// 投放中心管理服务实现。
/// </summary>
public class UpdateRolloutManagementService : IUpdateRolloutManagementService
{
    private readonly IUpdateRolloutRepository _rolloutRepository;
    private readonly IUpdateReleaseRepository _releaseRepository;
    private readonly ILogger<UpdateRolloutManagementService> _logger;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public UpdateRolloutManagementService(
        IUpdateRolloutRepository rolloutRepository,
        IUpdateReleaseRepository releaseRepository,
        ILogger<UpdateRolloutManagementService> logger)
    {
        _rolloutRepository = rolloutRepository;
        _releaseRepository = releaseRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RolloutDto>> GetRolloutsAsync(CancellationToken cancellationToken = default)
    {
        var rollouts = await _rolloutRepository.GetAllAsync(cancellationToken);
        return rollouts.Select(MapRollout).ToList();
    }

    /// <inheritdoc />
    public async Task<RolloutDto> SaveRolloutAsync(SaveRolloutRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRolloutRequest(request);

        var release = await _releaseRepository.GetByIdAsync(request.ReleaseId, cancellationToken)
            ?? throw new InvalidOperationException($"目标发布版本 {request.ReleaseId} 不存在");

        var rolloutId = request.RolloutId ?? Guid.Empty;
        var rollout = rolloutId == Guid.Empty
            ? new UpdateRollout { Id = Guid.Empty }
            : await _rolloutRepository.GetByIdAsync(rolloutId, cancellationToken) ?? new UpdateRollout { Id = rolloutId };

        rollout.ReleaseId = request.ReleaseId;
        rollout.Platform = NormalizeOrDefault(request.Platform, "desktop");
        rollout.Architecture = NormalizeOrDefault(request.Architecture, "any");
        rollout.Channel = NormalizeOrDefault(request.Channel, release.Channel);
        rollout.MinimumSupportedVersion = string.IsNullOrWhiteSpace(request.MinimumSupportedVersion)
            ? release.Version
            : request.MinimumSupportedVersion.Trim();
        rollout.ForceUpdate = request.ForceUpdate;
        rollout.RolloutPercent = Math.Clamp(request.RolloutPercent, 0, 100);
        rollout.AudienceRule = string.IsNullOrWhiteSpace(request.AudienceRule) ? "all" : request.AudienceRule.Trim();
        rollout.StartsAt = request.StartsAt == default ? DateTime.UtcNow : request.StartsAt;
        rollout.EndsAt = request.EndsAt;
        rollout.Priority = request.Priority;
        rollout.Enabled = request.Enabled;
        rollout.KillSwitchEnabled = request.KillSwitchEnabled;

        rollout = await _rolloutRepository.SaveAsync(rollout, cancellationToken);
        var saved = await _rolloutRepository.GetByIdAsync(rollout.Id, cancellationToken)
            ?? throw new InvalidOperationException("保存后的投放规则不存在");

        _logger.LogInformation(
            "[UpdateRolloutManagementService] 已保存投放规则 | RolloutId={RolloutId} ReleaseId={ReleaseId} Platform={Platform}",
            saved.Id,
            saved.ReleaseId,
            saved.Platform);

        return MapRollout(saved);
    }

    /// <inheritdoc />
    public async Task<RolloutDto> PauseAsync(Guid rolloutId, CancellationToken cancellationToken = default)
    {
        var rollout = await _rolloutRepository.GetByIdAsync(rolloutId, cancellationToken)
            ?? throw new InvalidOperationException($"投放规则 {rolloutId} 不存在");

        rollout.Enabled = false;
        rollout = await _rolloutRepository.SaveAsync(rollout, cancellationToken);
        return MapRollout(rollout);
    }

    /// <inheritdoc />
    public async Task<RolloutDto> ResumeAsync(Guid rolloutId, CancellationToken cancellationToken = default)
    {
        var rollout = await _rolloutRepository.GetByIdAsync(rolloutId, cancellationToken)
            ?? throw new InvalidOperationException($"投放规则 {rolloutId} 不存在");

        rollout.Enabled = true;
        rollout.KillSwitchEnabled = false;
        rollout = await _rolloutRepository.SaveAsync(rollout, cancellationToken);
        return MapRollout(rollout);
    }

    /// <inheritdoc />
    public async Task<RolloutDto> RollbackAsync(Guid rolloutId, CancellationToken cancellationToken = default)
    {
        var rollout = await _rolloutRepository.GetByIdAsync(rolloutId, cancellationToken)
            ?? throw new InvalidOperationException($"投放规则 {rolloutId} 不存在");

        if (rollout.Release == null)
        {
            throw new InvalidOperationException("回滚失败，当前投放规则未关联发布版本");
        }

        var previousRelease = await _releaseRepository.GetPreviousPublishedReleaseAsync(
            rollout.Release.Channel,
            rollout.Release.Id,
            cancellationToken);

        if (previousRelease == null)
        {
            throw new InvalidOperationException("未找到可回滚的上一个已发布版本");
        }

        // 中文注释：先停掉当前投放，再克隆一条更高优先级的新规则指向上一个稳定版本。
        rollout.Enabled = false;
        rollout.KillSwitchEnabled = true;
        await _rolloutRepository.SaveAsync(rollout, cancellationToken);

        var rollbackRollout = new UpdateRollout
        {
            ReleaseId = previousRelease.Id,
            Platform = rollout.Platform,
            Architecture = rollout.Architecture,
            Channel = rollout.Channel,
            MinimumSupportedVersion = rollout.MinimumSupportedVersion,
            ForceUpdate = rollout.ForceUpdate,
            RolloutPercent = 100,
            AudienceRule = rollout.AudienceRule,
            StartsAt = DateTime.UtcNow,
            EndsAt = null,
            Priority = rollout.Priority + 1,
            Enabled = true,
            KillSwitchEnabled = false
        };

        var saved = await _rolloutRepository.SaveAsync(rollbackRollout, cancellationToken);
        saved = await _rolloutRepository.GetByIdAsync(saved.Id, cancellationToken)
            ?? throw new InvalidOperationException("回滚后的投放规则不存在");

        return MapRollout(saved);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid rolloutId, CancellationToken cancellationToken = default)
    {
        var rollout = await _rolloutRepository.GetByIdAsync(rolloutId, cancellationToken)
            ?? throw new InvalidOperationException($"投放规则 {rolloutId} 不存在");

        await _rolloutRepository.DeleteAsync(rollout, cancellationToken);

        _logger.LogInformation(
            "[UpdateRolloutManagementService] 已删除投放规则 | RolloutId={RolloutId} ReleaseId={ReleaseId}",
            rollout.Id,
            rollout.ReleaseId);
    }

    private static void ValidateRolloutRequest(SaveRolloutRequest request)
    {
        if (request.ReleaseId == Guid.Empty)
        {
            throw new InvalidOperationException("ReleaseId 不能为空");
        }

        if (string.IsNullOrWhiteSpace(request.Platform))
        {
            throw new InvalidOperationException("Platform 不能为空");
        }

        if (request.EndsAt.HasValue && request.EndsAt.Value <= request.StartsAt)
        {
            throw new InvalidOperationException("EndsAt 必须晚于 StartsAt");
        }
    }

    private static RolloutDto MapRollout(UpdateRollout rollout)
    {
        return new RolloutDto
        {
            RolloutId = rollout.Id,
            ReleaseId = rollout.ReleaseId,
            ReleaseVersion = rollout.Release?.Version ?? string.Empty,
            ReleaseTitle = rollout.Release?.Title ?? string.Empty,
            Platform = rollout.Platform,
            Architecture = rollout.Architecture,
            Channel = rollout.Channel,
            MinimumSupportedVersion = rollout.MinimumSupportedVersion,
            ForceUpdate = rollout.ForceUpdate,
            RolloutPercent = rollout.RolloutPercent,
            AudienceRule = rollout.AudienceRule,
            StartsAt = rollout.StartsAt,
            EndsAt = rollout.EndsAt,
            Priority = rollout.Priority,
            Enabled = rollout.Enabled,
            KillSwitchEnabled = rollout.KillSwitchEnabled,
            CreatedAt = rollout.CreatedAt,
            UpdatedAt = rollout.UpdatedAt
        };
    }

    private static string NormalizeOrDefault(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToLowerInvariant();
    }
}
