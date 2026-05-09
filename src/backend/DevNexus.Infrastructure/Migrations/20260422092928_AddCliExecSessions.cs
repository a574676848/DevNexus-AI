using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevNexus.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCliExecSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CliExecSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChatSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SessionKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExecStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SessionMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Command = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    WorkingDirectory = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RuntimeHost = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastActivityAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WaitingForInput = table.Column<bool>(type: "boolean", nullable: false),
                    WaitingForInputSince = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExitCode = table.Column<int>(type: "integer", nullable: true),
                    TerminationReason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CliExecSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CliExecSessions_ChatSessionId_IsActive",
                table: "CliExecSessions",
                columns: new[] { "ChatSessionId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_CliExecSessions_SessionKey",
                table: "CliExecSessions",
                column: "SessionKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CliExecSessions_UserId_LastActivityAt",
                table: "CliExecSessions",
                columns: new[] { "UserId", "LastActivityAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CliExecSessions");
        }
    }
}
