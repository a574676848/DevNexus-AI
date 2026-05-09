using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// Thinking 链路调试辅助工具。
/// 统一生成短哈希和日志预览，便于跨阶段对比同一段内容。
/// </summary>
internal static class ThinkingTraceHelper
{
    private const int PreviewMaxLength = 80;

    public static string ComputeHash(string? content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return "empty";
        }

        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash[..6]);
    }

    public static string CreatePreview(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var normalized = Regex.Replace(content, @"\s+", " ").Trim();
        if (normalized.Length <= PreviewMaxLength)
        {
            return normalized;
        }

        return normalized[..PreviewMaxLength] + "...";
    }
}