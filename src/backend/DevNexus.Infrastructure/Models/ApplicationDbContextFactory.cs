using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace DevNexus.Infrastructure.Models;

/// <summary>
/// 设计时 DbContext 工厂
/// 用于 EF Core 迁移和工具
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    /// <summary>
    /// 创建设计时数据库上下文。
    /// </summary>
    /// <param name="args">EF Core 工具传入的参数。</param>
    /// <returns>设计时数据库上下文。</returns>
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        // 构建配置
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../DevNexus.ApiService"))
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        // 获取连接字符串
        var connectionString = configuration.GetConnectionString("devnexus")
            ?? throw new InvalidOperationException("缺少数据库连接字符串：ConnectionStrings:devnexus");

        // 配置 DbContext
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
