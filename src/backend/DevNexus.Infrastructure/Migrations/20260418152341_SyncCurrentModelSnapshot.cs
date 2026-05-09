using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevNexus.Infrastructure.Migrations;

/// <summary>
/// 同步当前模型快照，避免历史缺失 snapshot 导致后续迁移持续漂移。
/// </summary>
public partial class SyncCurrentModelSnapshot : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 该迁移仅用于让 EF 记录当前完整模型 snapshot，不执行任何额外 DDL。
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // 空实现：该迁移不承载实际数据库结构变更。
    }
}
