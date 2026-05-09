namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 推理流增量累加器。
/// 兼容上游同时返回“完整快照”和“增量片段”的两种模式，统一只输出未发送的新内容。
/// </summary>
internal sealed class ReasoningStreamAccumulator
{
    private string _emittedContent = string.Empty;

    /// <summary>
    /// 根据本次提取到的推理内容，计算尚未向客户端发送的增量部分。
    /// </summary>
    /// <param name="incomingContent">本次从上游提取到的推理内容</param>
    /// <returns>未发送过的新增内容；如果本次没有新增内容则返回空字符串</returns>
    public string GetDelta(string? incomingContent)
    {
        if (string.IsNullOrEmpty(incomingContent))
        {
            return string.Empty;
        }

        if (_emittedContent.Length == 0)
        {
            _emittedContent = incomingContent;
            return incomingContent;
        }

        if (string.Equals(incomingContent, _emittedContent, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        if (incomingContent.StartsWith(_emittedContent, StringComparison.Ordinal))
        {
            var delta = incomingContent[_emittedContent.Length..];
            _emittedContent = incomingContent;
            return delta;
        }

        if (_emittedContent.Contains(incomingContent, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var overlapLength = GetSuffixPrefixOverlapLength(_emittedContent, incomingContent);
        if (overlapLength > 0)
        {
            var delta = incomingContent[overlapLength..];
            if (delta.Length == 0)
            {
                return string.Empty;
            }

            _emittedContent += delta;
            return delta;
        }

        _emittedContent += incomingContent;
        return incomingContent;
    }

    /// <summary>
    /// 重置已发送内容缓存。
    /// </summary>
    public void Reset()
    {
        _emittedContent = string.Empty;
    }

    private static int GetSuffixPrefixOverlapLength(string existingContent, string incomingContent)
    {
        var maxLength = Math.Min(existingContent.Length, incomingContent.Length);
        for (var length = maxLength; length > 0; length--)
        {
            if (existingContent.AsSpan(existingContent.Length - length, length)
                .SequenceEqual(incomingContent.AsSpan(0, length)))
            {
                return length;
            }
        }

        return 0;
    }
}