namespace DevNexus.Shared.Constants;

/// <summary>
/// AI Agent 优化相关常量。
/// </summary>
public static class AiOptimizationConstants
{
    /// <summary>
    /// 为输出预留的 Token 数。
    /// </summary>
    public const int OutputReservedTokens = 20000;

    /// <summary>
    /// 历史消息占可用上下文的比例。
    /// </summary>
    public const double HistoryTokenBudgetRatio = 0.3;

    /// <summary>
    /// 工具输出进入模型上下文的最大字符数。
    /// </summary>
    public const int ToolOutputContextMaxChars = 2000;

    /// <summary>
    /// 工具输出摘要的头部字符比例分母。
    /// </summary>
    public const int ToolOutputHeadDivisor = 4;

    /// <summary>
    /// 工具输出摘要的尾部字符比例分子。
    /// </summary>
    public const int ToolOutputTailMultiplier = 3;

    /// <summary>
    /// 默认工具调用评估发布阈值。
    /// </summary>
    public const double ToolEvaluationReleaseThreshold = 0.85;

    /// <summary>
    /// 默认审批等待超时时间。
    /// </summary>
    public static readonly TimeSpan ApprovalTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 审计分析 API 路由常量。
    /// </summary>
    public static class AuditAnalyticsRoutes
    {
        /// <summary>
        /// 审计分析控制器基础路由。
        /// </summary>
        public const string Base = "/api/v1/auditanalytics";

        /// <summary>
        /// AI Agent 优化看板路由。
        /// </summary>
        public const string AiOptimizationDashboard = "ai-optimization-dashboard";
    }

    /// <summary>
    /// 工具调用协议常量。
    /// </summary>
    public static class ToolProtocol
    {
        /// <summary>
        /// 宿主服务插件名。
        /// </summary>
        public const string HostServicePlugin = "HostService";

        /// <summary>
        /// 网络检索插件名。
        /// </summary>
        public const string WebSearchPlugin = "WebSearchPlugin";

        /// <summary>
        /// 知识库检索插件名。
        /// </summary>
        public const string KnowledgeBasePlugin = "KnowledgeBasePlugin";

        /// <summary>
        /// 代码执行插件名。
        /// </summary>
        public const string CodeExecutionPlugin = "CodeExecution";

        /// <summary>
        /// 图像生成插件名。
        /// </summary>
        public const string ImageGenerationPlugin = "ImageGeneration";
    }

    /// <summary>
    /// 工具参数预验证常量。
    /// </summary>
    public static class ToolValidation
    {
        /// <summary>
        /// 文件类参数键。
        /// </summary>
        public static readonly string[] FileArgumentKeys = ["path", "filePath", "directoryPath", "workingDirectory"];

        /// <summary>
        /// 检索类参数键。
        /// </summary>
        public static readonly string[] QueryArgumentKeys = ["query", "url", "prompt", "content", "command"];
    }

    /// <summary>
    /// 工具目录分类。
    /// </summary>
    public static class ToolCategories
    {
        /// <summary>
        /// 研究检索类工具。
        /// </summary>
        public const string Research = "Research";

        /// <summary>
        /// 知识管理类工具。
        /// </summary>
        public const string Knowledge = "Knowledge";

        /// <summary>
        /// 编码执行类工具。
        /// </summary>
        public const string Coding = "Coding";

        /// <summary>
        /// 创意生成类工具。
        /// </summary>
        public const string Creative = "Creative";
    }

    /// <summary>
    /// 工具风险等级。
    /// </summary>
    public static class ToolRiskLevels
    {
        /// <summary>
        /// 低风险。
        /// </summary>
        public const string Low = "Low";

        /// <summary>
        /// 中风险。
        /// </summary>
        public const string Medium = "Medium";

        /// <summary>
        /// 高风险。
        /// </summary>
        public const string High = "High";
    }

    /// <summary>
    /// 工具目录展示名称。
    /// </summary>
    public static class ToolDisplayNames
    {
        /// <summary>
        /// 网络检索。
        /// </summary>
        public const string WebSearch = "网络检索";

        /// <summary>
        /// 知识库检索。
        /// </summary>
        public const string KnowledgeBase = "知识库检索";

        /// <summary>
        /// 宿主文件与命令。
        /// </summary>
        public const string HostService = "宿主文件与命令";

        /// <summary>
        /// 代码执行。
        /// </summary>
        public const string CodeExecution = "代码执行";

        /// <summary>
        /// 图像生成。
        /// </summary>
        public const string ImageGeneration = "图像生成";
    }

    /// <summary>
    /// 工具结果契约说明。
    /// </summary>
    public static class ToolResultContracts
    {
        /// <summary>
        /// 网络检索结果契约。
        /// </summary>
        public const string WebSearch = "返回来源、摘要和可继续读取的链接。";

        /// <summary>
        /// 知识库检索结果契约。
        /// </summary>
        public const string KnowledgeBase = "返回知识库片段、相似度和来源标识。";

        /// <summary>
        /// 宿主文件与命令结果契约。
        /// </summary>
        public const string HostService = "返回结构化命令结果、退出码、标准输出和错误摘要。";

        /// <summary>
        /// 代码执行结果契约。
        /// </summary>
        public const string CodeExecution = "返回执行状态、输出摘要和错误分类。";

        /// <summary>
        /// 图像生成结果契约。
        /// </summary>
        public const string ImageGeneration = "返回图片生成任务状态、资源地址和失败原因。";
    }

    /// <summary>
    /// 工具验证中文提示。
    /// </summary>
    public static class ToolValidationMessages
    {
        /// <summary>
        /// 缺少工具名称。
        /// </summary>
        public const string MissingToolName = "缺少工具名称。";

        /// <summary>
        /// 缺少工具参数。
        /// </summary>
        public const string MissingArguments = "缺少工具参数。";

        /// <summary>
        /// 工具参数不是合法 JSON。
        /// </summary>
        public const string InvalidJson = "工具参数不是合法 JSON。";

        /// <summary>
        /// 工具参数为空。
        /// </summary>
        public const string EmptyArguments = "工具参数为空。";

        /// <summary>
        /// 文件或工作目录参数不能为空。
        /// </summary>
        public const string BlankFileArgument = "文件或工作目录参数不能为空。";

        /// <summary>
        /// 检索参数不能为空。
        /// </summary>
        public const string BlankQueryArgument = "检索参数不能为空。";
    }
}
