using System.Linq.Expressions;
using DevNexus.Domain.Entities.Base;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace DevNexus.Infrastructure.Models;

internal static class ApplicationDbContextModelBuilderExtensions
{
    internal static void ApplyDevNexusModelConfigurations(this ModelBuilder modelBuilder)
    {
        modelBuilder.ConfigureIdentityModels();
        ConfigureSoftDeleteFilters(modelBuilder);
        ConfigureChatModels(modelBuilder);
        ConfigureUpdateModels(modelBuilder);
        ConfigureProviderModels(modelBuilder);
        ConfigureOperationalModels(modelBuilder);
        modelBuilder.ConfigureUserOwnedModels();
        modelBuilder.ConfigureFileModels();
    }

    private static void ConfigureSoftDeleteFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var property = Expression.Property(parameter, "IsDeleted");
            var constant = Expression.Constant(false);
            var equal = Expression.Equal(property, constant);
            var lambda = Expression.Lambda(equal, parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
        }
    }

    private static void ConfigureChatModels(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChatMessage>()
            .HasOne(m => m.ParentMessage)
            .WithMany(m => m.ChildMessages)
            .HasForeignKey(m => m.ParentMessageId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Artifact>()
            .HasOne(a => a.ParentArtifact)
            .WithMany()
            .HasForeignKey(a => a.ParentArtifactId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Artifact>()
            .Ignore(a => a.Message);

        modelBuilder.Entity<Artifact>()
            .HasIndex(a => a.MessageId);

        modelBuilder.Entity<ChatMessage>()
            .Property(m => m.Content)
            .HasColumnType("jsonb");

        modelBuilder.Entity<ChatMessage>()
            .Property(m => m.Metadata)
            .HasColumnType("jsonb");

        modelBuilder.Entity<ChatSession>()
            .Property(s => s.Metadata)
            .HasColumnType("jsonb");

        modelBuilder.Entity<PendingInteraction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.SessionId, e.Status });
            entity.HasIndex(e => new { e.SessionId, e.Kind, e.SourceTool });

            entity.HasOne(e => e.ChatSession)
                .WithMany()
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Message)
                .WithMany()
                .HasForeignKey(e => e.MessageId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.Property(e => e.Kind)
                .HasConversion(
                    value => value.ToWireValue(),
                    value => PendingInteractionKindExtensions.Parse(value))
                .HasMaxLength(32);

            entity.Property(e => e.Status)
                .HasConversion(
                    value => value.ToWireValue(),
                    value => PendingInteractionStatusExtensions.Parse(value))
                .HasMaxLength(32);

            entity.Property(e => e.SuggestedAction)
                .HasConversion(
                    value => value.HasValue ? value.Value.ToWireValue() : null,
                    value => string.IsNullOrWhiteSpace(value) ? null : ToolSuggestedActionExtensions.Parse(value))
                .HasMaxLength(32);

            entity.Property(e => e.Title)
                .HasMaxLength(256);

            entity.Property(e => e.Description)
                .HasMaxLength(4000);

            entity.Property(e => e.SourceTool)
                .HasMaxLength(256);

            entity.Property(e => e.RetryToken)
                .HasMaxLength(128);

            entity.Property(e => e.RequestedData)
                .HasColumnType("jsonb");

            entity.Property(e => e.ResolutionData)
                .HasColumnType("jsonb");
        });

        modelBuilder.Entity<Artifact>()
            .Property(a => a.Metadata)
            .HasColumnType("jsonb");

        modelBuilder.Entity<Artifact>()
            .Property(a => a.Status)
            .HasConversion(
                status => status.ToWireValue(),
                value => ArtifactLifecycleStatusExtensions.Parse(value))
            .HasMaxLength(32);

        modelBuilder.Entity<ContextSwarmSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SessionId).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.HasMany(e => e.Packages)
                .WithOne(t => t.ContextSwarmSession)
                .HasForeignKey(t => t.ContextSwarmSessionId)
                .OnDelete(DeleteBehavior.Cascade);

        });

        modelBuilder.Entity<ContextWorkPackageRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TaskId);
            entity.HasIndex(e => e.ContextSwarmSessionId);
            entity.Property(e => e.ContextType)
                .HasMaxLength(64);
            entity.Property(e => e.ExecutionStrategy)
                .HasMaxLength(64);
            entity.Property(e => e.ExecutorName)
                .HasMaxLength(128);
            entity.Property(e => e.CommandLine)
                .HasMaxLength(2048);
            entity.Property(e => e.WorkingDirectory)
                .HasMaxLength(1024);
            entity.Property(e => e.FailureReason)
                .HasMaxLength(4000);
            entity.Property(e => e.Dependencies)
                .HasColumnType("jsonb");
            entity.Property(e => e.LogicalUnits)
                .HasColumnType("jsonb");
            entity.Property(e => e.InputContracts)
                .HasColumnType("jsonb");
            entity.Property(e => e.OutputContracts)
                .HasColumnType("jsonb");
            entity.Property(e => e.OwnedFiles)
                .HasColumnType("jsonb");
            entity.Property(e => e.OwnedSymbols)
                .HasColumnType("jsonb");
        });

        // 排队聊天消息配置
        modelBuilder.Entity<QueuedChatMessage>(entity =>
        {
            entity.HasKey(e => e.Id);

            // 复合索引：按会话 + 状态 + 序号排序查询
            entity.HasIndex(e => new { e.ChatSessionId, e.Status, e.SequenceNumber })
                .HasDatabaseName("IX_QueuedChatMessages_SessionId_Status_SequenceNumber");

            entity.HasIndex(e => new { e.ChatSessionId, e.SequenceNumber })
                .IsUnique()
                .HasDatabaseName("IX_QueuedChatMessages_SessionId_SequenceNumber");

            // 索引：按用户 + 会话查询
            entity.HasIndex(e => new { e.UserId, e.ChatSessionId })
                .HasDatabaseName("IX_QueuedChatMessages_UserId_SessionId");

            // 与 ChatSession 的外键关系（SetNull：会话删除时排队消息保留但置空关联）
            entity.HasOne(e => e.ChatSession)
                .WithMany()
                .HasForeignKey(e => e.ChatSessionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Content 不需要 jsonb，纯文本即可
            entity.Property(e => e.Content)
                .IsRequired()
                .HasMaxLength(8000);

            entity.Property(e => e.MessageType)
                .IsRequired()
                .HasMaxLength(32);

            entity.Property(e => e.SelectedSkillName)
                .HasMaxLength(128);

            entity.Property(e => e.FailureReason)
                .HasMaxLength(2000);

            entity.Property(e => e.ArtifactIdsJson)
                .HasColumnType("jsonb");

            entity.Property(e => e.MetadataJson)
                .HasColumnType("jsonb");
        });
    }

    private static void ConfigureUpdateModels(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UpdateRelease>()
            .HasMany(r => r.Artifacts)
            .WithOne(a => a.Release)
            .HasForeignKey(a => a.ReleaseId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UpdateRollout>()
            .HasOne(r => r.Release)
            .WithMany()
            .HasForeignKey(r => r.ReleaseId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<UpdateRelease>()
            .HasIndex(r => new { r.Channel, r.Version })
            .IsUnique();

        modelBuilder.Entity<UpdateRelease>()
            .Property(r => r.Version)
            .HasMaxLength(64);

        modelBuilder.Entity<UpdateRelease>()
            .Property(r => r.Channel)
            .HasMaxLength(64);

        modelBuilder.Entity<UpdateRelease>()
            .Property(r => r.Status)
            .HasConversion(
                status => status.ToWireValue(),
                value => UpdateReleaseStatusExtensions.Parse(value))
            .HasMaxLength(32);

        modelBuilder.Entity<UpdateReleaseArtifact>()
            .Property(a => a.Platform)
            .HasMaxLength(32);

        modelBuilder.Entity<UpdateReleaseArtifact>()
            .Property(a => a.Architecture)
            .HasMaxLength(32);

        modelBuilder.Entity<UpdateReleaseArtifact>()
            .Property(a => a.PackageType)
            .HasMaxLength(32);

        modelBuilder.Entity<UpdateReleaseArtifact>()
            .HasIndex(a => new { a.ReleaseId, a.Platform, a.Architecture, a.PackageType });

        modelBuilder.Entity<UpdateRollout>()
            .Property(r => r.Platform)
            .HasMaxLength(32);

        modelBuilder.Entity<UpdateRollout>()
            .Property(r => r.Architecture)
            .HasMaxLength(32);

        modelBuilder.Entity<UpdateRollout>()
            .Property(r => r.Channel)
            .HasMaxLength(64);

        modelBuilder.Entity<UpdateRollout>()
            .HasIndex(r => new { r.Platform, r.Architecture, r.Channel, r.Enabled });

        modelBuilder.Entity<UpdateClientEvent>()
            .Property(e => e.InstallationId)
            .HasMaxLength(128);

        modelBuilder.Entity<UpdateClientEvent>()
            .Property(e => e.Platform)
            .HasMaxLength(32);

        modelBuilder.Entity<UpdateClientEvent>()
            .Property(e => e.Architecture)
            .HasMaxLength(32);

        modelBuilder.Entity<UpdateClientEvent>()
            .Property(e => e.Channel)
            .HasMaxLength(64);

        modelBuilder.Entity<UpdateClientEvent>()
            .Property(e => e.CurrentVersion)
            .HasMaxLength(64);

        modelBuilder.Entity<UpdateClientEvent>()
            .Property(e => e.TargetVersion)
            .HasMaxLength(64);

        modelBuilder.Entity<UpdateClientEvent>()
            .Property(e => e.EventType)
            .HasConversion(
                type => type.ToWireValue(),
                value => UpdateClientEventTypeExtensions.Parse(value))
            .HasMaxLength(64);

        modelBuilder.Entity<UpdateClientEvent>()
            .Property(e => e.Result)
            .HasConversion(
                result => result.ToWireValue(),
                value => UpdateClientEventResultExtensions.Parse(value))
            .HasMaxLength(32);

        modelBuilder.Entity<UpdateClientEvent>()
            .Property(e => e.ErrorCode)
            .HasMaxLength(64);

        modelBuilder.Entity<UpdateClientEvent>()
            .Property(e => e.ErrorMessage)
            .HasMaxLength(2048);

        modelBuilder.Entity<UpdateClientEvent>()
            .HasIndex(e => new { e.CreatedAt, e.EventType });

        modelBuilder.Entity<UpdateClientEvent>()
            .HasIndex(e => new { e.RolloutId, e.ReleaseId, e.ArtifactId });
    }

    private static void ConfigureProviderModels(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LLMProvider>()
            .Property(p => p.Configuration)
            .HasColumnType("jsonb");

        modelBuilder.Entity<LLMProvider>()
            .HasIndex(p => p.ProviderId);

        modelBuilder.Entity<LLMProvider>()
            .HasIndex(p => new { p.IsEnabled, p.IsDefault, p.Priority });

        modelBuilder.Entity<EmbeddingProvider>()
            .Property(p => p.Configuration)
            .HasColumnType("jsonb");

        modelBuilder.Entity<EmbeddingProvider>()
            .HasIndex(p => p.ProviderId);

        modelBuilder.Entity<EmbeddingProvider>()
            .HasIndex(p => new { p.IsEnabled, p.IsDefault, p.Priority });

        modelBuilder.Entity<SearchProvider>()
            .Property(p => p.Configuration)
            .HasColumnType("jsonb");

        modelBuilder.Entity<SearchProvider>()
            .HasIndex(p => p.ProviderId);

        modelBuilder.Entity<SearchProvider>()
            .HasIndex(p => new { p.IsEnabled, p.IsDefault, p.Priority });

        modelBuilder.Entity<StorageProvider>()
            .Property(p => p.Configuration)
            .HasColumnType("jsonb");

        modelBuilder.Entity<StorageProvider>()
            .HasIndex(p => p.ProviderId);

        modelBuilder.Entity<StorageProvider>()
            .HasIndex(p => new { p.IsEnabled, p.IsDefault, p.Priority });

        modelBuilder.Entity<ModelPricing>()
            .Property(mp => mp.ProviderType)
            .HasMaxLength(32);

        modelBuilder.Entity<ModelPricing>()
            .HasIndex(mp => new { mp.ProviderType, mp.ProviderId })
            .IsUnique();
    }

    private static void ConfigureOperationalModels(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TerminalStream>()
            .Property(stream => stream.Status)
            .HasConversion(
                status => status.ToWireValue(),
                value => TerminalStreamStatusExtensions.Parse(value))
            .HasMaxLength(50);

        modelBuilder.Entity<TerminalStream>()
            .Property(stream => stream.SessionState)
            .HasConversion(
                state => state.ToWireValue(),
                value => CliSessionStateExtensions.Parse(value))
            .HasMaxLength(50);

        modelBuilder.Entity<FileAsset>()
            .Property(a => a.Metadata)
            .HasColumnType("jsonb");

        modelBuilder.Entity<FileTask>()
            .Property(t => t.InputAssetIds)
            .HasColumnType("jsonb");

        modelBuilder.Entity<FileTask>()
            .Property(t => t.TemplateAssetIds)
            .HasColumnType("jsonb");

        modelBuilder.Entity<FileTask>()
            .Property(t => t.OutputAssetIds)
            .HasColumnType("jsonb");

        modelBuilder.Entity<ModelInvocationAudit>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.OwnerUserId, e.CreatedAt });
            entity.HasIndex(e => new { e.OwnerType, e.SceneCode, e.CreatedAt });
            entity.HasIndex(e => new { e.InvocationKind, e.CreatedAt });
            entity.HasIndex(e => new { e.Status, e.CreatedAt });
            entity.HasIndex(e => new { e.ModelId, e.CreatedAt });
            entity.HasIndex(e => new { e.SessionId, e.CreatedAt });
            entity.Property(e => e.OwnerType).IsRequired().HasMaxLength(32);
            entity.Property(e => e.InvocationKind).IsRequired().HasMaxLength(64);
            entity.Property(e => e.SceneCode).IsRequired().HasMaxLength(128);
            entity.Property(e => e.SceneCategory).IsRequired().HasMaxLength(64);
            entity.Property(e => e.ResourceType).IsRequired().HasMaxLength(64);
            entity.Property(e => e.ResourceId).HasMaxLength(128);
            entity.Property(e => e.TraceId).HasMaxLength(128);
            entity.Property(e => e.ModelId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ProviderType).IsRequired().HasMaxLength(32);
            entity.Property(e => e.ProviderName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ProviderId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.MeteringType).IsRequired().HasMaxLength(32);
            entity.Property(e => e.ToolName).HasMaxLength(256);
            entity.Property(e => e.ToolFailureReason).HasMaxLength(64);
            entity.Property(e => e.ToolSuggestedAction).HasMaxLength(64);
            entity.Property(e => e.MeteringValue).HasColumnType("decimal(18,6)");
            entity.Property(e => e.Cost).HasColumnType("decimal(18,6)");
            entity.Property(e => e.UsageSource).IsRequired().HasMaxLength(32);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(32);
            entity.Property(e => e.ErrorCode).HasMaxLength(100);
            entity.Property(e => e.ErrorMessage).HasMaxLength(4000);
        });

        modelBuilder.Entity<AuditSceneDefinition>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SceneCode).IsUnique();
            entity.HasIndex(e => new { e.IsEnabled, e.SortOrder });
            entity.Property(e => e.SceneCode)
                .IsRequired()
                .HasMaxLength(128);
            entity.Property(e => e.DisplayNameZhCn)
                .IsRequired()
                .HasMaxLength(100);
            entity.Property(e => e.ShortNameZhCn)
                .IsRequired()
                .HasMaxLength(50);
            entity.Property(e => e.DescriptionZhCn)
                .HasMaxLength(500);
            entity.Property(e => e.DisplayGroupZhCn)
                .IsRequired()
                .HasMaxLength(50);
            entity.Property(e => e.BadgeTone)
                .IsRequired()
                .HasMaxLength(20);
        });

        modelBuilder.Entity<UserFact>()
            .HasIndex(f => new { f.UserId, f.ConfidenceScore })
            .HasDatabaseName("IX_UserFacts_UserId_Confidence");

        modelBuilder.Entity<UserFact>()
            .HasIndex(f => new { f.UserId, f.Category })
            .HasDatabaseName("IX_UserFacts_UserId_Category");

        modelBuilder.Entity<UserFact>()
            .HasIndex(f => new { f.UserId, f.ContentHash })
            .HasDatabaseName("IX_UserFacts_UserId_ContentHash");

        modelBuilder.Entity<UserIntegration>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.IntegrationType, e.IsActive });
            entity.HasIndex(e => new { e.UserId, e.IntegrationType, e.IsDefault });
            entity.HasIndex(e => new { e.UserId, e.ProviderId });
            entity.Property(e => e.Configuration)
                .HasColumnType("jsonb");
        });

        modelBuilder.Entity<CliExecSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SessionKey).IsUnique();
            entity.HasIndex(e => new { e.ChatSessionId, e.IsActive });
            entity.HasIndex(e => new { e.UserId, e.LastActivityAt });
            entity.Property(e => e.SessionKey)
                .IsRequired()
                .HasMaxLength(128);
            entity.Property(e => e.ExecStatus)
                .HasConversion(
                    value => value.ToString(),
                    value => ParseCliExecStatus(value))
                .HasMaxLength(32);
            entity.Property(e => e.SessionMode)
                .HasConversion(
                    value => value.ToString(),
                    value => ParseCliSessionMode(value))
                .HasMaxLength(32);
            entity.Property(e => e.Command)
                .HasMaxLength(2000);
            entity.Property(e => e.WorkingDirectory)
                .HasMaxLength(1000);
            entity.Property(e => e.RuntimeHost)
                .HasMaxLength(100);
            entity.Property(e => e.TerminalStreamId);
            entity.Property(e => e.TerminationReason)
                .HasMaxLength(100);
        });

        modelBuilder.Entity<CliApprovalGrant>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.SessionScopeKey, e.Scope, e.MatchValue, e.ConsumedAt })
                .HasDatabaseName("IX_CliApprovalGrants_Scope_Match_ConsumedAt");
            entity.Property(e => e.SessionScopeKey)
                .IsRequired()
                .HasMaxLength(128);
            entity.Property(e => e.Scope)
                .HasConversion(
                    value => value.ToString(),
                    value => ParseCliApprovalGrantScope(value))
                .HasMaxLength(32);
            entity.Property(e => e.MatchValue)
                .IsRequired()
                .HasMaxLength(2048);
        });

        modelBuilder.Entity<CliExecCheckpoint>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.SessionKey, e.Status });
            entity.Property(e => e.SessionKey)
                .IsRequired()
                .HasMaxLength(128);
            entity.Property(e => e.Command)
                .IsRequired()
                .HasMaxLength(2000);
            entity.Property(e => e.WorkingDirectory)
                .IsRequired()
                .HasMaxLength(1000);
            entity.Property(e => e.SnapshotDirectory)
                .IsRequired()
                .HasMaxLength(1000);
            entity.Property(e => e.Status)
                .HasConversion(
                    value => value.ToString(),
                    value => ParseCliExecCheckpointStatus(value))
                .HasMaxLength(32);
        });

        modelBuilder.Entity<SystemExperience>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Intent);
            entity.HasIndex(e => e.Type);
            entity.HasIndex(e => e.LastMatchedAt);
            entity.HasIndex(e => e.UtilityScore);
        });

        modelBuilder.Entity<TerminalStream>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.MessageId);
            entity.HasIndex(e => e.ToolCallId);
            entity.HasIndex(e => e.SessionKey);
            entity.HasIndex(e => e.ChatSessionId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.LastActivityAt);
            entity.HasOne(e => e.Message)
                .WithMany(m => m.TerminalStreams)
                .HasForeignKey(e => e.MessageId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);
            entity.Property(e => e.Command)
                .IsRequired()
                .HasMaxLength(2000);
            entity.Property(e => e.WorkingDirectory)
                .HasMaxLength(1000);
            entity.Property(e => e.SessionKey)
                .HasMaxLength(200);
            entity.Property(e => e.PackageId)
                .HasMaxLength(128);
            entity.Property(e => e.LockKey)
                .HasMaxLength(1024);
            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(50);
            entity.Property(e => e.SessionState)
                .IsRequired()
                .HasMaxLength(50);
            entity.Property(e => e.RuntimeHost)
                .HasMaxLength(100);
            entity.Property(e => e.TerminationReason)
                .HasMaxLength(100);
            entity.Property(e => e.ArchivedOutputPath)
                .HasMaxLength(2000);
            entity.Property(e => e.WatchSummary)
                .HasMaxLength(1000);
            entity.Property(e => e.Output)
                .HasColumnType("text");
        });

    }

    private static CliExecStatus ParseCliExecStatus(string? value)
    {
        return Enum.TryParse<CliExecStatus>(value, true, out var parsed)
            ? parsed
            : CliExecStatus.Unknown;
    }

    private static CliSessionMode ParseCliSessionMode(string? value)
    {
        return Enum.TryParse<CliSessionMode>(value, true, out var parsed)
            ? parsed
            : CliSessionMode.Unknown;
    }

    private static CliApprovalGrantScope ParseCliApprovalGrantScope(string? value)
    {
        return Enum.TryParse<CliApprovalGrantScope>(value, true, out var parsed)
            ? parsed
            : CliApprovalGrantScope.Once;
    }

    private static CliExecCheckpointStatus ParseCliExecCheckpointStatus(string? value)
    {
        return Enum.TryParse<CliExecCheckpointStatus>(value, true, out var parsed)
            ? parsed
            : CliExecCheckpointStatus.Created;
    }
}
