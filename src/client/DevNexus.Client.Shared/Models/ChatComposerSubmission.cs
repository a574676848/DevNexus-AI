using DevNexus.Shared.DTOs;

namespace DevNexus.Client.Shared.Models;

/// <summary>
/// 聊天输入框提交模型
/// 用于承载发送消息时的工具、Skill 和附件上下文。
/// </summary>
public class ChatComposerSubmission
{
    /// <summary>
    /// 消息内容
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// 选中的 Provider
    /// </summary>
    public Guid? ProviderId { get; init; }

    /// <summary>
    /// 关联的 Artifact 标识
    /// </summary>
    public List<Guid>? ArtifactIds { get; init; }

    /// <summary>
    /// 本轮已创建的 Artifact
    /// </summary>
    public List<ArtifactDto>? Artifacts { get; init; }

    /// <summary>
    /// 是否启用 RAG
    /// </summary>
    public bool EnableRag { get; init; } = true;

    /// <summary>
    /// 显式指定的 Skill 名称
    /// </summary>
    public string? SelectedSkillName { get; init; }

    /// <summary>
    /// 附加上下文
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }
}