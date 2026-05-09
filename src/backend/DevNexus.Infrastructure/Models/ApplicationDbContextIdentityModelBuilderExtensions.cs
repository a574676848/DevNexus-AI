using Microsoft.EntityFrameworkCore;

namespace DevNexus.Infrastructure.Models;

internal static class ApplicationDbContextIdentityModelBuilderExtensions
{
    internal static void ConfigureIdentityModels(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InfrastructureUser>()
            .HasIndex(u => u.IsEnabled);

        modelBuilder.Entity<RefreshToken>()
            .HasOne<InfrastructureUser>()
            .WithMany()
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RefreshToken>()
            .HasIndex(rt => rt.TokenHash)
            .IsUnique();

        modelBuilder.Entity<RefreshToken>()
            .HasIndex(rt => new { rt.UserId, rt.IsRevoked });
    }

    internal static void ConfigureUserOwnedModels(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ContextSwarmSession>(entity =>
        {
            entity.HasOne<InfrastructureUser>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserFact>()
            .HasOne<InfrastructureUser>()
            .WithMany()
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserIntegration>(entity =>
        {
            entity.HasOne<InfrastructureUser>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
