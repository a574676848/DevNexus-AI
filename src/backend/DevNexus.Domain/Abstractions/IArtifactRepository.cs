using DevNexus.Domain.Entities;

namespace DevNexus.Domain.Abstractions;

/// <summary>
/// Artifact 仓储接口。
/// </summary>
public interface IArtifactRepository
{
    /// <summary>
    /// 新增 Artifact。
    /// </summary>
    Task AddAsync(Artifact artifact, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据 ID 获取 Artifact。
    /// </summary>
    Task<Artifact?> GetByIdAsync(Guid artifactId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据消息 ID 获取 Artifact 列表。
    /// </summary>
    Task<IReadOnlyList<Artifact>> ListByMessageIdAsync(Guid messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据会话和消息范围获取 Artifact 列表。
    /// </summary>
    Task<IReadOnlyList<Artifact>> ListBySessionAsync(
        Guid sessionId,
        IReadOnlyCollection<Guid> messageIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新 Artifact。
    /// </summary>
    Task UpdateAsync(Artifact artifact, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除 Artifact。
    /// </summary>
    Task<bool> DeleteAsync(Guid artifactId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除会话下全部 Artifact。
    /// </summary>
    Task<int> DeleteBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 将多个 Artifact 关联到指定消息。
    /// </summary>
    Task<int> LinkToMessageAsync(
        IReadOnlyCollection<Guid> artifactIds,
        Guid messageId,
        Guid sessionId,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken = default);
}
