using System.Security.Cryptography;
using System.Text;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// Prompt 指纹工具。
/// </summary>
public static class PromptFingerprint
{
    /// <summary>
    /// 计算稳定文本指纹。
    /// </summary>
    public static string ComputeHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
