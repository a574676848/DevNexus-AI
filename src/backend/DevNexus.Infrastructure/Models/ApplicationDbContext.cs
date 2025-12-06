using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using System.Linq.Expressions;
using DevNexus.Infrastructure.Models.Base;

namespace DevNexus.Infrastructure.Models;

/// <summary>
/// 应用程序数据库上下文
/// 继承自 IdentityDbContext 以支持 ASP.NET Identity
/// </summary>
public class ApplicationDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">数据库上下文选项</param>
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }
    
    /// <summary>
    /// 聊天消息表
    /// </summary>
    public DbSet<ChatMessage> ChatMessages { get; set; }
    
    /// <summary>
    /// 文档资产表
    /// </summary>
    public DbSet<Artifact> Artifacts { get; set; }
    
    /// <summary>
    /// 对话会话表
    /// </summary>
    public DbSet<ChatSession> ChatSessions { get; set; }
    
    /// <summary>
    /// 刷新令牌表
    /// </summary>
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    
    /// <summary>
    /// 配置实体关系和全局过滤
    /// </summary>
    /// <param name="modelBuilder">模型构建器</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // 配置软删除全局过滤
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var property = Expression.Property(parameter, "IsDeleted");
                var constant = Expression.Constant(false);
                var equal = Expression.Equal(property, constant);
                var lambda = Expression.Lambda(equal, parameter);
                
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }
        
        // 配置聊天消息的树状结构
        modelBuilder.Entity<ChatMessage>()
            .HasOne(m => m.ParentMessage)
            .WithMany(m => m.ChildMessages)
            .HasForeignKey(m => m.ParentMessageId)
            .OnDelete(DeleteBehavior.Restrict);
        
        // 配置资产的版本关系
        modelBuilder.Entity<Artifact>()
            .HasOne(a => a.ParentArtifact)
            .WithMany()
            .HasForeignKey(a => a.ParentArtifactId)
            .OnDelete(DeleteBehavior.Restrict);
        
        // 配置 JSONB 列
        modelBuilder.Entity<ChatMessage>()
            .Property(m => m.Content)
            .HasColumnType("jsonb");
        
        modelBuilder.Entity<ChatMessage>()
            .Property(m => m.Metadata)
            .HasColumnType("jsonb");
        
        modelBuilder.Entity<Artifact>()
            .Property(a => a.Metadata)
            .HasColumnType("jsonb");
        
        // 配置 RefreshToken
        modelBuilder.Entity<RefreshToken>()
            .HasOne(rt => rt.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        modelBuilder.Entity<RefreshToken>()
            .HasIndex(rt => rt.TokenHash)
            .IsUnique();
        
        modelBuilder.Entity<RefreshToken>()
            .HasIndex(rt => new { rt.UserId, rt.IsRevoked });
        
        // 配置 User 扩展属性
        modelBuilder.Entity<User>()
            .HasIndex(u => u.IsEnabled);
    }
}
