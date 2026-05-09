namespace DevNexus.Shared.Constants;

/// <summary>
/// SmartDocument 相关协议常量入口。
/// </summary>
public static class SmartDocumentConstants
{
    /// <summary>
    /// SmartDocument.Metadata 键定义。
    /// </summary>
    public static class MetadataKeys
    {
        public const string SourceUrl = "SourceUrl";
        public const string FileSize = "FileSize";
        public const string UploadedAt = "UploadedAt";
        public const string FileAssetId = "FileAssetId";
        public const string CurrentVersionId = "CurrentVersionId";
        public const string StorageProvider = "StorageProvider";
        public const string ObjectKey = "ObjectKey";
        public const string FileAssetStatus = "FileAssetStatus";
        public const string ContextMode = "ContextMode";
        public const string SemanticPipelineStage = "SemanticPipelineStage";
        public const string AssetOnlyContext = "AssetOnlyContext";
        public const string SemanticDisabledReason = "SemanticDisabledReason";
        public const string ParseErrorMessage = "ParseErrorMessage";
        public const string OriginScope = "OriginScope";
        public const string SourceType = "SourceType";
        public const string EffectiveMimeType = "EffectiveMimeType";
        public const string DeclaredMimeType = "DeclaredMimeType";
        public const string SemanticParsedAt = "SemanticParsedAt";
        public const string VectorIndexStatus = "VectorIndexStatus";
        public const string VectorIndexedAt = "VectorIndexedAt";
        public const string VectorIndexError = "VectorIndexError";
        public const string RetryCount = "RetryCount";
        public const string LastRetryAt = "LastRetryAt";
        public const string RetryRequestedAt = "RetryRequestedAt";
    }

    /// <summary>
    /// 附件发送时导出的 artifact metadata 键定义。
    /// </summary>
    public static class ArtifactMetadataKeys
    {
        public const string DocumentSourceType = "DocumentSourceType";
        public const string ParsingStatus = "ParsingStatus";
        public const string MimeType = "MimeType";
        public const string IsExecutableReady = "IsExecutableReady";
        public const string IsSemanticReady = "IsSemanticReady";
        public const string UseSemanticContext = "UseSemanticContext";
        public const string UseExecutionContext = "UseExecutionContext";
        public const string IsAssetOnlyContext = "IsAssetOnlyContext";
        public const string IsTaskOutputReuse = "IsTaskOutputReuse";
        public const string CapabilityTag = "CapabilityTag";
        public const string SemanticStatusLabel = "SemanticStatusLabel";
        public const string SemanticPipelineStageLabel = "SemanticPipelineStageLabel";
        public const string ExecutionStatusLabel = "ExecutionStatusLabel";
        public const string SourceLabel = "SourceLabel";
    }

    /// <summary>
    /// 语义流水线阶段协议值。
    /// </summary>
    public static class SemanticPipelineStages
    {
        public const string NotRequested = "NotRequested";
        public const string Pending = ArtifactStatusConstants.Pending;
        public const string Processing = ArtifactStatusConstants.Processing;
        public const string Parsed = ArtifactStatusConstants.Parsed;
        public const string Indexing = ArtifactStatusConstants.Indexing;
        public const string Completed = ArtifactStatusConstants.Completed;
    }

    /// <summary>
    /// 向量索引状态协议值。
    /// </summary>
    public static class VectorIndexStatuses
    {
        public const string Building = "Building";
        public const string Ready = "Ready";
        public const string Failed = "Failed";
    }

    /// <summary>
    /// SmartDocument 复用来源范围。
    /// </summary>
    public static class OriginScopes
    {
        public const string TaskOutput = "TaskOutput";
    }

    /// <summary>
    /// 文档解析 thinking 元信息定义。
    /// </summary>
    public static class ParsingThinking
    {
        public const string SourceName = "SmartDocumentParsing";
        public const string SourceMetadataKey = "source";
        public const string StartTemplate = "🗂️ 正在解析文档: {0}";
        public const string CompletedMessage = "✅ 文档解析完成，可用于引用。";
        public const string FailedTemplate = "❌ 文档解析失败: {0}";
    }
}
