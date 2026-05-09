namespace DevNexus.Shared.Constants;

/// <summary>
/// 聊天消息 metadata 的共享协议键和值定义。
/// </summary>
public static class ChatMessageMetadataKeys
{
    /// <summary>
    /// 选中的 Skill 名称。
    /// </summary>
    public const string SelectedSkillName = "selectedSkillName";

    /// <summary>
    /// 命中缓存标记。
    /// </summary>
    public const string CacheHit = "cacheHit";

    /// <summary>
    /// 语义相似度。
    /// </summary>
    public const string Similarity = "similarity";

    /// <summary>
    /// Swarm 模式标记。
    /// </summary>
    public const string SwarmMode = "swarmMode";

    /// <summary>
    /// 客户端投递状态文案。
    /// </summary>
    public const string ClientDeliveryState = "clientDeliveryState";

    /// <summary>
    /// 客户端投递状态语气。
    /// </summary>
    public const string ClientDeliveryTone = "clientDeliveryTone";

    /// <summary>
    /// 客户端进入动画标记。
    /// </summary>
    public const string ClientEntryAnimation = "clientEntryAnimation";

    /// <summary>
    /// 发送中文案。
    /// </summary>
    public const string DeliveryStateSending = "发送中";

    /// <summary>
    /// 已发送文案。
    /// </summary>
    public const string DeliveryStateSent = "已发送";

    /// <summary>
    /// 成功语气。
    /// </summary>
    public const string DeliveryToneSuccess = "success";

    /// <summary>
    /// 中性语气。
    /// </summary>
    public const string DeliveryToneNeutral = "neutral";

    /// <summary>
    /// 新消息入场动画。
    /// </summary>
    public const string EntryAnimationFresh = "fresh";

    /// <summary>
    /// 重发消息动画。
    /// </summary>
    public const string EntryAnimationReplay = "replay";

    /// <summary>
    /// 判断 metadata 是否标记为 Swarm 模式。
    /// </summary>
    public const string PendingInteractionId = "pendingInteractionId";

    /// <summary>
    /// 恢复挂起交互的消息标记。
    /// </summary>
    public const string ResumePendingInteraction = "resumePendingInteraction";

    /// <summary>
    /// 判断 metadata 是否标记为 Swarm 模式。
    /// </summary>
    public static bool IsSwarmMode(IReadOnlyDictionary<string, object>? metadata)
    {
        if (metadata == null || !metadata.TryGetValue(SwarmMode, out var value) || value == null)
        {
            return false;
        }

        return bool.TryParse(value.ToString(), out var enabled) && enabled;
    }
}
