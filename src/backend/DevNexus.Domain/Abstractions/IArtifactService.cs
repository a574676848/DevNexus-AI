using DevNexus.Shared.DTOs;

namespace DevNexus.Domain.Abstractions;

/// <summary>
/// Artifact 服务接口
/// 处理 Artifact 的识别、创建、推流
/// </summary>
public interface IArtifactService
{
    /// <summary>
    /// 从内容中识别并提取 Artifact
    /// </summary>
    /// <param name="content">内容</param>
    /// <param name="messageId">消息ID</param>
    /// <returns>识别到的 Artifact 列表</returns>
    Task<List<ArtifactDto>> ExtractArtifactsAsync(string content, Guid messageId);
    
    /// <summary>
    /// 创建 Artifact
    /// </summary>
    /// <param name="artifact">Artifact 数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>创建的 Artifact</returns>
    Task<ArtifactDto> CreateArtifactAsync(ArtifactDto artifact, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 更新 Artifact
    /// </summary>
    /// <param name="artifactId">Artifact ID</param>
    /// <param name="content">新内容</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新后的 Artifact</returns>
    Task<ArtifactDto> UpdateArtifactAsync(Guid artifactId, string content, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取 Artifact
    /// </summary>
    /// <param name="artifactId">Artifact ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>Artifact</returns>
    Task<ArtifactDto?> GetArtifactAsync(Guid artifactId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取消息的所有 Artifacts
    /// </summary>
    /// <param name="messageId">消息ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>Artifact 列表</returns>
    Task<List<ArtifactDto>> GetMessageArtifactsAsync(Guid messageId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取会话的所有 Artifacts
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>Artifact 列表</returns>
    Task<List<ArtifactDto>> GetSessionArtifactsAsync(Guid sessionId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 删除 Artifact
    /// </summary>
    /// <param name="artifactId">Artifact ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    Task<bool> DeleteArtifactAsync(Guid artifactId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 将多个 Artifact 关联到指定消息
    /// 在创建用户消息后调用，更新附带文档的 MessageId
    /// </summary>
    /// <param name="artifactIds">Artifact ID 列表</param>
    /// <param name="messageId">消息 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>成功更新的数量</returns>
    Task<int> LinkArtifactsToMessageAsync(IEnumerable<Guid> artifactIds, Guid messageId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 更新 Artifact 的 Metadata（合并方式）
    /// </summary>
    /// <param name="artifactId">Artifact ID</param>
    /// <param name="metadata">要更新的元数据（新值覆盖旧值）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新后的 Artifact</returns>
    Task<ArtifactDto> UpdateArtifactMetadataAsync(
        Guid artifactId,
        Dictionary<string, object> metadata,
        CancellationToken cancellationToken = default);
}
