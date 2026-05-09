using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using DevNexus.Shared.Constants;
using System.Text.Json;
using FileAssetStatusEnum = DevNexus.Shared.Enums.FileAssetStatus;

namespace DevNexus.Client.Shared.Models;

/// <summary>
/// 文档来源类型
/// </summary>
public enum DocumentSourceType
{
    /// <summary>
    /// 粘贴的长文本
    /// </summary>
    Pasted,
    
    /// <summary>
    /// 代码片段
    /// </summary>
    Code,
    
    /// <summary>
    /// 上传的文件
    /// </summary>
    Uploaded
}

public enum DocumentContextMode
{
    ExecuteOnly,
    SemanticOnly,
    Both
}

public sealed class DocumentContextModeChange
{
    public Guid DocumentId { get; set; }
    public DocumentContextMode Mode { get; set; }
}

/// <summary>
/// 粘贴文档数据模型
/// 以 SmartDocument 为核心数据结构，提供计算属性访问
/// </summary>
public class PastedDocument
{
    /// <summary>
    /// 文档唯一标识
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 核心数据 - SmartDocument
    /// </summary>
    public SmartDocument SmartDocument { get; set; } = new();

    /// <summary>
    /// 文档来源类型
    /// </summary>
    public DocumentSourceType SourceType { get; set; } = DocumentSourceType.Pasted;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ========== 计算属性 - 从 SmartDocument 提取 ==========

    /// <summary>
    /// 文件名
    /// </summary>
    public string FileName => SmartDocument.FileName;

    /// <summary>
    /// MIME 类型
    /// </summary>
    public string MimeType => SmartDocument.MimeType;

    /// <summary>
    /// 文件大小（字节）
    /// </summary>
    public long FileSizeBytes => SmartDocument.SizeBytes;

    /// <summary>
    /// 提取文本内容（用于 LLM 上下文）
    /// </summary>
    public string Content => SmartDocument.Content switch
    {
        TextDocumentContent t => t.Text ?? string.Empty,
        CodeDocumentContent c => c.Text ?? string.Empty,
        ImageDocumentContent i => i.Description ?? i.Url ?? string.Empty,
        TableDocumentContent tb => tb.CsvRepresentation ?? string.Empty,
        _ => string.Empty
    };

    /// <summary>
    /// 行数
    /// </summary>
    public int LineCount => SmartDocument.Content switch
    {
        CodeDocumentContent c => c.Metrics?.TotalLines ?? (c.Text?.Split('\n').Length ?? 0),
        TextDocumentContent t => t.Text?.Split('\n').Length ?? 0,
        TableDocumentContent tb => tb.RowCount,
        _ => 0
    };

    /// <summary>
    /// 字符数
    /// </summary>
    public int CharacterCount => Content.Length;

    /// <summary>
    /// 编程语言（代码文件专用）
    /// </summary>
    public string? Language => (SmartDocument.Content as CodeDocumentContent)?.Language;

    /// <summary>
    /// 页数（PDF 专用）
    /// </summary>
    public int PageCount => (SmartDocument.Content as TextDocumentContent)?.PageCount ?? 0;

    /// <summary>
    /// 列数（表格专用）
    /// </summary>
    public int ColumnCount => (SmartDocument.Content as TableDocumentContent)?.ColumnCount ?? 0;

    /// <summary>
    /// 获取友好的文件大小显示
    /// </summary>
    public string DisplaySize
    {
        get
        {
            var bytes = FileSizeBytes > 0 ? FileSizeBytes : System.Text.Encoding.UTF8.GetByteCount(Content);
            if (bytes < 1024)
                return $"{bytes} B";
            if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / (1024.0 * 1024):F1} MB";
        }
    }

    /// <summary>
    /// 获取内容类型标识
    /// </summary>
    public string ContentType => SmartDocument.Content?.ContentType ?? "unknown";

    /// <summary>
    /// 获取图标类名（根据内容类型）
    /// </summary>
    public string IconClass => SmartDocument.Content switch
    {
        CodeDocumentContent => "fa-solid fa-code",
        ImageDocumentContent => "fa-solid fa-image",
        TableDocumentContent => "fa-solid fa-table",
        TextDocumentContent t when t.HasTables => "fa-solid fa-file-excel",
        TextDocumentContent t when SmartDocument.MimeType.Contains("pdf") => "fa-solid fa-file-pdf",
        TextDocumentContent t when SmartDocument.MimeType.Contains("word") => "fa-solid fa-file-word",
        _ => "fa-solid fa-file-lines"
    };

    /// <summary>
    /// 获取简短的类型标签
    /// </summary>
    public string TypeTag => SmartDocument.Content switch
    {
        CodeDocumentContent c => c.Language?.ToUpper() ?? "CODE",
        ImageDocumentContent => "IMG",
        TableDocumentContent => "TABLE",
        TextDocumentContent when SmartDocument.MimeType.Contains("pdf") => "PDF",
        TextDocumentContent when SmartDocument.MimeType.Contains("word") => "DOC",
        TextDocumentContent when SmartDocument.MimeType.Contains("markdown") => "MD",
        _ => "TXT"
    };

    /// <summary>
    /// 是否已绑定文件资产
    /// </summary>
    public bool HasFileAsset => FileAssetId.HasValue;

    /// <summary>
    /// 文件资产状态
    /// </summary>
    public FileAssetStatusEnum? FileAssetStatus => TryGetEnumMetadata<FileAssetStatusEnum>(SmartDocumentConstants.MetadataKeys.FileAssetStatus);

    /// <summary>
    /// 文件资产 ID
    /// </summary>
    public Guid? FileAssetId => TryGetGuidMetadata(SmartDocumentConstants.MetadataKeys.FileAssetId);

    /// <summary>
    /// 当前版本 ID
    /// </summary>
    public Guid? CurrentVersionId => TryGetGuidMetadata(SmartDocumentConstants.MetadataKeys.CurrentVersionId);

    /// <summary>
    /// 源文件地址
    /// </summary>
    public string? SourceUrl => TryGetStringMetadata(SmartDocumentConstants.MetadataKeys.SourceUrl);

    /// <summary>
    /// 存储提供商
    /// </summary>
    public string? StorageProvider => TryGetStringMetadata(SmartDocumentConstants.MetadataKeys.StorageProvider);

    /// <summary>
    /// 存储对象键
    /// </summary>
    public string? ObjectKey => TryGetStringMetadata(SmartDocumentConstants.MetadataKeys.ObjectKey);

    /// <summary>
    /// 解析失败原因
    /// </summary>
    public string? FailureReason => TryGetStringMetadata(SmartDocumentConstants.MetadataKeys.ParseErrorMessage);

    /// <summary>
    /// 语义不可用原因
    /// </summary>
    public string? SemanticDisabledReason => TryGetStringMetadata(SmartDocumentConstants.MetadataKeys.SemanticDisabledReason);

    /// <summary>
    /// 是否已完成语义准备
    /// </summary>
    public bool IsSemanticReady => !IsAssetOnlyContext && SmartDocument.Status == ParsingStatus.Completed;

    /// <summary>
    /// 是否具备可执行文件上下文
    /// </summary>
    public bool IsExecutableReady
    {
        get
        {
            if (!HasFileAsset)
            {
                return false;
            }

            return FileAssetStatus switch
            {
                FileAssetStatusEnum.Failed => false,
                FileAssetStatusEnum.Archived => false,
                _ => true
            };
        }
    }

    /// <summary>
    /// 是否仅作为资产引用回挂到输入区
    /// </summary>
    public bool IsAssetOnlyContext => TryGetBoolMetadata(SmartDocumentConstants.MetadataKeys.AssetOnlyContext);

    /// <summary>
    /// 是否来自上一轮任务输出
    /// </summary>
    public bool IsTaskOutputReuse => string.Equals(
    TryGetStringMetadata(SmartDocumentConstants.MetadataKeys.OriginScope),
    SmartDocumentConstants.OriginScopes.TaskOutput,
        StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 当前上下文模式
    /// </summary>
    public DocumentContextMode ContextMode
    {
        get
        {
            var fromMetadata = TryGetEnumMetadata<DocumentContextMode>(SmartDocumentConstants.MetadataKeys.ContextMode);
            if (fromMetadata.HasValue)
            {
                return fromMetadata.Value;
            }

            if (IsAssetOnlyContext)
            {
                return DocumentContextMode.ExecuteOnly;
            }

            return HasFileAsset ? DocumentContextMode.Both : DocumentContextMode.SemanticOnly;
        }
    }

    /// <summary>
    /// 是否启用语义上下文
    /// </summary>
    public bool UseSemanticContext => ContextMode is DocumentContextMode.SemanticOnly or DocumentContextMode.Both;

    /// <summary>
    /// 是否启用执行上下文
    /// </summary>
    public bool UseExecutionContext => IsExecutableReady && ContextMode is DocumentContextMode.ExecuteOnly or DocumentContextMode.Both;

    /// <summary>
    /// 显示状态标签
    /// </summary>
    public string DisplayStatus => SemanticStatusLabel;

    /// <summary>
    /// 语义准备状态标签
    /// </summary>
    public string SemanticStatusLabel
    {
        get
        {
            if (IsAssetOnlyContext)
            {
                return "语义不参与";
            }

            return SmartDocument.Status switch
            {
                ParsingStatus.Completed => "语义可用",
                ParsingStatus.Processing => "语义准备中",
                ParsingStatus.Pending => "语义待提取",
                ParsingStatus.Failed => "语义失败",
                _ => "语义未知"
            };
        }
    }

    /// <summary>
    /// 语义流水线阶段标签
    /// </summary>
    public string SemanticPipelineStageLabel
    {
        get
        {
            var stage = TryGetStringMetadata(SmartDocumentConstants.MetadataKeys.SemanticPipelineStage);
            if (string.Equals(stage, SmartDocumentConstants.SemanticPipelineStages.Pending, StringComparison.OrdinalIgnoreCase))
            {
                return "解析排队中";
            }

            if (string.Equals(stage, SmartDocumentConstants.SemanticPipelineStages.NotRequested, StringComparison.OrdinalIgnoreCase))
            {
                return "入库成功，解析未触发";
            }

            if (string.Equals(stage, SmartDocumentConstants.SemanticPipelineStages.Processing, StringComparison.OrdinalIgnoreCase))
            {
                return "语义解析中";
            }

            if (string.Equals(stage, SmartDocumentConstants.SemanticPipelineStages.Parsed, StringComparison.OrdinalIgnoreCase))
            {
                return "解析完成，向量待构建";
            }

            if (string.Equals(stage, SmartDocumentConstants.SemanticPipelineStages.Indexing, StringComparison.OrdinalIgnoreCase))
            {
                return "向量构建中";
            }

            if (SmartDocument.Status == ParsingStatus.Pending)
            {
                return "解析排队中";
            }

            if (SmartDocument.Status == ParsingStatus.Processing)
            {
                return "语义解析中";
            }

            if (SmartDocument.Status == ParsingStatus.Completed &&
                string.Equals(TryGetStringMetadata(SmartDocumentConstants.MetadataKeys.VectorIndexStatus), SmartDocumentConstants.VectorIndexStatuses.Building, StringComparison.OrdinalIgnoreCase))
            {
                return "向量构建中";
            }

            if (SmartDocument.Status == ParsingStatus.Completed &&
                string.Equals(TryGetStringMetadata(SmartDocumentConstants.MetadataKeys.VectorIndexStatus), SmartDocumentConstants.VectorIndexStatuses.Failed, StringComparison.OrdinalIgnoreCase))
            {
                return "语义可用（向量失败）";
            }

            return SemanticStatusLabel;
        }
    }

    /// <summary>
    /// 执行上下文状态标签
    /// </summary>
    public string ExecutionStatusLabel => FileAssetStatus switch
    {
        FileAssetStatusEnum.PendingUpload => "执行待上传",
        FileAssetStatusEnum.Uploaded => "执行准备中",
        FileAssetStatusEnum.Processing => "执行准备中",
        FileAssetStatusEnum.Ready => "执行可用",
        FileAssetStatusEnum.Failed => "执行失败",
        FileAssetStatusEnum.Archived => "执行归档",
        _ => IsExecutableReady ? "执行可用" : "执行不可用"
    };

    /// <summary>
    /// 显示能力标签
    /// </summary>
    public string CapabilityTag => IsAssetOnlyContext
        ? "仅执行"
        : IsExecutableReady
            ? "可执行"
            : "仅引用";

    /// <summary>
    /// 显示来源标签
    /// </summary>
    public string SourceLabel => IsTaskOutputReuse
        ? "上一轮输出"
        : SourceType switch
        {
            DocumentSourceType.Code => "代码片段",
            DocumentSourceType.Uploaded => "本地上传",
            _ => "粘贴内容"
        };

    /// <summary>
    /// 上下文摘要标签
    /// </summary>
    public string ContextSummaryLabel => IsAssetOnlyContext
        ? $"{SourceLabel} · {CapabilityTag}"
        : $"{SourceLabel} · {SemanticStatusLabel} · {CapabilityTag}";

    private string? TryGetStringMetadata(string key)
    {
        if (!SmartDocument.Metadata.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        if (value is string stringValue)
        {
            return stringValue;
        }

        if (value is JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                JsonValueKind.String => jsonElement.GetString(),
                JsonValueKind.Number => jsonElement.ToString(),
                JsonValueKind.True => bool.TrueString,
                JsonValueKind.False => bool.FalseString,
                _ => jsonElement.ToString()
            };
        }

        return value.ToString();
    }

    private Guid? TryGetGuidMetadata(string key)
    {
        var raw = TryGetStringMetadata(key);
        return Guid.TryParse(raw, out var guidValue) ? guidValue : null;
    }

    private bool TryGetBoolMetadata(string key)
    {
        if (!SmartDocument.Metadata.TryGetValue(key, out var value) || value == null)
        {
            return false;
        }

        return value switch
        {
            bool boolValue => boolValue,
            JsonElement element when element.ValueKind == JsonValueKind.True => true,
            JsonElement element when element.ValueKind == JsonValueKind.False => false,
            JsonElement element when element.ValueKind == JsonValueKind.String
                && bool.TryParse(element.GetString(), out var parsed) => parsed,
            _ => bool.TryParse(value.ToString(), out var parsed) && parsed
        };
    }

    private TEnum? TryGetEnumMetadata<TEnum>(string key) where TEnum : struct
    {
        if (!SmartDocument.Metadata.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        if (value is TEnum enumValue)
        {
            return enumValue;
        }

        if (value is JsonElement element && element.ValueKind == JsonValueKind.String)
        {
            var raw = element.GetString();
            if (Enum.TryParse<TEnum>(raw, true, out var parsed))
            {
                return parsed;
            }
        }

        if (Enum.TryParse<TEnum>(value.ToString(), true, out var fallback))
        {
            return fallback;
        }

        return null;
    }
}
