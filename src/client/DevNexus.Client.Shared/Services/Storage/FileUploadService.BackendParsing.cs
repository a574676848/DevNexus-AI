using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;

namespace DevNexus.Client.Shared.Services.Storage;

public partial class FileUploadService
{
    private async Task<SmartDocument?> ParseCodeBytesAsync(
        byte[] bytes,
        string fileName,
        string contentType,
        Guid? providerId,
        Guid? sessionId,
        FileAssetDto? uploadedAsset)
    {
        using var stream = new MemoryStream(bytes);
        var document = await ParseBackendStreamAsync(stream, fileName, contentType, providerId, sessionId, uploadedAsset, "FileUpload.ParseCode.Exception");
        return AttachFileAssetMetadata(document, uploadedAsset, fileName, contentType, bytes.Length);
    }

    private async Task<SmartDocument?> ParseDocumentBytesAsync(
        byte[] bytes,
        string fileName,
        string contentType,
        Guid? providerId,
        Guid? sessionId,
        FileAssetDto? uploadedAsset)
    {
        using var stream = new MemoryStream(bytes);
        var document = await ParseBackendStreamAsync(stream, fileName, contentType, providerId, sessionId, uploadedAsset, "FileUpload.ParseDocument.Exception");
        return AttachFileAssetMetadata(document, uploadedAsset, fileName, contentType, bytes.Length);
    }

    private async Task<SmartDocument?> ParseImageBytesAsync(
        byte[] bytes,
        string fileName,
        string contentType,
        Guid? providerId,
        Guid? sessionId,
        FileAssetDto? uploadedAsset)
    {
        using var stream = new MemoryStream(bytes);
        var document = await ParseBackendStreamAsync(stream, fileName, contentType, providerId, sessionId, uploadedAsset, "FileUpload.ParseImage.Exception");
        return AttachFileAssetMetadata(document, uploadedAsset, fileName, contentType, bytes.Length);
    }

    /// <summary>
    /// 处理代码流（后端解析 - C#/Java/JS/Python）
    /// </summary>
    public async Task<SmartDocument?> ParseCodeStreamAsync(
        Stream stream,
        string fileName,
        string contentType = "text/plain",
        Guid? providerId = null)
    {
        return await ParseBackendStreamAsync(stream, fileName, contentType, providerId, null, null, "FileUpload.ParseCode.Exception");
    }

    /// <summary>
    /// 处理文档流（后端解析 - PDF/Word）
    /// </summary>
    public async Task<SmartDocument?> ParseDocumentStreamAsync(
        Stream stream,
        string fileName,
        string contentType = DefaultMimeType,
        Guid? providerId = null)
    {
        return await ParseBackendStreamAsync(stream, fileName, contentType, providerId, null, null, "FileUpload.ParseDocument.Exception");
    }

    /// <summary>
    /// 处理图片流（后端解析）
    /// </summary>
    public async Task<SmartDocument?> ParseImageStreamAsync(
        Stream stream,
        string fileName,
        string contentType = "image/png",
        Guid? providerId = null)
    {
        return await ParseBackendStreamAsync(stream, fileName, contentType, providerId, null, null, "FileUpload.ParseImage.Exception");
    }

    private async Task<SmartDocument?> ParseBackendStreamAsync(
        Stream stream,
        string fileName,
        string contentType,
        Guid? providerId,
        Guid? sessionId,
        FileAssetDto? uploadedAsset,
        string errorSource)
    {
        try
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            var bytes = ms.ToArray();
            var effectiveMimeType = ResolveBackendMimeType(fileName, contentType);

            var response = await _apiService.ParseDocumentAsync(new ParseDocumentRequest
            {
                FileName = fileName,
                Base64Content = uploadedAsset == null ? Convert.ToBase64String(bytes) : string.Empty,
                FileUrl = uploadedAsset?.FileUrl,
                FileAssetId = uploadedAsset?.FileAssetId,
                MimeType = effectiveMimeType,
                ProviderId = providerId,
                SessionId = sessionId
            });

            return await HandleApiResponse(response, fileName, effectiveMimeType, bytes.Length, uploadedAsset);
        }
        catch (Exception ex)
        {
            await _remoteLog.LogErrorAsync(ex, errorSource, new Dictionary<string, object?>
            {
                ["FileName"] = fileName
            });
            return null;
        }
    }

    /// <summary>
    /// 统一处理 API 响应
    /// </summary>
    private async Task<SmartDocument?> HandleApiResponse(
        ParseDocumentResponse response,
        string fileName,
        string mimeType,
        long sizeBytes,
        FileAssetDto? uploadedAsset = null)
    {
        if (response.Success)
        {
            if (response.SmartDocument != null)
            {
                response.SmartDocument.Metadata ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                response.SmartDocument.Metadata[SmartDocumentConstants.MetadataKeys.SemanticPipelineStage] = SmartDocumentConstants.SemanticPipelineStages.Parsed;
                return AttachFileAssetMetadata(response.SmartDocument, uploadedAsset, fileName, mimeType, sizeBytes);
            }

            if (!string.IsNullOrEmpty(response.TraceId))
            {
                var document = new SmartDocument
                {
                    FileName = fileName,
                    MimeType = mimeType,
                    Status = ParsingStatus.Processing,
                    TraceId = response.TraceId,
                    SizeBytes = sizeBytes,
                    Metadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                };
                document.Metadata[SmartDocumentConstants.MetadataKeys.SemanticPipelineStage] = SmartDocumentConstants.SemanticPipelineStages.Pending;
                return AttachFileAssetMetadata(document, uploadedAsset, fileName, mimeType, sizeBytes);
            }
        }

        if (uploadedAsset != null)
        {
            return CreateAssetReferenceDocument(
                uploadedAsset,
                fileName,
                mimeType,
                sizeBytes,
                ParsingStatus.Failed,
                assetOnlyContext: false,
                semanticDisabledReason: null,
                traceId: response.TraceId,
                parseErrorMessage: response.ErrorMessage ?? "语义解析失败");
        }

        await _remoteLog.LogErrorAsync(
            new Exception(response.ErrorMessage ?? "Unknown backend error"),
            "FileUpload.BackendParse.Failure",
            new Dictionary<string, object?>
            {
                ["FileName"] = fileName,
                ["ErrorMessage"] = response.ErrorMessage
            });
        return null;
    }
}
