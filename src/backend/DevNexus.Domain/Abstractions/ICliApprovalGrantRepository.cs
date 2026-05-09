using DevNexus.Domain.Entities;
using DevNexus.Shared.Enums;

namespace DevNexus.Domain.Abstractions;

/// <summary>
/// CLI 审批授权仓储接口。
/// </summary>
public interface ICliApprovalGrantRepository
{
    /// <summary>
    /// 获取指定范围内仍有效的授权记录。
    /// </summary>
    Task<CliApprovalGrant?> GetActiveGrantAsync(
        string sessionScopeKey,
        CliApprovalGrantScope scope,
        string matchValue,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增授权记录。
    /// </summary>
    Task AddAsync(CliApprovalGrant grant, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新授权记录。
    /// </summary>
    Task UpdateAsync(CliApprovalGrant grant, CancellationToken cancellationToken = default);
}
