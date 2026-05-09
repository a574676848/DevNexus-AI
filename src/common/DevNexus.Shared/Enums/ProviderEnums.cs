namespace DevNexus.Shared.Enums;

/// <summary>
/// LLM供应商类型
/// </summary>
public enum ProviderType
{
    OpenAICompatible = 1,
    Gemini = 2,
    Kimi = 3,
    MiniMax = 4,
    DeepSeek = 5,
    GLM = 6,
}

/// <summary>
/// Embedding供应商类型
/// </summary>
public enum EmbeddingProviderType
{
    Doubao = 1,
    OpenAI = 2,
    Local = 3
}

    /// <summary>
    /// 搜索供应商类型
    /// </summary>
    public enum SearchProviderType
    {
        SearXNG = 3,
        Tavily = 4,
        JinaReader = 10,
        Firecrawl = 11
    }

/// <summary>
/// 存储供应商类型
/// </summary>
public enum StorageProviderType
{
    /// <summary>
    /// 本地存储（开发环境）
    /// </summary>
    Local = 0,
    
    /// <summary>
    /// AWS S3
    /// </summary>
    AwsS3 = 1,
    
    /// <summary>
    /// 阿里云 OSS
    /// </summary>
    AliyunOss = 2,
    
    /// <summary>
    /// 七牛云 Kodo
    /// </summary>
    QiniuKodo = 3,
    
    /// <summary>
    /// 腾讯云 COS
    /// </summary>
    TencentCos = 4,
    
    /// <summary>
    /// MinIO
    /// </summary>
    MinIO = 5,
    
    /// <summary>
    /// Cloudflare R2
    /// </summary>
    CloudflareR2 = 6,
    
    /// <summary>
    /// 其他 S3 兼容服务
    /// </summary>
    S3Compatible = 99
}

/// <summary>
/// 笔记系统供应商类型
/// </summary>
public enum NoteProviderType
{
    /// <summary>
    /// Memos - 开源笔记系统
    /// </summary>
    Memos = 1,
    
    /// <summary>
    /// Notion - 企业级协作平台
    /// </summary>
    Notion = 2,
    
    /// <summary>
    /// Obsidian - 本地知识库
    /// </summary>
    Obsidian = 3,
    
    /// <summary>
    /// 其他自定义笔记系统
    /// </summary>
    Custom = 99
}

/// <summary>
/// 验证状态
/// </summary>
public enum ValidationStatus
{
    Unknown = 0,
    Valid = 1,
    Invalid = 2,
    Validating = 3
}
