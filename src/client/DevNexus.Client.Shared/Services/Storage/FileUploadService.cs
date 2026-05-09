using DevNexus.Client.Shared.Abstractions;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace DevNexus.Client.Shared.Services.Storage;

/// <summary>
/// 文件上传解析服务
/// 封装各类文件的解析逻辑，统一返回 SmartDocument
/// </summary>
public partial class FileUploadService
{
    private readonly IApiService _apiService;
    private readonly IJSRuntime _js;
    private readonly IRemoteLogService _remoteLog;

    private const long MaxDocFileSize = 20 * 1024 * 1024;
    private const string DefaultMimeType = "application/octet-stream";

    private static readonly HashSet<string> FrontendParsableTextTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".markdown"
    };

    private static readonly HashSet<string> FrontendParsableTableTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ".csv", ".xlsx", ".xls"
    };

    private static readonly HashSet<string> BackendParsableCodeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".java", ".js", ".ts", ".jsx", ".tsx", ".py", ".go", ".rs", ".cpp", ".c", ".h"
    };

    private static readonly HashSet<string> BackendParsableDocumentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx"
    };

    private static readonly HashSet<string> SupportedImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp"
    };

    private static readonly Dictionary<string, string> BackendMimeOverrides = new(StringComparer.OrdinalIgnoreCase)
    {
        [".cs"] = "text/x-csharp",
        [".java"] = "text/x-java",
        [".js"] = "application/javascript",
        [".jsx"] = "text/javascript",
        [".ts"] = "text/typescript",
        [".tsx"] = "text/typescript",
        [".py"] = "text/x-python",
        [".go"] = "text/x-go",
        [".rs"] = "text/x-rust",
        [".cpp"] = "text/x-cpp",
        [".c"] = "text/x-c",
        [".h"] = "text/x-c",
        [".md"] = "text/markdown",
        [".markdown"] = "text/markdown",
        [".csv"] = "text/csv",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".xls"] = "application/vnd.ms-excel"
    };

    public FileUploadService(IApiService apiService, IJSRuntime js, IRemoteLogService remoteLog)
    {
        _apiService = apiService;
        _js = js;
        _remoteLog = remoteLog;
    }

    /// <summary>
    /// 根据文件扩展名自动选择解析方式
    /// </summary>
    public async Task<SmartDocument?> ParseFileAsync(IBrowserFile file, Guid? providerId = null, Guid? sessionId = null)
    {
        return await ParseStreamAsync(file.OpenReadStream(MaxDocFileSize), file.Name, file.ContentType, providerId, sessionId);
    }

    /// <summary>
    /// 解析文件流 (支持从 JS 互操作传递过来的流/数据)
    /// </summary>
    public async Task<SmartDocument?> ParseStreamAsync(
        Stream stream,
        string fileName,
        string contentType,
        Guid? providerId = null,
        Guid? sessionId = null)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        var bytes = buffer.ToArray();

        var uploadedAsset = await TryUploadFileAssetAsync(bytes, fileName, contentType, sessionId);
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var isSupportedType = IsSupportedExtension(extension);

        if (!isSupportedType)
        {
            return uploadedAsset == null
                ? null
                : CreateAssetReferenceDocument(
                    uploadedAsset,
                    fileName,
                    contentType,
                    bytes.LongLength,
                    ParsingStatus.Completed,
                    assetOnlyContext: true,
                    semanticDisabledReason: "该文件类型暂不支持语义解析",
                    traceId: null,
                    parseErrorMessage: null);
        }

        SmartDocument? quickPreview = null;
        if (FrontendParsableTextTypes.Contains(extension))
        {
            quickPreview = await ParseTextBytesAsync(bytes, fileName, contentType, uploadedAsset);
        }
        else if (FrontendParsableTableTypes.Contains(extension))
        {
            quickPreview = await ParseTableBytesAsync(bytes, fileName, contentType, uploadedAsset);
        }

        var backendParseResult = await ParseDocumentBytesAsync(bytes, fileName, contentType, providerId, sessionId, uploadedAsset);
        if (backendParseResult == null)
        {
            if (quickPreview != null)
            {
                return quickPreview;
            }

            return uploadedAsset == null
                ? null
                : CreateAssetReferenceDocument(
                    uploadedAsset,
                    fileName,
                    contentType,
                    bytes.LongLength,
                    ParsingStatus.Failed,
                    assetOnlyContext: false,
                    semanticDisabledReason: null,
                    traceId: null,
                    parseErrorMessage: "语义解析失败");
        }

        if (quickPreview == null)
        {
            return backendParseResult;
        }

        quickPreview.Status = backendParseResult.Status;
        quickPreview.TraceId = backendParseResult.TraceId;
        quickPreview.ParsedAt = backendParseResult.ParsedAt;

        foreach (var pair in backendParseResult.Metadata)
        {
            quickPreview.Metadata[pair.Key] = pair.Value;
        }

        return quickPreview;
    }

    /// <summary>
    /// 基于已上传的文件资产重新触发后端解析（失败重试入口）。
    /// </summary>
    public async Task<SmartDocument?> RetryParseFromAssetAsync(
        SmartDocument currentDocument,
        Guid? providerId = null,
        Guid? sessionId = null)
    {
        var fileAssetId = TryGetGuidMetadata(currentDocument.Metadata, SmartDocumentConstants.MetadataKeys.FileAssetId);
        var sourceUrl = TryGetStringMetadata(currentDocument.Metadata, SmartDocumentConstants.MetadataKeys.SourceUrl);
        if (fileAssetId == null && string.IsNullOrWhiteSpace(sourceUrl))
        {
            await _remoteLog.LogWarningAsync(
                "重试解析失败：缺少 FileAssetId / SourceUrl",
                "FileUpload.RetryParse.MissingAssetReference",
                new Dictionary<string, object?>
                {
                    ["FileName"] = currentDocument.FileName
                });
            return null;
        }

        var effectiveMimeType = ResolveBackendMimeType(currentDocument.FileName, currentDocument.MimeType);
        var response = await _apiService.ParseDocumentAsync(new ParseDocumentRequest
        {
            FileName = currentDocument.FileName,
            Base64Content = string.Empty,
            FileUrl = sourceUrl,
            FileAssetId = fileAssetId,
            MimeType = effectiveMimeType,
            ProviderId = providerId,
            SessionId = sessionId
        });

        if (!response.Success)
        {
            await _remoteLog.LogWarningAsync(
                "重试解析请求被拒绝",
                "FileUpload.RetryParse.BackendRejected",
                new Dictionary<string, object?>
                {
                    ["FileName"] = currentDocument.FileName,
                    ["ErrorMessage"] = response.ErrorMessage
                });
            return null;
        }

        var nextDocument = response.SmartDocument ?? new SmartDocument
        {
            FileName = currentDocument.FileName,
            MimeType = effectiveMimeType,
            Status = ParsingStatus.Processing,
            TraceId = response.TraceId,
            SizeBytes = currentDocument.SizeBytes,
            Metadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        };

        nextDocument.FileName = string.IsNullOrWhiteSpace(nextDocument.FileName) ? currentDocument.FileName : nextDocument.FileName;
        nextDocument.MimeType = string.IsNullOrWhiteSpace(nextDocument.MimeType) ? effectiveMimeType : nextDocument.MimeType;
        nextDocument.SizeBytes = nextDocument.SizeBytes <= 0 ? currentDocument.SizeBytes : nextDocument.SizeBytes;
        nextDocument.Metadata ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in currentDocument.Metadata)
        {
            nextDocument.Metadata[pair.Key] = pair.Value;
        }

        if (!string.IsNullOrWhiteSpace(response.TraceId))
        {
            nextDocument.TraceId = response.TraceId;
        }

        nextDocument.Status = response.SmartDocument?.Status ?? ParsingStatus.Processing;
        nextDocument.Metadata[SmartDocumentConstants.MetadataKeys.RetryRequestedAt] = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(sourceUrl))
        {
            nextDocument.Metadata[SmartDocumentConstants.MetadataKeys.SourceUrl] = sourceUrl;
        }

        if (fileAssetId.HasValue)
        {
            nextDocument.Metadata[SmartDocumentConstants.MetadataKeys.FileAssetId] = fileAssetId.Value;
        }

        return nextDocument;
    }

    public SmartDocument CreateFromPastedText(string text, string fileName = "pasted-content.txt")
    {
        return new SmartDocument
        {
            FileId = Guid.NewGuid().ToString(),
            FileName = fileName,
            MimeType = "text/plain",
            SizeBytes = global::System.Text.Encoding.UTF8.GetByteCount(text),
            CreatedAt = DateTime.UtcNow,
            ParsedAt = DateTime.UtcNow,
            Content = new TextDocumentContent
            {
                Text = text,
                Format = "plain",
                PageCount = 1
            },
            ParseInfo = new ParseMetadata
            {
                Strategy = "frontend-paste",
                ProcessingTimeMs = 0
            }
        };
    }

    public bool IsFileSupported(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return IsSupportedExtension(extension);
    }

    public string GetFileTypeDescription(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        if (FrontendParsableTextTypes.Contains(extension)) return "文本";
        if (FrontendParsableTableTypes.Contains(extension)) return "表格";
        if (BackendParsableCodeTypes.Contains(extension)) return "代码";
        if (BackendParsableDocumentTypes.Contains(extension)) return "文档";
        if (SupportedImageTypes.Contains(extension)) return "图片";

        return "未知";
    }

    private static bool IsSupportedExtension(string extension)
    {
        return FrontendParsableTextTypes.Contains(extension)
               || FrontendParsableTableTypes.Contains(extension)
               || BackendParsableCodeTypes.Contains(extension)
               || BackendParsableDocumentTypes.Contains(extension)
               || SupportedImageTypes.Contains(extension);
    }
}
