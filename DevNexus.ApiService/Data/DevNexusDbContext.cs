using DevNexus.ApiService.Domain;
using Microsoft.EntityFrameworkCore;

namespace DevNexus.ApiService.Data;

public class DevNexusDbContext : DbContext
{
    public DevNexusDbContext(DbContextOptions<DevNexusDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Config> Configs => Set<Config>();
    public DbSet<ChatHistory> ChatHistories => Set<ChatHistory>();
}
