using DevNexus.Infrastructure.Models;
using DevNexus.Core.Models;
using DevNexus.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DevNexus.Infrastructure.Extensions;

/// <summary>
/// 数据库种子扩展
/// </summary>
public static class DatabaseSeeder
{
    /// <summary>
    /// 默认管理员用户名
    /// </summary>
    public const string DefaultAdminUsername = "admin";
    
    /// <summary>
    /// 默认管理员邮箱
    /// </summary>
    public const string DefaultAdminEmail = "admin@devnexus.local";
    
    /// <summary>
    /// 默认管理员密码（首次部署后应立即修改）
    /// </summary>
    public const string DefaultAdminPassword = "Admin@123456";
    
    /// <summary>
    /// 管理员角色名称
    /// </summary>
    public const string AdminRoleName = RoleNames.Admin;
    
    /// <summary>
    /// 普通用户角色名称
    /// </summary>
    public const string UserRoleName = RoleNames.User;
    
    /// <summary>
    /// 初始化数据库种子数据
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    public static async Task SeedDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<ApplicationDbContext>>();
        
        try
        {
            var userIdentityService = services.GetRequiredService<IUserIdentityService>();
            
            // 创建角色
            await SeedRolesAsync(userIdentityService, logger);
            
            // 创建管理员用户
            await SeedAdminUserAsync(userIdentityService, logger);

            logger.LogInformation("[Database.Seed] Database seeding completed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Database.Seed] An error occurred while seeding the database");
            throw;
        }
    }

    /// <summary>
    /// 创建默认角色
    /// </summary>
    private static async Task SeedRolesAsync(
        IUserIdentityService userIdentityService,
        ILogger logger)
    {
        var roles = new[] { AdminRoleName, UserRoleName };
        
        foreach (var roleName in roles)
        {
            if (!await userIdentityService.RoleExistsAsync(roleName))
            {
                try
                {
                    await userIdentityService.CreateRoleAsync(Guid.NewGuid(), roleName);
                    logger.LogInformation("[Database.Seed] Role created | Role={Role}", roleName);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "[Database.Seed] Failed to create role | Role={Role}",
                        roleName);
                }
            }
        }
    }
    
    /// <summary>
    /// 创建默认管理员用户
    /// </summary>
    private static async Task SeedAdminUserAsync(
        IUserIdentityService userIdentityService,
        ILogger logger)
    {
        // 检查管理员是否已存在
        var existingAdmin = await userIdentityService.FindByUsernameAsync(DefaultAdminUsername);
        
        if (existingAdmin == null)
        {
            // 创建新管理员
            var admin = new UserIdentityModel
            {
                Id = Guid.NewGuid(),
                Username = DefaultAdminUsername,
                Email = DefaultAdminEmail,
                EmailConfirmed = true,
                DisplayName = "系统管理员",
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            
            var createResult = await userIdentityService.CreateAsync(admin, DefaultAdminPassword);
            
            if (createResult.Succeeded)
            {
                // 分配管理员角色
                await userIdentityService.AddToRoleAsync(admin, AdminRoleName);
                
                logger.LogInformation(
                    "[Database.Seed] Admin user created | Username={Username} Email={Email}",
                    DefaultAdminUsername,
                    DefaultAdminEmail);
                
                logger.LogWarning(
                    "[Database.Seed] ⚠️ DEFAULT ADMIN PASSWORD IS: {Password} - PLEASE CHANGE IT IMMEDIATELY!",
                    DefaultAdminPassword);
            }
            else
            {
                logger.LogError(
                    "[Database.Seed] Failed to create admin user | Errors={Errors}",
                    string.Join(", ", createResult.Errors));
            }
        }
        else
        {
            // 确保管理员有正确的角色
            if (!await userIdentityService.IsInRoleAsync(existingAdmin, AdminRoleName))
            {
                await userIdentityService.AddToRoleAsync(existingAdmin, AdminRoleName);
                logger.LogInformation("[Database.Seed] Admin role assigned to existing admin user");
            }
            
            // 确保管理员账户已启用
            if (!existingAdmin.IsEnabled)
            {
                existingAdmin.IsEnabled = true;
                existingAdmin.UpdatedAt = DateTime.UtcNow;
                await userIdentityService.UpdateAsync(existingAdmin);
                logger.LogInformation("[Database.Seed] Admin user re-enabled");
            }
            
            logger.LogInformation("[Database.Seed] Admin user already exists | Username={Username}", DefaultAdminUsername);
        }
    }
}
