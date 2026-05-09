using Microsoft.ML.Tokenizers;

namespace DevNexus.Infrastructure.Services.Memory;

/// <summary>
/// Tiktoken 服务
/// 使用 Microsoft.ML.Tokenizers 实现精确的 BPE 分词
/// 支持 GPT-4 / text-embedding-ada-002 使用的 cl100k_base 编码
/// </summary>
public static class TiktokenService
{
    /// <summary>
    /// 延迟初始化的 Tokenizer 实例（线程安全）
    /// 使用 cl100k_base 编码，兼容 GPT-4 和 text-embedding-ada-002
    /// </summary>
    private static readonly Lazy<TiktokenTokenizer> _tokenizer = new(() =>
        TiktokenTokenizer.CreateForEncoding("cl100k_base"));

    /// <summary>
    /// 获取 Tokenizer 实例
    /// </summary>
    public static TiktokenTokenizer Tokenizer => _tokenizer.Value;

    /// <summary>
    /// 计算文本的 Token 数量
    /// </summary>
    public static int CountTokens(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        return _tokenizer.Value.CountTokens(text);
    }

    /// <summary>
    /// 将文本编码为 Token ID 列表
    /// </summary>
    public static IReadOnlyList<int> Encode(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return Array.Empty<int>();

        return _tokenizer.Value.EncodeToIds(text);
    }

    /// <summary>
    /// 将 Token ID 列表解码为文本
    /// </summary>
    public static string Decode(IEnumerable<int> tokenIds)
    {
        return _tokenizer.Value.Decode(tokenIds);
    }
}
