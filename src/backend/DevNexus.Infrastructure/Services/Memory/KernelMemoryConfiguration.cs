using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.MemoryDb.Qdrant;

namespace DevNexus.Infrastructure.Services.Memory;

/// <summary>
/// Kernel Memory 服务配置
/// 提供 IKernelMemory 实例的创建和配置
/// </summary>
public static class KernelMemoryConfiguration
{
    /// <summary>
    /// 添加 Kernel Memory 服务到 DI 容器
    /// 支持"记忆海马体"模块的完整功能：
    /// - 显性语义记忆: PostgreSQL UserFacts 表（直接由 UserMemoryService 管理）
    /// - 隐性情境记忆: Qdrant 向量数据库（通过 Kernel Memory 管理）
    /// 
    /// 注意：摘要生成由 MemoryConsolidationJob 外部完成（使用 IKernelService），
    /// Kernel Memory 仅负责向量化和存储，因此使用 WithoutTextGenerator()。
    /// </summary>
    public static IServiceCollection AddKernelMemoryServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 注册 Kernel Memory Embedding 适配器（使用项目现有的 IEmbeddingProviderFactory）
        services.AddSingleton<KernelMemoryEmbeddingAdapter>();

        // 注册 IKernelMemory 单例
        services.AddSingleton<IKernelMemory>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<MemoryServerless>>();
            var embeddingAdapter = sp.GetRequiredService<KernelMemoryEmbeddingAdapter>();
            var qdrantOptions = configuration.GetSection("Qdrant").Get<QdrantOptions>() ?? new QdrantOptions();
            var qdrantConnectionString = configuration.GetConnectionString("qdrant");
            
            // 解析连接字符串获取 Host 和 Port
            var (host, port) = ParseQdrantConnectionString(qdrantConnectionString);

            return new KernelMemoryBuilder()
                .WithCustomEmbeddingGenerator(embeddingAdapter)
                .WithoutTextGenerator() // 摘要由 MemoryConsolidationJob 外部生成，KM 只做向量化存储
                .WithQdrantMemoryDb(new QdrantConfig
                {
                    Endpoint = $"http://{host}:{port - 1}", // Qdrant HTTP 端口 (gRPC 端口 - 1)
                    APIKey = qdrantOptions.ApiKey ?? ""
                })
                .Build<MemoryServerless>(new KernelMemoryBuilderBuildOptions 
                { 
                    AllowMixingVolatileAndPersistentData = true 
                });
        });

        return services;
    }

    /// <summary>
    /// 解析 Qdrant 连接字符串
    /// </summary>
    private static (string host, int port) ParseQdrantConnectionString(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString)) 
            return ("localhost", 6334);
        
        var uri = connectionString.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? new Uri(connectionString) 
            : new Uri($"http://{connectionString}");
        
        // 优先使用 gRPC 端口 6334
        var grpcPort = uri.Port == 6333 ? 6334 : uri.Port;
        return (uri.Host, grpcPort);
    }
}
