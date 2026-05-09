namespace DevNexus.Domain.Abstractions;

/// <summary>
/// 加密服务接口
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// 加密字符串
    /// </summary>
    string Encrypt(string plainText);
    
    /// <summary>
    /// 解密字符串
    /// </summary>
    string Decrypt(string cipherText);
}
