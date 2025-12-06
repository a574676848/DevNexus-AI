using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.RegularExpressions;

namespace DevNexus.Core.Services.LLM;

/// <summary>
/// Block 解析器
/// 用于解析 LLM 流式响应并转换为不同类型的 Block
/// </summary>
public class BlockParser
{
    private readonly ILogger<BlockParser> _logger;
    private readonly StringBuilder _buffer = new();
    private bool _inThoughtChain = false;
    private bool _inCodeBlock = false;
    private string _codeLanguage = string.Empty;
    private Guid _currentArtifactId = Guid.Empty;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    public BlockParser(ILogger<BlockParser> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 解析流式文本块并生成 Block
    /// </summary>
    /// <param name="content">内容增量</param>
    /// <param name="messageId">消息ID</param>
    /// <returns>解析出的 Block 列表</returns>
    public List<BlockDto> Parse(string content, Guid messageId)
    {
        var blocks = new List<BlockDto>();
        _buffer.Append(content);

        var bufferText = _buffer.ToString();

        // 检测思考链标签 <think>...</think>
        if (bufferText.Contains("<think>") && !_inThoughtChain)
        {
            _inThoughtChain = true;
            var beforeThink = bufferText[..bufferText.IndexOf("<think>")];
            
            if (!string.IsNullOrEmpty(beforeThink))
            {
                blocks.Add(CreateTextDeltaBlock(beforeThink, messageId));
            }

            _buffer.Clear();
            _buffer.Append(bufferText[(bufferText.IndexOf("<think>") + 7)..]);
            return blocks;
        }

        if (bufferText.Contains("</think>") && _inThoughtChain)
        {
            _inThoughtChain = false;
            var thoughtContent = bufferText[..bufferText.IndexOf("</think>")];
            
            blocks.Add(CreateThoughtChainBlock(thoughtContent, messageId));

            _buffer.Clear();
            _buffer.Append(bufferText[(bufferText.IndexOf("</think>") + 8)..]);
            return blocks;
        }

        // 检测代码块标记 ```language
        var codeBlockMatch = Regex.Match(bufferText, @"```(\w+)");
        if (codeBlockMatch.Success && !_inCodeBlock)
        {
            _inCodeBlock = true;
            _codeLanguage = codeBlockMatch.Groups[1].Value;
            _currentArtifactId = Guid.NewGuid();

            var beforeCode = bufferText[..codeBlockMatch.Index];
            if (!string.IsNullOrEmpty(beforeCode))
            {
                blocks.Add(CreateTextDeltaBlock(beforeCode, messageId));
            }

            // 发送 ArtifactStart
            blocks.Add(CreateArtifactStartBlock(_codeLanguage, messageId));

            _buffer.Clear();
            _buffer.Append(bufferText[(codeBlockMatch.Index + codeBlockMatch.Length)..].TrimStart('\n'));
            return blocks;
        }

        if (bufferText.Contains("```") && _inCodeBlock)
        {
            _inCodeBlock = false;
            var codeContent = bufferText[..bufferText.IndexOf("```")];
            
            // 发送最后的代码增量
            if (!string.IsNullOrEmpty(codeContent))
            {
                blocks.Add(CreateArtifactDeltaBlock(codeContent, messageId));
            }

            // 发送 ArtifactEnd
            blocks.Add(CreateArtifactEndBlock(messageId));

            _buffer.Clear();
            _buffer.Append(bufferText[(bufferText.IndexOf("```") + 3)..]);
            _currentArtifactId = Guid.Empty;
            return blocks;
        }

        // 如果在思考链或代码块中，暂存不发送
        if (_inThoughtChain || _inCodeBlock)
        {
            // 如果在代码块中，可以发送增量
            if (_inCodeBlock && _buffer.Length > 50)
            {
                var deltaContent = _buffer.ToString();
                blocks.Add(CreateArtifactDeltaBlock(deltaContent, messageId));
                _buffer.Clear();
            }
            return blocks;
        }

        // 普通文本增量
        if (_buffer.Length > 0)
        {
            var textContent = _buffer.ToString();
            blocks.Add(CreateTextDeltaBlock(textContent, messageId));
            _buffer.Clear();
        }

        return blocks;
    }

    /// <summary>
    /// 完成解析，返回剩余的内容
    /// </summary>
    /// <param name="messageId">消息ID</param>
    /// <returns>最后的 Block</returns>
    public List<BlockDto> Finish(Guid messageId)
    {
        var blocks = new List<BlockDto>();

        if (_buffer.Length > 0)
        {
            if (_inCodeBlock)
            {
                // 如果还在代码块中，强制结束
                blocks.Add(CreateArtifactDeltaBlock(_buffer.ToString(), messageId));
                blocks.Add(CreateArtifactEndBlock(messageId));
            }
            else if (_inThoughtChain)
            {
                // 如果还在思考链中，作为思考链结束
                blocks.Add(CreateThoughtChainBlock(_buffer.ToString(), messageId));
            }
            else
            {
                // 普通文本
                blocks.Add(CreateTextDeltaBlock(_buffer.ToString(), messageId));
            }

            _buffer.Clear();
        }

        // 添加最后一个标记块
        blocks.Add(new BlockDto
        {
            BlockType = BlockType.TextDelta,
            Content = string.Empty,
            MessageId = messageId,
            IsLast = true
        });

        return blocks;
    }

    /// <summary>
    /// 创建文本增量 Block
    /// </summary>
    private BlockDto CreateTextDeltaBlock(string content, Guid messageId)
    {
        return new BlockDto
        {
            BlockType = BlockType.TextDelta,
            Content = content,
            MessageId = messageId,
            IsLast = false
        };
    }

    /// <summary>
    /// 创建思考链 Block
    /// </summary>
    private BlockDto CreateThoughtChainBlock(string content, Guid messageId)
    {
        _logger.LogInformation("[AI.Block] ThoughtChain detected | Length={Length}", content.Length);

        return new BlockDto
        {
            BlockType = BlockType.ThoughtChain,
            Content = content,
            MessageId = messageId,
            IsLast = false
        };
    }

    /// <summary>
    /// 创建 Artifact 开始 Block
    /// </summary>
    private BlockDto CreateArtifactStartBlock(string language, Guid messageId)
    {
        _logger.LogInformation("[AI.Block] ArtifactStart detected | Language={Language}", language);

        return new BlockDto
        {
            BlockType = BlockType.ArtifactStart,
            Content = string.Empty,
            MessageId = messageId,
            IsLast = false,
            Metadata = new Dictionary<string, object>
            {
                { "artifactId", _currentArtifactId },
                { "language", language },
                { "title", $"Code {language}" }
            }
        };
    }

    /// <summary>
    /// 创建 Artifact 增量 Block
    /// </summary>
    private BlockDto CreateArtifactDeltaBlock(string content, Guid messageId)
    {
        return new BlockDto
        {
            BlockType = BlockType.ArtifactDelta,
            Content = content,
            MessageId = messageId,
            IsLast = false,
            Metadata = new Dictionary<string, object>
            {
                { "artifactId", _currentArtifactId }
            }
        };
    }

    /// <summary>
    /// 创建 Artifact 结束 Block
    /// </summary>
    private BlockDto CreateArtifactEndBlock(Guid messageId)
    {
        _logger.LogInformation("[AI.Block] ArtifactEnd detected | ArtifactId={ArtifactId}", _currentArtifactId);

        return new BlockDto
        {
            BlockType = BlockType.ArtifactEnd,
            Content = string.Empty,
            MessageId = messageId,
            IsLast = false,
            Metadata = new Dictionary<string, object>
            {
                { "artifactId", _currentArtifactId }
            }
        };
    }
}
