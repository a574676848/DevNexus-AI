namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 系统经验提纯输出协议。
/// </summary>
public static class ExperienceDistillationOutputProtocol
{
    /// <summary>
    /// 输出协议版本。
    /// </summary>
    public const string Version = "experience-distillation-output:v1";

    /// <summary>
    /// 上下文标签中的提纯协议前缀。
    /// </summary>
    public const string ContextTagPrefix = "distillation-protocol:";

    /// <summary>
    /// 当前提纯协议上下文标签。
    /// </summary>
    public const string ContextTag = ContextTagPrefix + Version;

    /// <summary>
    /// 自我迭代候选原因标签前缀。
    /// </summary>
    public const string CandidateReasonTagPrefix = "self-iteration-candidate:";

    /// <summary>
    /// 上下文压力原因标签前缀。
    /// </summary>
    public const string ContextPressureReasonTagPrefix = "context-pressure:";

    /// <summary>
    /// 上下文压缩摘要指纹标签前缀。
    /// </summary>
    public const string ContextCompressionFingerprintTagPrefix = "context-compression-fingerprint:";

    /// <summary>
    /// 经验提纯 Prompt 指纹标签前缀。
    /// </summary>
    public const string DistillationPromptFingerprintTagPrefix = "distillation-prompt-fingerprint:";

    /// <summary>
    /// 长期价值信号标签前缀。
    /// </summary>
    public const string ValueSignalTagPrefix = "value-signal:";

    /// <summary>
    /// 经验来源会话标签前缀。
    /// </summary>
    public const string SourceSessionTagPrefix = "source-session:";

    /// <summary>
    /// 无经验价值标记。
    /// </summary>
    public const string NoValueMarker = "NONE";

    /// <summary>
    /// 意图标记。
    /// </summary>
    public const string IntentMarker = "[INTENT]";

    /// <summary>
    /// 可持久化 SOP 最大字符数。
    /// </summary>
    public const int MaximumSopCharacters = 4000;

    /// <summary>
    /// 高价值经验信号。
    /// </summary>
    public static readonly string[] HighValueSignals =
    [
        "明确的技术、产品或流程决策",
        "新的长期项目背景、约束或目标",
        "对既有认知的纠正或避坑经验",
        "用户明确表达的偏好、规范或协作方式"
    ];

    /// <summary>
    /// 高价值经验信号关键词。
    /// </summary>
    public static readonly string[] HighValueSignalKeywords =
    [
        "决策", "决定", "约定", "规范", "偏好", "原则", "架构", "边界", "定位", "流程",
        "闭环", "复盘", "踩坑", "避坑", "原因", "修复", "SOP", "迁移", "约束", "不要",
        "必须", "应该", "以后", "后续", "记住", "remember", "preference", "decision",
        "constraint", "workflow", "postmortem", "migration", "architecture"
    ];

    /// <summary>
    /// 不应提纯为长期经验的内容。
    /// </summary>
    public static readonly string[] SkipConditions =
    [
        "一次性解释、概念科普或普通问答",
        "仅运行测试、修复格式、提交或部署",
        "没有形成可复用 SOP 的临时排查",
        "与既有经验重复或只是轻微改写",
        "原始 QA、日志、命令输出或临时调试记录"
    ];

    /// <summary>
    /// 长期经验正文中禁止出现的原始记录标记。
    /// </summary>
    public static readonly string[] RawTranscriptMarkers =
    [
        "【用户问题】",
        "【助手回答】",
        "User:",
        "Assistant:",
        "Q:",
        "A:",
        "```",
        "[SUCCESS]",
        "[FAILURE]",
        "[EXCEPTION]"
    ];

    /// <summary>
    /// 不应进入提纯的跳过条件关键词。
    /// </summary>
    public static readonly string[] SkipConditionKeywords =
    [
        "一次性解释", "概念科普", "普通问答", "运行测试", "修复格式", "提交", "部署",
        "临时排查", "轻微改写", "重复经验", "run tests", "fix formatting", "commit",
        "deploy", "one-off", "temporary investigation"
    ];
}
