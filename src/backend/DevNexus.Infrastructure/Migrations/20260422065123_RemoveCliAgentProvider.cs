using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevNexus.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCliAgentProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CliAgentProviders");

            migrationBuilder.DropColumn(
                name: "CliProviderId",
                table: "TerminalStreams");

            migrationBuilder.AddColumn<int>(
                name: "ConsecutiveAuthFailureCount",
                table: "UserIntegrations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "CooldownUntil",
                table: "UserIntegrations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAuthFailureAt",
                table: "UserIntegrations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastCredentialRefreshAt",
                table: "UserIntegrations",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConsecutiveAuthFailureCount",
                table: "UserIntegrations");

            migrationBuilder.DropColumn(
                name: "CooldownUntil",
                table: "UserIntegrations");

            migrationBuilder.DropColumn(
                name: "LastAuthFailureAt",
                table: "UserIntegrations");

            migrationBuilder.DropColumn(
                name: "LastCredentialRefreshAt",
                table: "UserIntegrations");

            migrationBuilder.AddColumn<Guid>(
                name: "CliProviderId",
                table: "TerminalStreams",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CliAgentProviders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ArgumentsTemplate = table.Column<string>(type: "text", nullable: true),
                    Capabilities = table.Column<string>(type: "jsonb", nullable: false),
                    Command = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExecutionMode = table.Column<int>(type: "integer", nullable: false),
                    HealthStatus = table.Column<int>(type: "integer", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastHealthCheckAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastHealthMessage = table.Column<string>(type: "text", nullable: true),
                    MaxConcurrent = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    RequiredTools = table.Column<string>(type: "jsonb", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CliAgentProviders", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CliAgentProviders_IsEnabled_HealthStatus_Priority",
                table: "CliAgentProviders",
                columns: new[] { "IsEnabled", "HealthStatus", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_CliAgentProviders_Name",
                table: "CliAgentProviders",
                column: "Name",
                unique: true);
        }
    }
}
