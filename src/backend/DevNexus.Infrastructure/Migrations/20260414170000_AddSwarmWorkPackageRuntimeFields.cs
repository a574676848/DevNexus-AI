using DevNexus.Infrastructure.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevNexus.Infrastructure.Migrations;

/// <summary>
/// 为 Swarm 工作包与终端流补充运行态字段。
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260414170000_AddSwarmWorkPackageRuntimeFields")]
public partial class AddSwarmWorkPackageRuntimeFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM information_schema.tables
                    WHERE table_schema = 'public'
                      AND table_name = 'ContextWorkPackages'
                ) THEN
                    ALTER TABLE "ContextWorkPackages"
                        ADD COLUMN IF NOT EXISTS "FailureReason" character varying(4000),
                        ADD COLUMN IF NOT EXISTS "ExecutorName" character varying(128),
                        ADD COLUMN IF NOT EXISTS "CommandLine" character varying(2048),
                        ADD COLUMN IF NOT EXISTS "WorkingDirectory" character varying(1024),
                        ADD COLUMN IF NOT EXISTS "ExecutionReportArtifactId" uuid;
                END IF;
            END
            $$;
            """);

        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM information_schema.tables
                    WHERE table_schema = 'public'
                      AND table_name = 'TerminalStreams'
                ) THEN
                    ALTER TABLE "TerminalStreams"
                        ADD COLUMN IF NOT EXISTS "PackageId" character varying(128);
                END IF;
            END
            $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM information_schema.tables
                    WHERE table_schema = 'public'
                      AND table_name = 'ContextWorkPackages'
                ) THEN
                    ALTER TABLE "ContextWorkPackages"
                        DROP COLUMN IF EXISTS "FailureReason",
                        DROP COLUMN IF EXISTS "ExecutorName",
                        DROP COLUMN IF EXISTS "CommandLine",
                        DROP COLUMN IF EXISTS "WorkingDirectory",
                        DROP COLUMN IF EXISTS "ExecutionReportArtifactId";
                END IF;
            END
            $$;
            """);

        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM information_schema.tables
                    WHERE table_schema = 'public'
                      AND table_name = 'TerminalStreams'
                ) THEN
                    ALTER TABLE "TerminalStreams"
                        DROP COLUMN IF EXISTS "PackageId";
                END IF;
            END
            $$;
            """);
    }
}
