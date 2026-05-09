using DevNexus.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using AuditEntity = DevNexus.Domain.Entities.ModelInvocationAudit;

namespace DevNexus.Infrastructure.Services.Analytics;

/// <summary>
/// 审计分析服务筛选与字典辅助能力。
/// </summary>
public partial class AuditAnalyticsService
{
    private static DateTime? EnsureUtc(DateTime? dateTime)
    {
        if (!dateTime.HasValue)
        {
            return null;
        }

        return dateTime.Value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(dateTime.Value, DateTimeKind.Utc)
            : dateTime.Value.ToUniversalTime();
    }

    private static AuditDictionaryItemDto CreateDictionaryItem(string code, string displayName)
    {
        return new AuditDictionaryItemDto
        {
            Code = code,
            DisplayName = displayName
        };
    }

    private IQueryable<AuditEntity> ApplyAuditFilters(
        IQueryable<AuditEntity> query,
        Guid? userId,
        DateTime? startDate,
        DateTime? endDate,
        string? ownerType,
        string? sceneCode,
        string? invocationKind,
        string? status)
    {
        if (userId.HasValue)
        {
            query = query.Where(item => item.OwnerUserId == userId.Value || item.UserId == userId.Value);
        }

        if (!string.IsNullOrWhiteSpace(ownerType))
        {
            query = query.Where(item => item.OwnerType == ownerType);
        }

        if (!string.IsNullOrWhiteSpace(sceneCode))
        {
            query = query.Where(item => item.SceneCode == sceneCode);
        }

        if (!string.IsNullOrWhiteSpace(invocationKind))
        {
            query = query.Where(item => item.InvocationKind == invocationKind);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(item => item.Status == status);
        }

        var startUtc = EnsureUtc(startDate);
        var endUtc = EnsureUtc(endDate);

        if (startUtc.HasValue)
        {
            query = query.Where(item => item.CreatedAt >= startUtc.Value);
        }

        if (endUtc.HasValue)
        {
            var endOfDay = endUtc.Value.Date.AddDays(1);
            query = query.Where(item => item.CreatedAt < endOfDay);
        }

        return query;
    }

    private async Task<AuditDictionaryDto> GetAuditDictionaryInternalAsync(CancellationToken cancellationToken)
    {
        var scenes = await _dbContext.AuditSceneDefinitions
            .AsNoTracking()
            .Where(item => item.IsEnabled)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.DisplayNameZhCn)
            .Select(item => new AuditDictionaryItemDto
            {
                Code = item.SceneCode,
                DisplayName = item.DisplayNameZhCn
            })
            .ToListAsync(cancellationToken);

        return new AuditDictionaryDto
        {
            Scenes = scenes,
            Owners =
            [
                CreateDictionaryItem(ModelInvocationOwnerTypes.User, "用户"),
                CreateDictionaryItem(ModelInvocationOwnerTypes.System, "系统")
            ],
            Statuses =
            [
                CreateDictionaryItem(ModelInvocationStatuses.Succeeded, "成功"),
                CreateDictionaryItem(ModelInvocationStatuses.Failed, "失败"),
                CreateDictionaryItem(ModelInvocationStatuses.Cancelled, "已取消"),
                CreateDictionaryItem(ModelInvocationStatuses.Timeout, "超时")
            ]
        };
    }
}
