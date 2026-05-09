namespace DevNexus.Shared.Constants;

/// <summary>
/// 记忆与语义检索相关的系统级常量
/// </summary>
public static class MemoryConstants
{
    /// <summary>
    /// 全局系统经验库的向量索引名称
    /// </summary>
    public const string ExperienceIndex = "agent-experiential-memory";
    
    /// <summary>
    /// Swarm 编解码完全命中的阈值 (跳过大模型零延迟返回)
    /// </summary>
    public const float SwarmDagPerfectHitThreshold = 0.95f;
    
    /// <summary>
    /// Swarm 编解码部分命中的阈值 (用于 Few-shot 注入)
    /// </summary>
    public const float SwarmDagPartialHitThreshold = 0.80f;
    
    /// <summary>
    /// 聊天缓存完全命中的阈值 (零延迟回复)
    /// </summary>
    public const float ChatPerfectHitThreshold = 0.95f;
    
    /// <summary>
    /// 聊天缓存部分命中的阈值 (作为内部参考提供给模型)
    /// </summary>
    public const float ChatPartialHitThreshold = 0.85f;
}
