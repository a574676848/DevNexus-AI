using System.Text.Json;
using System.Text;

namespace DevNexus.Client.Shared.Helpers;

/// <summary>
/// 元数据辅助工具类
/// 提供从 Dictionary 和 BlockDto 中提取数据的通用方法
/// </summary>
public static class MetadataHelper
{
    /// <summary>
    /// 从 Metadata 字典中获取字符串值
    /// </summary>
    /// <param name="metadata">元数据字典</param>
    /// <param name="key">键名</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>字符串值</returns>
    public static string GetString(Dictionary<string, object>? metadata, string key, string defaultValue = "")
    {
        if (metadata == null) return defaultValue;
        if (metadata.TryGetValue(key, out var value) && value != null)
        {
            if (value is JsonElement element)
            {
                return element.ValueKind == JsonValueKind.String 
                    ? element.GetString() ?? defaultValue 
                    : element.ToString();
            }
            return value.ToString() ?? defaultValue;
        }
        return defaultValue;
    }

    /// <summary>
    /// 从 Metadata 字典中获取 Guid 值
    /// </summary>
    public static Guid? GetGuid(Dictionary<string, object>? metadata, string key)
    {
        if (metadata == null || !metadata.TryGetValue(key, out var value) || value == null) 
            return null;

        if (value is JsonElement element && element.ValueKind == JsonValueKind.String)
        {
            if (Guid.TryParse(element.GetString(), out var guid)) return guid;
        }
        else if (Guid.TryParse(value.ToString(), out var guid))
        {
            return guid;
        }
        return null;
    }

    /// <summary>
    /// 从 Metadata 字典中获取整数值
    /// </summary>
    public static int? GetInt(Dictionary<string, object>? metadata, string key)
    {
        if (metadata == null || !metadata.TryGetValue(key, out var value) || value == null) 
            return null;

        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number)
                return element.GetInt32();
            if (element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), out var parsed))
                return parsed;
        }
        else if (int.TryParse(value.ToString(), out var parsed))
        {
            return parsed;
        }
        return null;
    }

    /// <summary>
    /// 从 Metadata 字典中获取布尔值
    /// </summary>
    public static bool? GetBool(Dictionary<string, object>? metadata, string key)
    {
        if (metadata == null || !metadata.TryGetValue(key, out var value) || value == null) 
            return null;

        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.True) return true;
            if (element.ValueKind == JsonValueKind.False) return false;
            if (element.ValueKind == JsonValueKind.String && bool.TryParse(element.GetString(), out var parsed))
                return parsed;
        }
        else if (bool.TryParse(value.ToString(), out var parsed))
        {
            return parsed;
        }
        return null;
    }

    /// <summary>
    /// 从 Metadata 字典中获取字符串列表。
    /// </summary>
    public static List<string> GetStringList(Dictionary<string, object>? metadata, string key)
    {
        if (metadata == null || !metadata.TryGetValue(key, out var value) || value == null)
        {
            return new List<string>();
        }

        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                return element.EnumerateArray()
                    .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Select(item => item!)
                    .ToList();
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                var single = element.GetString();
                return string.IsNullOrWhiteSpace(single) ? new List<string>() : new List<string> { single };
            }
        }

        if (value is IEnumerable<string> stringEnumerable)
        {
            return stringEnumerable.Where(item => !string.IsNullOrWhiteSpace(item)).ToList();
        }

        if (value is System.Collections.IEnumerable enumerable && value is not string)
        {
            var result = new List<string>();
            foreach (var item in enumerable)
            {
                var stringValue = item?.ToString();
                if (!string.IsNullOrWhiteSpace(stringValue))
                {
                    result.Add(stringValue);
                }
            }

            return result;
        }

        return new List<string>();
    }

    /// <summary>
    /// 解析思维链内容为列表
    /// </summary>
    /// <param name="content">思维链原始内容</param>
    /// <returns>步骤列表</returns>
    public static List<string> ParseThoughts(string? content)
    {
        if (string.IsNullOrEmpty(content)) return new List<string>();
        return content.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    /// <summary>
    /// 按 thinking block 边界拼接内容，避免多条思考在 UI 中黏成一整段。
    /// </summary>
    public static string JoinThoughtSegments(IEnumerable<string?> segments)
    {
        var builder = new StringBuilder();

        foreach (var segment in segments)
        {
            AppendThoughtSegment(builder, segment);
        }

        return builder.ToString();
    }

    /// <summary>
    /// 追加一段 thinking 内容；当相邻 block 间缺少换行时自动补一个换行符。
    /// </summary>
    public static void AppendThoughtSegment(StringBuilder builder, string? segment)
    {
        if (builder == null || string.IsNullOrEmpty(segment))
        {
            return;
        }

        if (builder.Length > 0 &&
            builder[^1] != '\n' &&
            builder[^1] != '\r' &&
            segment[0] != '\n' &&
            segment[0] != '\r')
        {
            builder.Append('\n');
        }

        builder.Append(segment);
    }
}
