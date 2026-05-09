using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 流式 Block 解析器
/// 从 LLM 流式输出中实时识别和解析 Block 标记
/// </summary>
/// <remarks>
/// 支持的 Block 格式:
/// 
/// :::chart{type="line" title="标题"}
/// JSON 数据
/// :::
/// 
/// :::code{id="user-service" version="1" action="create" lang="csharp" title="UserService.cs" highlight="5-8,12"}
/// 代码内容
/// :::
/// 
/// :::thinking{collapsed="true"}
/// 思考过程...
/// :::
/// 
/// :::warning{level="info" title="提示"}
/// 警告内容
/// :::
/// 
/// :::ref{id="user-service"}
/// 引用说明
/// :::
/// </remarks>
public partial class StreamBlockParser
{
    private readonly ILogger? _logger;

    public StreamBlockParser(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// 解析状态
    /// </summary>
    private enum ParserState
    {
        /// <summary>普通文本模式</summary>
        Text,
        /// <summary>Block 内容模式</summary>
        BlockContent,
        /// <summary>WebSearch 标签内容模式</summary>
        SearchWeb,
        /// <summary>AdvancedSearch 标签内容模式</summary>
        AdvancedSearch,
        /// <summary>Webpage 标签内容模式</summary>
        Webpage
    }

    /// <summary>
    /// Block 解析上下文
    /// </summary>
    private class BlockContext
    {
        public string BlockName { get; set; } = string.Empty;
        public Dictionary<string, string> Attributes { get; set; } = new();
        public StringBuilder Content { get; set; } = new();
    }

    // 状态
    private ParserState _state = ParserState.Text;
    private BlockContext? _currentBlock;
    private readonly StringBuilder _textBuffer = new();
    private readonly StringBuilder _lineBuffer = new();
    private readonly StringBuilder _searchBuffer = new();
    private readonly StringBuilder _advancedSearchBuffer = new();
    private readonly StringBuilder _webpageBuffer = new();
    
    // ★ 思维链内容累积器（用于持久化）
    private readonly StringBuilder _thinkingBuffer = new();
    
    // ★ 周期性持久化配置
    private int _thinkingBlockCount = 0;
    private const int PERSISTENCE_THRESHOLD = 3;  // 每 3 个 Thinking Block 触发一次持久化
    
    // ★ 持久化回调（由 ChatService 注入）
    private Func<string, Guid, Guid, Task>? _persistenceCallback;

    // 正则表达式
    private static readonly Regex BlockStartPattern = new(
        @"^:::(\w+(?:-\w+)*)(?:\{(.+?)\})?\s*$",
        RegexOptions.Compiled);

    private static readonly Regex AttributePattern = new(
        @"(\w+)=""([^""]*)""|(\w+)='([^']*)'",
        RegexOptions.Compiled);    
    /// <summary>
    /// 设置持久化回调（用于周期性保存思维链）
    /// </summary>
    public void SetPersistenceCallback(Func<string, Guid, Guid, Task>? callback)
    {
        _persistenceCallback = callback;
    }
    /// <summary>
    /// 解析流式 chunk，返回解析出的 Block 列表
    /// </summary>
    /// <param name="chunk">流式文本片段</param>
    /// <returns>解析出的 Block 列表</returns>
    public IEnumerable<BlockDto> ParseChunk(string? chunk)
    {
        if (string.IsNullOrEmpty(chunk))
            yield break;

        foreach (var ch in chunk)
        {
            if (ch == '\n')
            {
                // 处理完整行
                var line = _lineBuffer.ToString();
                _lineBuffer.Clear();

                foreach (var block in ProcessLine(line))
                {
                    yield return block;
                }
            }
            else
            {
                _lineBuffer.Append(ch);
            }
        }

        // ★ 流式即时下发：每个 chunk 处理完毕后，立即推送已缓冲的文本
        // 业界主流做法：服务端尽快推送 token，客户端节流渲染
        if (_state == ParserState.Text)
        {
            // 将行缓冲区的不完整行也纳入输出
            // 使用 CouldBeBlockMarkerStart 排除可能跨 chunk 拆分的标记前缀（如 ":" "::" "<s"）
            if (_lineBuffer.Length > 0)
            {
                var pending = _lineBuffer.ToString();
                if (!CouldBeBlockMarkerStart(pending))
                {
                    _textBuffer.Append(pending);
                    _lineBuffer.Clear();
                }
            }

            // 下发已缓冲的所有文本
            if (_textBuffer.Length > 0)
            {
                yield return new BlockDto
                {
                    BlockId = Guid.NewGuid(),
                    BlockType = BlockType.TextDelta,
                    Content = _textBuffer.ToString(),
                    IsLast = false
                };
                _textBuffer.Clear();
            }
        }

        // ★ BlockContent 模式流式反馈：让用户在代码生成期间看到实时进度
        // 策略：code/html 类型 Block 积累内容时，以 ArtifactDelta 形式推送
        if (_state == ParserState.BlockContent && _currentBlock != null)
        {
            var blockName = _currentBlock.BlockName.ToLowerInvariant();
            if ((blockName == "code" || blockName == "html") && _currentBlock.Content.Length > 0)
            {
                var lang = blockName == "code"
                    ? (_currentBlock.Attributes.TryGetValue("lang", out var l) ? l : ArtifactBlockMetadataConstants.LanguagePlaintext)
                    : ArtifactBlockMetadataConstants.TypeHtml;
                var title = _currentBlock.Attributes.TryGetValue("title", out var t) ? t : ArtifactBlockMetadataConstants.DefaultCodeTitle;

                yield return new BlockDto
                {
                    BlockId = Guid.NewGuid(),
                    BlockType = BlockType.ArtifactDelta,
                    Content = _currentBlock.Content.ToString(),
                    IsLast = false,
                    Metadata = new Dictionary<string, object>
                    {
                        { ArtifactBlockMetadataConstants.Type, ArtifactBlockMetadataConstants.NormalizeType(blockName) },
                        { ArtifactBlockMetadataConstants.Language, lang },
                        { ArtifactBlockMetadataConstants.Title, title },
                        { ArtifactBlockMetadataConstants.IsComplete, false }
                    }
                };
            }
        }
    }

    /// <summary>
    /// 处理一行文本
    /// </summary>
    private IEnumerable<BlockDto> ProcessLine(string line)
    {
        switch (_state)
        {
            case ParserState.Text:
                // 优先处理 <search_web> 标记
                if (line.Contains(SearchWebStartTag, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var block in ProcessSearchWebTagInText(line))
                    {
                        yield return block;
                    }
                    // 只要包含标签，无论是否进入 SearchWeb 模式，当前行都已被处理（避免重复输出）
                    break;
                }

                // 处理 <advanced_search> 标记
                if (line.Contains(AdvancedSearchStartTag, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var block in ProcessAdvancedSearchTagInText(line))
                    {
                        yield return block;
                    }
                    break;
                }

                // 处理 <webpage> 标记
                if (line.Contains(WebpageStartTag, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var block in ProcessWebpageTagInText(line))
                    {
                        yield return block;
                    }
                    break;
                }

                // 检查是否是 Block 开始
                var match = BlockStartPattern.Match(line);
                if (match.Success)
                {
                    // 先 yield 累积的文本
                    if (_textBuffer.Length > 0)
                    {
                        yield return new BlockDto
                        {
                            BlockId = Guid.NewGuid(),
                            BlockType = BlockType.TextDelta,
                            Content = _textBuffer.ToString(),
                            IsLast = false
                        };
                        _textBuffer.Clear();
                    }

                    // 开始新的 Block
                    _currentBlock = new BlockContext
                    {
                        BlockName = match.Groups[1].Value,
                        Attributes = ParseAttributes(match.Groups[2].Value)
                    };
                    _state = ParserState.BlockContent;
                }
                else
                {
                    // 累积普通文本
                    _textBuffer.AppendLine(line);
                }
                break;

            case ParserState.BlockContent:
                if (line.Trim() == ":::")
                {
                    // Block 结束
                    if (_currentBlock != null)
                    {
                        yield return BuildBlockDto(_currentBlock);
                        _currentBlock = null;
                    }
                    _state = ParserState.Text;
                }
                else
                {
                    // 累积 Block 内容
                    _currentBlock?.Content.AppendLine(line);
                }
                break;

            case ParserState.SearchWeb:
                foreach (var block in ProcessSearchWebTagContent(line))
                {
                    yield return block;
                }
                break;

            case ParserState.AdvancedSearch:
                foreach (var block in ProcessAdvancedSearchTagContent(line))
                {
                    yield return block;
                }
                break;

            case ParserState.Webpage:
                foreach (var block in ProcessWebpageTagContent(line))
                {
                    yield return block;
                }
                break;
        }
    }

    private const string SearchWebStartTag = "<search_web>";
}
