namespace DevNexus.Domain.Configuration;

/// <summary>
/// 加密服务配置选项
/// </summary>
public class EncryptionOptions
{
    /// <summary>
    /// 加密密钥（用于提供商 API Key 加密，至少32字符）
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 加密初始化向量（16字符）
    /// </summary>
    public string IV { get; set; } = string.Empty;

    /// <summary>
    /// 验证配置是否有效
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Key))
        {
            throw new InvalidOperationException(
                "Encryption Key is required for provider API key encryption. " +
                "Set it via appsettings.json (Encryption:Key) or environment variable (Encryption__Key). " +
                "Generate using: openssl rand -base64 32");
        }

        if (Key.Length < 32)
        {
            throw new InvalidOperationException(
                $"Encryption Key must be at least 32 characters long. Current length: {Key.Length}");
        }

        if (string.IsNullOrWhiteSpace(IV))
        {
            throw new InvalidOperationException(
                "Encryption IV is required. " +
                "Set it via appsettings.json (Encryption:IV) or environment variable (Encryption__IV). " +
                "Generate using: openssl rand -base64 16");
        }

        if (IV.Length < 16)
        {
            throw new InvalidOperationException(
                $"Encryption IV must be at least 16 characters long. Current length: {IV.Length}");
        }
    }
}
