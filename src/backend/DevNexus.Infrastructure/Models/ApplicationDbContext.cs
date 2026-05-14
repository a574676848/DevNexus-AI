using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DevNexus.Infrastructure.Models;

/// <summary>
/// 应用程序数据库上下文
/// 继承自 IdentityDbContext 以支持 ASP.NET Identity
/// </summary>
public class ApplicationDbContext : IdentityDbContext<InfrastructureUser, IdentityRole<Guid>, Guid>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">数据库上下文选项</param>
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<ContextSwarmSession> ContextSwarmSessions { get; set; }
    public DbSet<ContextWorkPackageRecord> ContextWorkPackages { get; set; }
    public DbSet<Artifact> Artifacts { get; set; }
    public DbSet<FileAsset> FileAssets { get; set; }
    public DbSet<FileVersion> FileVersions { get; set; }
    public DbSet<FileTask> FileTasks { get; set; }
    public DbSet<UploadSession> UploadSessions { get; set; }
    public DbSet<ChatSession> ChatSessions { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<LLMProvider> LLMProviders { get; set; }
    public DbSet<EmbeddingProvider> EmbeddingProviders { get; set; }
    public DbSet<SearchProvider> SearchProviders { get; set; }
    public DbSet<StorageProvider> StorageProviders { get; set; }
    public DbSet<UserIntegration> UserIntegrations => Set<UserIntegration>();
    public DbSet<ModelInvocationAudit> ModelInvocationAudits { get; set; }
    public DbSet<AuditSceneDefinition> AuditSceneDefinitions { get; set; }
    public DbSet<ModelPricing> ModelPrices { get; set; }
    public DbSet<UserFact> UserFacts { get; set; }
    public DbSet<SystemExperience> SystemExperiences { get; set; }
    public DbSet<CliApprovalGrant> CliApprovalGrants { get; set; }
    public DbSet<CliExecSession> CliExecSessions { get; set; }
    public DbSet<CliExecCheckpoint> CliExecCheckpoints { get; set; }
    public DbSet<TerminalStream> TerminalStreams { get; set; }
    public DbSet<QueuedChatMessage> QueuedChatMessages { get; set; }
    public DbSet<PendingInteraction> PendingInteractions { get; set; }
    public DbSet<UpdateRelease> UpdateReleases { get; set; }
    public DbSet<UpdateReleaseArtifact> UpdateReleaseArtifacts { get; set; }
    public DbSet<UpdateRollout> UpdateRollouts { get; set; }
    public DbSet<UpdateClientEvent> UpdateClientEvents { get; set; }

    /// <summary>
    /// 配置实体关系和全局过滤
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyDevNexusModelConfigurations();
    }
}
