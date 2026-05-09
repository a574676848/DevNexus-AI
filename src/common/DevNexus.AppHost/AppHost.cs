using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// 使用与 DevNexus.Shared.Constants.ConnectionStringNames 相同的常量值
// AppHost 项目因 Aspire SDK 限制无法直接引用 Shared 项目
// 如需修改常量，请同步修改 DevNexus.Shared/Constants/ConnectionStringNames.cs
const string DbConnection = "devnexus";
const string RedisConnection = "redis";
const string SeqConnection = "seq";
const string QdrantConnection = "qdrant";
const string ElasticsearchConnection = "elasticsearch";
const string PaddleOcrConnection = "paddle-ocr";

var builder = DistributedApplication.CreateBuilder(args);

// =====================================================
// 参数定义（敏感配置）
// 使用 AddParameter 管理敏感信息，支持 User Secrets 和环境变量
// =====================================================

// 开发环境默认密钥说明（仅用于文档参考，实际配置通过 User Secrets 或 appsettings.Development.json）
// JWT Key: 至少 32 字符的安全密钥
// Encryption Key: 32 字符的 AES-256 密钥
// Encryption IV: 16 字符的初始化向量

// JWT 密钥（开发环境有默认值，生产环境必须配置）
// 开发环境: 使用默认值或 dotnet user-secrets set "Parameters:jwt-key" "your-secret-key"
// 生产环境: 必须设置环境变量 Parameters__jwt-key
var jwtKey = builder.AddParameter("jwt-key", secret: true);

// JWT 颁发者和受众（有默认值）
var jwtIssuer = builder.AddParameter("jwt-issuer");
var jwtAudience = builder.AddParameter("jwt-audience");

// 加密密钥（开发环境有默认值，生产环境必须配置）
// 开发环境: 使用默认值或通过 User Secrets 配置
// 生产环境: 必须设置环境变量 Parameters__encryption-key 和 Parameters__encryption-iv
var encryptionKey = builder.AddParameter("encryption-key", secret: true);
var encryptionIV = builder.AddParameter("encryption-iv", secret: true);

// =====================================================
// 外部资源定义（连接到已存在的服务）
// 使用 AddConnectionString 连接外部托管的服务
// =====================================================

// PostgreSQL 数据库（外部服务）
// 连接字符串从 appsettings.json 或环境变量 ConnectionStrings__devnexus 读取
var postgres = builder.AddConnectionString(DbConnection);

// Redis 缓存（外部服务）
// 连接字符串从 appsettings.json 或环境变量 ConnectionStrings__redis 读取
var redis = builder.AddConnectionString(RedisConnection);

// Seq 日志服务（外部服务）
// 连接字符串从 appsettings.json 或环境变量 ConnectionStrings__seq 读取
var seq = builder.AddConnectionString(SeqConnection);

// Qdrant 向量数据库（外部服务）
// 连接字符串从 appsettings.json 或环境变量 ConnectionStrings__qdrant 读取
var qdrant = builder.AddConnectionString(QdrantConnection);

// Elasticsearch 搜索引擎（外部服务）
// 连接字符串从 appsettings.json 或环境变量 ConnectionStrings__elasticsearch 读取
var elasticsearch = builder.AddConnectionString(ElasticsearchConnection);

// PaddleOCR 服务（外部服务）
// 连接字符串从 appsettings.json 或环境变量 ConnectionStrings__paddle-ocr 读取
var paddleocr = builder.AddConnectionString(PaddleOcrConnection);

// =====================================================
// API 服务
// =====================================================

var apiService = builder.AddProject<Projects.DevNexus_ApiService>("apiservice")
    .WithReference(postgres)        // 注入数据库连接字符串
    .WithReference(redis)           // 注入 Redis 连接字符串
    .WithReference(seq)             // 注入 Seq 连接字符串
    .WithReference(qdrant)          // 注入 Qdrant 连接字符串
    .WithReference(elasticsearch)   // 注入 Elasticsearch 连接字符串
    .WithReference(paddleocr)       // 注入 PaddleOCR 连接字符串
                                    // 注入 JWT 配置参数（开发环境使用默认值）
    .WithEnvironment("Jwt__Key", jwtKey)
    .WithEnvironment("Jwt__Issuer", jwtIssuer)
    .WithEnvironment("Jwt__Audience", jwtAudience)
    // 注入加密密钥参数（开发环境使用默认值）
    .WithEnvironment("Encryption__Key", encryptionKey)
    .WithEnvironment("Encryption__IV", encryptionIV)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

// =====================================================
// Web 客户端
// =====================================================

var webClient = builder.AddProject<Projects.DevNexus_Client_Web>("web-client")
    .WithReference(apiService) // 自动注入 ApiService 地址
    .WithExternalHttpEndpoints();

// 解决 Aspire Dashboard 资源监控的 HTTP/2 Ping 超时问题
// 增加 Ping 延迟容忍度，防止开发环境因高负载导致的连接中断
if (builder.Environment.IsDevelopment())
{
    builder.Services.Configure<Microsoft.Extensions.Http.HttpClientFactoryOptions>(options =>
    {
        options.HttpMessageHandlerBuilderActions.Add(handlerBuilder =>
        {
            // 针对通过工厂创建的所有 HttpClient，调整 SocketsHttpHandler 配置
            if (handlerBuilder.PrimaryHandler is System.Net.Http.SocketsHttpHandler socketHandler)
            {
                socketHandler.KeepAlivePingDelay = TimeSpan.FromSeconds(300);
                socketHandler.KeepAlivePingTimeout = TimeSpan.FromSeconds(60);
                socketHandler.EnableMultipleHttp2Connections = true;
            }
        });
    });
}

// 开发环境提示
if (builder.Environment.EnvironmentName == "Development")
{
    Console.WriteLine("========================================");
    Console.WriteLine("🔧 开发环境配置提示");
    Console.WriteLine("========================================");
    Console.WriteLine("当前使用开发环境默认密钥。");
    Console.WriteLine();
    Console.WriteLine("如需自定义密钥，请使用以下命令：");
    Console.WriteLine("  cd src/common/DevNexus.AppHost");
    Console.WriteLine("  dotnet user-secrets set \"Parameters:jwt-key\" \"your-key\"");
    Console.WriteLine("  dotnet user-secrets set \"Parameters:encryption-key\" \"your-key\"");
    Console.WriteLine("  dotnet user-secrets set \"Parameters:encryption-iv\" \"your-iv\"");
    Console.WriteLine();
    Console.WriteLine("⚠️  生产环境必须配置环境变量！");
    Console.WriteLine("========================================");
}

builder.Build().Run();
