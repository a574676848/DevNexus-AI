using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevNexus.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTerminalOutputArchival : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArchivedOutputPath",
                table: "TerminalStreams",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasArchivedOutput",
                table: "TerminalStreams",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "OutputChunkCount",
                table: "TerminalStreams",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OutputLength",
                table: "TerminalStreams",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OutputLineCount",
                table: "TerminalStreams",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "WatchSummary",
                table: "TerminalStreams",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArchivedOutputPath",
                table: "TerminalStreams");

            migrationBuilder.DropColumn(
                name: "HasArchivedOutput",
                table: "TerminalStreams");

            migrationBuilder.DropColumn(
                name: "OutputChunkCount",
                table: "TerminalStreams");

            migrationBuilder.DropColumn(
                name: "OutputLength",
                table: "TerminalStreams");

            migrationBuilder.DropColumn(
                name: "OutputLineCount",
                table: "TerminalStreams");

            migrationBuilder.DropColumn(
                name: "WatchSummary",
                table: "TerminalStreams");
        }
    }
}
