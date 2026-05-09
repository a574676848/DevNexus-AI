using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevNexus.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCliRuntimeProcessContracts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TerminalStreamId",
                table: "CliExecSessions",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TerminalStreamId",
                table: "CliExecSessions");
        }
    }
}
