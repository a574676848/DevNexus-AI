using DevNexus.Core.Extensions;
using DevNexus.Shared.DTOs;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Extensions;

/// <summary>
/// SmartDocument 上下文提取测试。
/// </summary>
public sealed class SmartDocumentExtensionsTests
{
    private const string FileName = "OpenViking 企业 AI 知识中台架构全景图.png";
    private const string VisionChunkText = "OpenViking 企业 AI 知识中台架构全景图展示了接入矩阵、极速寻址中枢和 AI 算力调度。";

    /// <summary>
    /// 图片解析结果可能只有 chunks，没有 content，也必须进入模型上下文。
    /// </summary>
    [Fact]
    public void ExtractTextContent_ShouldUseChunkText_WhenContentIsNull()
    {
        var smartDocument = new SmartDocument
        {
            FileName = FileName,
            MimeType = "image/png",
            Content = null,
            Chunks =
            [
                new SmartChunk
                {
                    Type = ChunkType.Image,
                    Content = VisionChunkText
                }
            ]
        };

        var result = smartDocument.ExtractTextContent();

        result.Should().Be(VisionChunkText);
    }

    /// <summary>
    /// 图片 content 有描述时优先使用描述，避免 chunk 兜底覆盖主解析结果。
    /// </summary>
    [Fact]
    public void ExtractTextContent_ShouldPreferImageDescription_WhenPresent()
    {
        var smartDocument = new SmartDocument
        {
            FileName = FileName,
            MimeType = "image/png",
            Content = new ImageDocumentContent
            {
                Description = "图片描述",
                Format = "png"
            },
            Chunks =
            [
                new SmartChunk
                {
                    Type = ChunkType.Image,
                    Content = VisionChunkText
                }
            ]
        };

        var result = smartDocument.ExtractTextContent();

        result.Should().Be("图片描述");
    }

    /// <summary>
    /// Artifact 类型为 unknown 时，只要内容是 SmartDocument JSON，也应进入 SmartDocument 解析分支。
    /// </summary>
    [Fact]
    public void IsSmartDocumentArtifact_ShouldUseContentPresence_WhenArtifactTypeIsUnknown()
    {
        SmartDocumentExtensions.IsSmartDocumentArtifact("unknown", "{}").Should().BeTrue();
    }
}
