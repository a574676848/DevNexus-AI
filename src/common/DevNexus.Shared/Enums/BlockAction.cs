namespace DevNexus.Shared.Enums;

/// <summary>
/// Block 操作类型枚举
/// 用于标识 Artifact 的创建、更新或删除操作
/// </summary>
public enum BlockAction
{
    /// <summary>
    /// 创建新 Artifact
    /// </summary>
    Create,
    
    /// <summary>
    /// 更新现有 Artifact（增量更新）
    /// </summary>
    Update,
    
    /// <summary>
    /// 删除 Artifact
    /// </summary>
    Delete
}
