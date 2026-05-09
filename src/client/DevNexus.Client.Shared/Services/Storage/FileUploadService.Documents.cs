using System.Text.Json;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;

namespace DevNexus.Client.Shared.Services.Storage;

public partial class FileUploadService
{
    private async Task<FileAssetDto?> TryUploadFileAssetAsync(byte[] bytes, string fileName, string contentType, Guid? sessionId)
    {
        try
        {
            var createResponse = await _apiService.CreateUploadSessionAsync(new CreateUploadSessionRequest
            {
                FileName = fileName,
                ContentType = string.IsNullOrWhiteSpace(contentType) ? DefaultMimeType : contentType,
                SessionId = sessionId,
                ExpectedSizeBytes = bytes.LongLength,
                SourceType = "chat-upload"
            });

            using var uploadStream = new MemoryStream(bytes);
            await _apiService.UploadUploadSessionContentAsync(
                createResponse.UploadSession,
                uploadStream,
                string.IsNullOrWhiteSpace(contentType) ? DefaultMimeType : contentType);

            var finalizeResponse = await _apiService.FinalizeUploadAsync(new FinalizeUploadRequest
            {
                UploadSessionId = createResponse.UploadSession.UploadSessionId,
                SizeBytes = bytes.LongLength
            });

            return finalizeResponse.FileAsset;
        }
        catch (Exception ex)
        {
            await _remoteLog.LogWarningAsync(
                "文件资产上传失败，回退到旧解析路径",
                "FileUpload.TryUploadFileAssetAsync",
                new Dictionary<string, object?>
                {
                    ["FileName"] = fileName,
                    ["ContentType"] = contentType,
                    ["ErrorMessage"] = ex.Message
                });
            return null;
        }
    }

    private static SmartDocument? AttachFileAssetMetadata(
        SmartDocument? document,
        FileAssetDto? uploadedAsset,
        string fileName,
        string mimeType,
        long sizeBytes)
    {
        if (document == null)
        {
            return null;
        }

        document.Metadata ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        document.FileName = string.IsNullOrWhiteSpace(document.FileName) ? fileName : document.FileName;
        document.MimeType = string.IsNullOrWhiteSpace(document.MimeType) ? mimeType : document.MimeType;
        document.SizeBytes = document.SizeBytes <= 0 ? sizeBytes : document.SizeBytes;

        if (uploadedAsset != null)
        {
            document.FileId = uploadedAsset.FileAssetId.ToString();
            document.Metadata[SmartDocumentConstants.MetadataKeys.FileAssetId] = uploadedAsset.FileAssetId;
            document.Metadata[SmartDocumentConstants.MetadataKeys.SourceUrl] = uploadedAsset.FileUrl;
            document.Metadata[SmartDocumentConstants.MetadataKeys.StorageProvider] = uploadedAsset.StorageProvider;
            document.Metadata[SmartDocumentConstants.MetadataKeys.ObjectKey] = uploadedAsset.ObjectKey;
            document.Metadata[SmartDocumentConstants.MetadataKeys.CurrentVersionId] = uploadedAsset.CurrentVersionId;
            document.Metadata[SmartDocumentConstants.MetadataKeys.FileAssetStatus] = uploadedAsset.Status.ToString();
        }

        return document;
    }

    private static SmartDocument CreateAssetReferenceDocument(
        FileAssetDto uploadedAsset,
        string fileName,
        string mimeType,
        long sizeBytes,
        ParsingStatus semanticStatus,
        bool assetOnlyContext,
        string? semanticDisabledReason,
        string? traceId,
        string? parseErrorMessage)
    {
        var document = new SmartDocument
        {
            FileName = fileName,
            MimeType = mimeType,
            SizeBytes = sizeBytes,
            Status = semanticStatus,
            TraceId = traceId,
            Content = new TextDocumentContent
            {
                Text = string.Empty,
                Format = "asset-reference",
                PageCount = 0
            },
            Metadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        };

        if (assetOnlyContext)
        {
            document.Metadata[SmartDocumentConstants.MetadataKeys.AssetOnlyContext] = true;
            document.Metadata[SmartDocumentConstants.MetadataKeys.SemanticPipelineStage] = SmartDocumentConstants.SemanticPipelineStages.NotRequested;
        }

        if (!string.IsNullOrWhiteSpace(semanticDisabledReason))
        {
            document.Metadata[SmartDocumentConstants.MetadataKeys.SemanticDisabledReason] = semanticDisabledReason;
        }

        if (!string.IsNullOrWhiteSpace(parseErrorMessage))
        {
            document.Metadata[SmartDocumentConstants.MetadataKeys.ParseErrorMessage] = parseErrorMessage;
        }

        return AttachFileAssetMetadata(document, uploadedAsset, fileName, mimeType, sizeBytes) ?? document;
    }

    private static string ResolveBackendMimeType(string fileName, string contentType)
    {
        var extension = Path.GetExtension(fileName);
        if (!string.IsNullOrWhiteSpace(extension) &&
            BackendMimeOverrides.TryGetValue(extension, out var overridden))
        {
            return overridden;
        }

        return string.IsNullOrWhiteSpace(contentType) ? DefaultMimeType : contentType;
    }

    private static string? TryGetStringMetadata(Dictionary<string, object> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        return value switch
        {
            string s => s,
            JsonElement json when json.ValueKind == JsonValueKind.String => json.GetString(),
            JsonElement json => json.ToString(),
            _ => value.ToString()
        };
    }

    private static Guid? TryGetGuidMetadata(Dictionary<string, object> metadata, string key)
    {
        var raw = TryGetStringMetadata(metadata, key);
        return Guid.TryParse(raw, out var result) ? result : null;
    }
}
