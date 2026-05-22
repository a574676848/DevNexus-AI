using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevNexus.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddModelInvocationToolAuditMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ToolArgumentsValid",
                table: "ModelInvocationAudits",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ToolExitCode",
                table: "ModelInvocationAudits",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ToolFailureReason",
                table: "ModelInvocationAudits",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ToolName",
                table: "ModelInvocationAudits",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ToolRequiresHumanIntervention",
                table: "ModelInvocationAudits",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ToolRetryable",
                table: "ModelInvocationAudits",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ToolSuggestedAction",
                table: "ModelInvocationAudits",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ToolArgumentsValid",
                table: "ModelInvocationAudits");

            migrationBuilder.DropColumn(
                name: "ToolExitCode",
                table: "ModelInvocationAudits");

            migrationBuilder.DropColumn(
                name: "ToolFailureReason",
                table: "ModelInvocationAudits");

            migrationBuilder.DropColumn(
                name: "ToolName",
                table: "ModelInvocationAudits");

            migrationBuilder.DropColumn(
                name: "ToolRequiresHumanIntervention",
                table: "ModelInvocationAudits");

            migrationBuilder.DropColumn(
                name: "ToolRetryable",
                table: "ModelInvocationAudits");

            migrationBuilder.DropColumn(
                name: "ToolSuggestedAction",
                table: "ModelInvocationAudits");
        }
    }
}
