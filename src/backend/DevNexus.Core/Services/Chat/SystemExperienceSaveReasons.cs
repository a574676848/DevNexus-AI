namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 系统经验保存结果原因。
/// </summary>
public static class SystemExperienceSaveReasons
{
    /// <summary>
    /// 新经验已保存并完成向量索引。
    /// </summary>
    public const string CreatedAndIndexed = "CreatedAndIndexed";

    /// <summary>
    /// 新经验已保存，但向量索引失败。
    /// </summary>
    public const string CreatedButIndexFailed = "CreatedButIndexFailed";

    /// <summary>
    /// 重复经验已跳过。
    /// </summary>
    public const string DuplicateSkipped = "DuplicateSkipped";
}
