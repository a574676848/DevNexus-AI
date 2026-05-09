using System.Text;
// using DevNexus.Domain.Abstractions via GlobalUsings
using DevNexus.Shared.DTOs;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace DevNexus.Infrastructure.Services.Parsing;

/// <summary>
/// 本地文档智能服务 (基于 PdfPig)
/// </summary>
public class LocalDocumentIntelligenceService : IDocumentIntelligenceService
{
    private readonly ILogger<LocalDocumentIntelligenceService> _logger;

    private static class ChunkMetadataKeys
    {
        public const string Page = "Page";
        public const string Height = "Height";
        public const string Width = "Width";
    }

    public LocalDocumentIntelligenceService(ILogger<LocalDocumentIntelligenceService> logger)
    {
        _logger = logger;
    }

    public async Task<DocumentAnalysisResult> AnalyzeDocumentAsync(
        Stream fileStream, 
        string mimeType, 
        ParsingContext? context = null)
    {
        var result = new DocumentAnalysisResult();
        
        // 目前仅支持 PDF，未来可扩展 Image
        if (mimeType.Contains("pdf", StringComparison.OrdinalIgnoreCase))
        {
             // Copy stream to memory to read
             using var memoryStream = new MemoryStream();
             await fileStream.CopyToAsync(memoryStream);
             var bytes = memoryStream.ToArray();
             
             try 
             {
                 using var document = PdfDocument.Open(bytes);
                 var sb = new StringBuilder();
                 var chunks = new List<SmartChunk>();
                 
                 foreach (var page in document.GetPages())
                 {
                     var text = ContentOrderTextExtractor.GetText(page);
                     sb.AppendLine(text);
                     
                     // Simple Chunking per Page
                     if (!string.IsNullOrWhiteSpace(text))
                     {
                         chunks.Add(new SmartChunk
                         {
                             Type = ChunkType.Text,
                             Content = text,
                             Metadata = new Dictionary<string, object>
                             {
                                 { ChunkMetadataKeys.Page, page.Number },
                                 { ChunkMetadataKeys.Height, page.Height },
                                 { ChunkMetadataKeys.Width, page.Width }
                             }
                         });
                     }
                 }
                 
                 result.FullText = sb.ToString();
                 result.Chunks = chunks;
                 result.Metadata.Add("PageCount", document.NumberOfPages);
                 result.QualityScore = string.IsNullOrWhiteSpace(result.FullText) ? 0.1 : 0.9;
             }
             catch (Exception ex)
             {
                 _logger.LogError(ex, "PDF Analysis failed");
                 result.QualityScore = 0.0;
             }
        }
        else
        {
            _logger.LogWarning("LocalDocumentIntelligenceService only supports PDF for now. Skipped {MimeType}", mimeType);
            // Default empty result
        }

        return result;
    }
}
