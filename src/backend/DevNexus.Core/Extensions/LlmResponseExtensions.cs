using System;
using System.Text;
using System.Text.RegularExpressions;

namespace DevNexus.Core.Extensions;

/// <summary>
/// LLM 响应处理扩展类
/// </summary>
public static class LlmResponseExtensions
{
    private static readonly Regex JsonFenceRegex = new(@"```(?:json)?\s*([\s\S]*?)\s*```", RegexOptions.Compiled);

    /// <summary>
    /// 清理 LLM 返回的 JSON 字符串，移除可能的 Markdown 代码块围栏
    /// </summary>
    /// <param name="content">LLM 返回的原始字符串</param>
    /// <returns>清理后的 JSON 字符串</returns>
    public static string CleanJsonContent(this string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return "{}";

        content = content.Trim();

        // 1. 尝试匹配 Markdown 代码块围栏
        var match = JsonFenceRegex.Match(content);
        if (match.Success)
        {
            content = match.Groups[1].Value.Trim();
        }

        // 2. 进一步清理：找到第一个 '{'/'[' 和最后一个 '}'/']'，截取中间内容
        // 这样可以过滤掉 LLM 在 JSON 前后添加的解释性文字或杂质（如反斜杠）
        int firstBrace = content.IndexOf('{');
        int firstBracket = content.IndexOf('[');
        int start = (firstBrace >= 0 && (firstBracket < 0 || firstBrace < firstBracket)) ? firstBrace : firstBracket;

        int lastBrace = content.LastIndexOf('}');
        int lastBracket = content.LastIndexOf(']');
        int end = (lastBrace >= 0 && (lastBracket < 0 || lastBrace > lastBracket)) ? lastBrace : lastBracket;

        if (start >= 0 && end > start)
        {
            content = content.Substring(start, end - start + 1);
        }

        // 3. 清理无效的 UTF-8 字符和控制字符
        content = SanitizeUtf8(content);

        // 4. 尝试修复不完整的 JSON（未闭合的括号）
        content = RepairIncompleteJson(content);

        return content;
    }

    /// <summary>
    /// 尝试修复不完整的 JSON（自动补全缺失的闭合括号）
    /// </summary>
    private static string RepairIncompleteJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return json;

        var stack = new Stack<char>();
        var inString = false;
        var escaping = false;

        // 遍历 JSON 字符串，追踪括号匹配状态
        for (int i = 0; i < json.Length; i++)
        {
            char c = json[i];

            if (escaping)
            {
                escaping = false;
                continue;
            }

            if (c == '\\')
            {
                escaping = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString) continue;

            // 处理括号
            if (c == '{' || c == '[')
            {
                stack.Push(c);
            }
            else if (c == '}')
            {
                if (stack.Count > 0 && stack.Peek() == '{')
                    stack.Pop();
            }
            else if (c == ']')
            {
                if (stack.Count > 0 && stack.Peek() == '[')
                    stack.Pop();
            }
        }

        // 如果栈中还有未闭合的括号，尝试补全
        if (stack.Count > 0)
        {
            var closingChars = new StringBuilder();
            
            // 如果字符串未闭合，先闭合字符串
            if (inString)
            {
                closingChars.Append('"');
            }

            // 按照栈的顺序补全闭合括号
            while (stack.Count > 0)
            {
                char opening = stack.Pop();
                closingChars.Append(opening == '{' ? '}' : ']');
            }

            return json + closingChars.ToString();
        }

        return json;
    }

    /// <summary>
    /// 清理字符串中的无效 UTF-8 字符和控制字符
    /// </summary>
    private static string SanitizeUtf8(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        var bytes = Encoding.UTF8.GetBytes(input);
        var sanitizedBytes = new List<byte>();
        
        int i = 0;
        while (i < bytes.Length)
        {
            byte b = bytes[i];
            
            // 单字节 ASCII 字符 (0x00-0x7F)
            if (b <= 0x7F)
            {
                // 保留可打印字符、空格、换行符、制表符
                if (b >= 0x20 || b == 0x09 || b == 0x0A || b == 0x0D)
                {
                    sanitizedBytes.Add(b);
                }
                i++;
            }
            // 多字节 UTF-8 序列
            else
            {
                int sequenceLength = 0;
                
                // 检测 UTF-8 序列长度
                if ((b & 0xE0) == 0xC0) sequenceLength = 2;      // 110xxxxx
                else if ((b & 0xF0) == 0xE0) sequenceLength = 3; // 1110xxxx
                else if ((b & 0xF8) == 0xF0) sequenceLength = 4; // 11110xxx
                
                // 验证是否有足够的字节并且后续字节格式正确
                bool validSequence = sequenceLength > 0 && i + sequenceLength <= bytes.Length;
                
                if (validSequence)
                {
                    // 验证后续字节都是 10xxxxxx 格式
                    for (int j = 1; j < sequenceLength; j++)
                    {
                        if ((bytes[i + j] & 0xC0) != 0x80)
                        {
                            validSequence = false;
                            break;
                        }
                    }
                }
                
                if (validSequence)
                {
                    // 保留有效的 UTF-8 序列
                    for (int j = 0; j < sequenceLength; j++)
                    {
                        sanitizedBytes.Add(bytes[i + j]);
                    }
                    i += sequenceLength;
                }
                else
                {
                    // 跳过无效字节
                    i++;
                }
            }
        }
        
        return Encoding.UTF8.GetString(sanitizedBytes.ToArray());
    }
}
