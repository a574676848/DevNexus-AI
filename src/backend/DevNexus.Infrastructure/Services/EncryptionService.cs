using System.Security.Cryptography;
using System.Text;
// using DevNexus.Domain.Abstractions via GlobalUsings
using Microsoft.Extensions.Configuration;

namespace DevNexus.Infrastructure.Services;

/// <summary>
/// 加密服务实现 (使用 AES)
/// </summary>
public class EncryptionService : IEncryptionService
{
    private readonly byte[] _key;
    private readonly byte[] _iv;
    
    public EncryptionService(IConfiguration configuration)
    {
        // 从配置读取加密密钥
        var keyString = configuration["Encryption:Key"] 
            ?? throw new InvalidOperationException("Encryption key not configured");
        var ivString = configuration["Encryption:IV"] 
            ?? throw new InvalidOperationException("Encryption IV not configured");
            
        _key = Convert.FromBase64String(keyString);
        _iv = Convert.FromBase64String(ivString);
    }
    
    public string Encrypt(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;
        
        var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var msEncrypt = new MemoryStream();
        using var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
        using (var swEncrypt = new StreamWriter(csEncrypt))
        {
            swEncrypt.Write(plainText);
        }
        
        return Convert.ToBase64String(msEncrypt.ToArray());
    }
    
    public string Decrypt(string cipherText)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;
        
        var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var msDecrypt = new MemoryStream(Convert.FromBase64String(cipherText));
        using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
        using var srDecrypt = new StreamReader(csDecrypt);
        
        return srDecrypt.ReadToEnd();
    }
}
