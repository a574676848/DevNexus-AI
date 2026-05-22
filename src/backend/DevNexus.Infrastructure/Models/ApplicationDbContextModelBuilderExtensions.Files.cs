using Microsoft.EntityFrameworkCore;

namespace DevNexus.Infrastructure.Models;

internal static partial class ApplicationDbContextModelBuilderFileExtensions
{
    internal static void ConfigureFileModels(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FileAsset>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SessionId);
            entity.HasIndex(e => e.CreatedBy);
            entity.HasIndex(e => new { e.CreatedBy, e.SourceType, e.Status });
            entity.Property(e => e.OriginalFileName).HasMaxLength(512);
            entity.Property(e => e.Extension).HasMaxLength(32);
            entity.Property(e => e.ContentType).HasMaxLength(256);
            entity.Property(e => e.StorageProvider).HasMaxLength(64);
            entity.Property(e => e.FileUrl).HasMaxLength(2048);
            entity.Property(e => e.ObjectKey).HasMaxLength(1024);
            entity.Property(e => e.SourceType).HasMaxLength(128);
        });

        modelBuilder.Entity<FileVersion>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.FileAssetId);
            entity.HasIndex(e => e.GeneratedByTaskId);
            entity.Property(e => e.ObjectKey).HasMaxLength(1024);
            entity.Property(e => e.FileUrl).HasMaxLength(2048);
            entity.Property(e => e.ChangeSummary).HasMaxLength(1024);
        });

        modelBuilder.Entity<FileTask>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SessionId);
            entity.HasIndex(e => e.CreatedBy);
            entity.HasIndex(e => new { e.CreatedBy, e.Status, e.UpdatedAt });
            entity.Property(e => e.TaskType).HasMaxLength(128);
            entity.Property(e => e.TaskDirectoryPath).HasMaxLength(1024);
            entity.Property(e => e.StageSummary).HasMaxLength(1024);
            entity.Property(e => e.ErrorSummary).HasMaxLength(2048);
        });

        modelBuilder.Entity<UploadSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.FileAssetId);
            entity.HasIndex(e => e.SessionId);
            entity.HasIndex(e => new { e.CreatedBy, e.Status, e.ExpiresAt });
            entity.Property(e => e.FileName).HasMaxLength(512);
            entity.Property(e => e.ContentType).HasMaxLength(256);
            entity.Property(e => e.FileUrl).HasMaxLength(2048);
            entity.Property(e => e.ObjectKey).HasMaxLength(1024);
            entity.Property(e => e.UploadUrl).HasMaxLength(4096);
            entity.Property(e => e.UploadMethod).HasMaxLength(32);
        });
    }
}
