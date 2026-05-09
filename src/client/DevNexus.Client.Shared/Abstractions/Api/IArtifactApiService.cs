using DevNexus.Shared.DTOs;

namespace DevNexus.Client.Shared.Abstractions;

/// <summary>
/// Artifact API 服务接口
/// </summary>
public interface IArtifactApiService
{
    /// <summary>
    /// 创建 Artifact
    /// </summary>
    Task<ArtifactDto> CreateArtifactAsync(CreateArtifactRequestDto request);

    /// <summary>
    /// 获取 Artifact
    /// </summary>
    Task<ArtifactDto?> GetArtifactAsync(Guid artifactId);

    /// <summary>
    /// 获取会话的所有 Artifacts
    /// </summary>
    Task<List<ArtifactDto>> GetSessionArtifactsAsync(Guid sessionId);

    /// <summary>
    /// 获取消息的所有 Artifacts
    /// </summary>
    Task<List<ArtifactDto>> GetMessageArtifactsAsync(Guid messageId);

    /// <summary>
    /// 更新 Artifact 内容
    /// </summary>
    Task<ArtifactDto> UpdateArtifactAsync(Guid artifactId, string content);

    /// <summary>
    /// 删除 Artifact
    /// </summary>
    Task DeleteArtifactAsync(Guid artifactId);

    /// <summary>
    /// 解析文档内容（不创建 Artifact）
    /// 适用于 Code/Word/PDF/Image 等需要后端解析的文件类型
    /// Artifact 由客户端在发送消息时单独创建
    /// </summary>
    Task<ParseDocumentResponse> ParseDocumentAsync(ParseDocumentRequest request);

    /// <summary>
    /// 查询异步解析状态（SignalR 丢事件时用于兜底轮询）
    /// </summary>
    Task<ArtifactStatusDto?> GetParseStatusAsync(string traceId);
}

/// <summary>
/// 创建 Artifact 请求 DTO（前端使用）
/// </summary>
public class CreateArtifactRequestDto
{
    /// <summary>
    /// 语义标识符（由 LLM 指定，用于引用和增量更新）
    /// </summary>
    public string? SemanticId { get; set; }

    /// <summary>
    /// 版本号
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// 父 Artifact ID（用于版本链）
    /// </summary>
    public Guid? ParentArtifactId { get; set; }

    /// <summary>
    /// Artifact 类型
    /// </summary>
    public string Type { get; set; } = "Markdown";

    /// <summary>
    /// Artifact 名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Artifact 内容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 关联的文件资产 ID
    /// </summary>
    public Guid? FileAssetId { get; set; }

    /// <summary>
    /// 关联的文件版本 ID
    /// </summary>
    public Guid? FileVersionId { get; set; }

    /// <summary>
    /// 会话 ID
    /// </summary>
    public Guid? SessionId { get; set; }

    /// <summary>
    /// 消息 ID
    /// </summary>
    public Guid? MessageId { get; set; }

    /// <summary>
    /// 元数据（用于存储卡片状态等扩展信息）
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}

