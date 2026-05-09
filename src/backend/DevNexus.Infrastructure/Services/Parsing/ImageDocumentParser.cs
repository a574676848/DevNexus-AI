using DevNexus.Domain.Abstractions;
using DevNexus.Shared.DTOs;
using Microsoft.Extensions.Logging;

namespace DevNexus.Infrastructure.Services.Parsing;

/// <summary>
/// 图片文档解析适配器
/// 将图片解析请求路由到 VisionParsingService
/// </summary>
public class ImageDocumentParser : ISmartDocumentParser
{
    private readonly VisionParsingService _visionService;
    private readonly ILogger<ImageDocumentParser> _logger;

    private static class ChunkMetadataKeys
    {
        public const string Source = "Source";
        public const string SourceVisionParsingService = "VisionParsingService";
    }

    public ImageDocumentParser(
        VisionParsingService visionService,
        ILogger<ImageDocumentParser> logger)
    {
        _visionService = visionService;
        _logger = logger;
    }

    public bool CanParse(string mimeType)
    {
        return mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<SmartDocument> ParseAsync(
        Stream fileStream, 
        string fileName, 
        string? mimeType = null, 
        ParsingOptions? options = null, 
        ParsingContext? context = null)
    {
        _logger.LogInformation("Parsing image via VisionService: {FileName}", fileName);

        // VisionService 需要 byte[]
        using var memoryStream = new MemoryStream();
        await fileStream.CopyToAsync(memoryStream, context?.CancellationToken ?? CancellationToken.None);
        var imageBytes = memoryStream.ToArray();

        // 确定 MIME 类型
        var effectiveMimeType = mimeType ?? SmartDocumentParserFactory.GetMimeType(fileName);

        // 调用 VisionService
        var result = await _visionService.ParseImageAsync(
            imageBytes, 
            effectiveMimeType, 
            options?.ProviderId, // 如果指定了 Provider
            options,
            context?.CancellationToken ?? CancellationToken.None
        );

        var doc = new SmartDocument
        {
            FileName = fileName,
            MimeType = effectiveMimeType,
            Status = result.Success ? ParsingStatus.Completed : ParsingStatus.Failed,
            ParseInfo = new ParseMetadata
            {
                ModelUsed = result.ModelUsed,
                Strategy = result.Strategy ?? "vision",
                ProcessingTimeMs = result.ProcessingTimeMs,
                Warnings = result.Warnings,
                QualityScore = result.Success ? 1.0 : 0.0,
                ParsedBy = "server"
            }
        };

        if (result.Success && !string.IsNullOrEmpty(result.Description))
        {
            // 将描述作为 Chunk 添加，以便 KnowledgeBaseService 能够索引
            doc.Chunks.Add(new SmartChunk
            {
                Content = result.Description,
                Type = ChunkType.Image, // 或者是 Text? ChunkType.Image 可能不被索引?
                // KnowledgeBaseService.UpsertDocumentAsync: BuildTextContent: AppendLine($"Type: {chunk.Type}")
                // 它会将 content 传给 KM ImportTextAsync。
                // 所以这是可以的。
                Metadata = new Dictionary<string, object>
                {
                    [ChunkMetadataKeys.Source] = ChunkMetadataKeys.SourceVisionParsingService
                }
            });
        }
        else
        {
            _logger.LogWarning("Image parsing failed or returned empty description: {ErrorMessage}", result.ErrorMessage);
        }

        return doc;
    }
}
