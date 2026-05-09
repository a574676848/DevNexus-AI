namespace DevNexus.Shared.Constants;

/// <summary>
/// 连接字符串名称常量
/// 与 AppHost 配置保持一致
/// </summary>
public static class ConnectionStringNames
{
    /// <summary>
    /// PostgreSQL 数据库连接字符串名称
    /// </summary>
    public const string Database = "devnexus";

    /// <summary>
    /// Redis 缓存连接字符串名称
    /// </summary>
    public const string Redis = "redis";

    /// <summary>
    /// Seq 日志服务连接字符串名称
    /// </summary>
    public const string Seq = "seq";

    /// <summary>
    /// Qdrant 向量数据库连接字符串名称
    /// </summary>
    public const string Qdrant = "qdrant";

    /// <summary>
    /// Elasticsearch 搜索引擎连接字符串名称
    /// </summary>
    public const string Elasticsearch = "elasticsearch";

    /// <summary>
    /// PaddleOCR 连接字符串名称
    /// </summary>
    public const string PaddleOcr = "paddle-ocr";
}
