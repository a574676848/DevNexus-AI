namespace DevNexus.Shared.DTOs;

/// <summary>
/// 模型调用主体类型。
/// </summary>
public static class ModelInvocationOwnerTypes
{
    /// <summary>
    /// 用户调用。
    /// </summary>
    public const string User = "user";

    /// <summary>
    /// 系统调用。
    /// </summary>
    public const string System = "system";
}

/// <summary>
/// 模型调用提供商类型。
/// </summary>
public static class ModelInvocationProviderTypes
{
    public const string Llm = "llm";
    public const string Embedding = "embedding";
}

/// <summary>
/// 模型调用技术类型。
/// </summary>
public static class ModelInvocationKinds
{
    public const string ChatCompletion = "chat_completion";
    public const string StreamingChat = "streaming_chat";
    public const string Embedding = "embedding";
    public const string Vision = "vision";
    public const string ImageGeneration = "image_generation";
    public const string Evaluation = "evaluation";
    public const string FunctionCall = "function_call";
    public const string Other = "other";
}

/// <summary>
/// 模型调用场景分组。
/// </summary>
public static class ModelInvocationSceneCategories
{
    public const string UserFacing = "user_facing";
    public const string Background = "background";
    public const string Swarm = "swarm";
    public const string Memory = "memory";
    public const string Parsing = "parsing";
    public const string Governance = "governance";
    public const string Other = "other";
}

/// <summary>
/// 模型调用资源类型。
/// </summary>
public static class ModelInvocationResourceTypes
{
    public const string Message = "message";
    public const string Session = "session";
    public const string ContextWorkPackageRecord = "context_work_package";
    public const string BackgroundJob = "background_job";
    public const string MemoryRecord = "memory_record";
    public const string Artifact = "artifact";
    public const string None = "none";
}

/// <summary>
/// 计量类型。
/// </summary>
public static class ModelInvocationMeteringTypes
{
    public const string Token = "token";
    public const string Request = "request";
    public const string Image = "image";
    public const string Character = "character";
    public const string Unknown = "unknown";
}

/// <summary>
/// 使用量来源。
/// </summary>
public static class ModelInvocationUsageSources
{
    public const string Actual = "actual";
    public const string Estimated = "estimated";
    public const string None = "none";
}

/// <summary>
/// 审计状态。
/// </summary>
public static class ModelInvocationStatuses
{
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
    public const string Timeout = "timeout";
}

/// <summary>
/// 常用场景编码。
/// </summary>
public static class ModelInvocationSceneCodes
{
    public const string ChatMessageReply = "chat.message.reply";
    public const string ChatSessionTitle = "chat.session.title";
    public const string SwarmComplexityEvaluate = "swarm.complexity.evaluate";
    public const string SwarmWorkPackageDecompose = "swarm.work_package.decompose";
    public const string SwarmWorkPackageExecute = "swarm.work_package.execute";
    public const string SwarmWorkPackageRepair = "swarm.work_package.repair";
    public const string SwarmWorkPackageGroupChatRound = "swarm.work_package.group_chat.round";
    public const string SwarmWorkPackageGroupChatSummary = "swarm.work_package.group_chat.summary";
    public const string EvaluationResponseReview = "evaluation.response.review";
    public const string MemoryUserFactExtract = "memory.user_fact.extract";
    public const string MemorySessionSummary = "memory.session.summary";
    public const string MemorySessionTags = "memory.session.tags";
    public const string MemoryUserEmbedding = "memory.user_embedding";
    public const string MemorySystemExperienceDistill = "memory.system_experience.distill";
    public const string KnowledgeEmbeddingIndex = "knowledge.embedding.index";
    public const string ParsingOcrCleanup = "parsing.ocr.cleanup";
    public const string VisionImageUnderstanding = "vision.image_understanding";
    public const string GenerationImageCreate = "generation.image.create";
    public const string RoutingAgentSelect = "routing.agent.select";
    public const string GenerationAgentProfile = "generation.agent.profile";
    public const string ContextSummary = "context.summary";
    public const string HandoffStructuredOutput = "handoff.structured_output";
    public const string ToolFunctionCall = "tool.function_call";
    public const string SystemOther = "system.other";
}
