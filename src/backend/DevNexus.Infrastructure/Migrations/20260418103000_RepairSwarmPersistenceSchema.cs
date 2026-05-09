using DevNexus.Infrastructure.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevNexus.Infrastructure.Migrations;

/// <summary>
/// 修复 Swarm 持久化与终端流的历史数据库结构漂移。
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260418103000_RepairSwarmPersistenceSchema")]
public partial class RepairSwarmPersistenceSchema : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE IF NOT EXISTS "ContextSwarmSessions" (
                "Id" uuid NOT NULL,
                "SessionId" text NOT NULL,
                "Title" text NOT NULL,
                "Description" text NOT NULL,
                "Status" integer NOT NULL,
                "StartedAt" timestamp with time zone NOT NULL,
                "CompletedAt" timestamp with time zone NULL,
                "Result" text NULL,
                "UserId" uuid NOT NULL,
                "DomainType" integer NOT NULL,
                "ProviderId" uuid NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL,
                "CreatedBy" uuid NULL,
                "UpdatedBy" uuid NULL,
                "IsDeleted" boolean NOT NULL DEFAULT FALSE,
                "DeletedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_ContextSwarmSessions" PRIMARY KEY ("Id")
            );
            """);

        migrationBuilder.Sql(
            """
            CREATE TABLE IF NOT EXISTS "ContextWorkPackages" (
                "Id" uuid NOT NULL,
                "TaskId" text NOT NULL,
                "Title" text NOT NULL,
                "Description" text NOT NULL,
                "Role" text NOT NULL,
                "ContextType" character varying(64) NOT NULL,
                "ExecutionStrategy" character varying(64) NOT NULL,
                "Status" integer NOT NULL,
                "Dependencies" jsonb NOT NULL DEFAULT '[]'::jsonb,
                "Result" text NULL,
                "FailureReason" character varying(4000) NULL,
                "ExecutorName" character varying(128) NULL,
                "CommandLine" character varying(2048) NULL,
                "WorkingDirectory" character varying(1024) NULL,
                "ExecutionReportArtifactId" uuid NULL,
                "StartedAt" timestamp with time zone NULL,
                "CompletedAt" timestamp with time zone NULL,
                "LogicalUnits" jsonb NOT NULL DEFAULT '[]'::jsonb,
                "InputContracts" jsonb NOT NULL DEFAULT '[]'::jsonb,
                "OutputContracts" jsonb NOT NULL DEFAULT '[]'::jsonb,
                "OwnedFiles" jsonb NOT NULL DEFAULT '[]'::jsonb,
                "OwnedSymbols" jsonb NOT NULL DEFAULT '[]'::jsonb,
                "ContextSwarmSessionId" uuid NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL,
                "CreatedBy" uuid NULL,
                "UpdatedBy" uuid NULL,
                "IsDeleted" boolean NOT NULL DEFAULT FALSE,
                "DeletedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_ContextWorkPackages" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_ContextWorkPackages_ContextSwarmSessions_ContextSwarmSessionId"
                    FOREIGN KEY ("ContextSwarmSessionId")
                    REFERENCES "ContextSwarmSessions" ("Id")
                    ON DELETE CASCADE
            );
            """);

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
                        ADD COLUMN IF NOT EXISTS "ExecutionReportArtifactId" uuid,
                        ADD COLUMN IF NOT EXISTS "LogicalUnits" jsonb NOT NULL DEFAULT '[]'::jsonb,
                        ADD COLUMN IF NOT EXISTS "InputContracts" jsonb NOT NULL DEFAULT '[]'::jsonb,
                        ADD COLUMN IF NOT EXISTS "OutputContracts" jsonb NOT NULL DEFAULT '[]'::jsonb,
                        ADD COLUMN IF NOT EXISTS "OwnedFiles" jsonb NOT NULL DEFAULT '[]'::jsonb,
                        ADD COLUMN IF NOT EXISTS "OwnedSymbols" jsonb NOT NULL DEFAULT '[]'::jsonb;
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

                    ALTER TABLE "TerminalStreams"
                        ALTER COLUMN "MessageId" DROP NOT NULL;

                    ALTER TABLE "TerminalStreams"
                        ALTER COLUMN "Output" TYPE text;
                END IF;
            END
            $$;
            """);

        migrationBuilder.Sql(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_ContextSwarmSessions_SessionId"
            ON "ContextSwarmSessions" ("SessionId");
            """);

        migrationBuilder.Sql(
            """
            CREATE INDEX IF NOT EXISTS "IX_ContextSwarmSessions_UserId"
            ON "ContextSwarmSessions" ("UserId");
            """);

        migrationBuilder.Sql(
            """
            CREATE INDEX IF NOT EXISTS "IX_ContextWorkPackages_TaskId"
            ON "ContextWorkPackages" ("TaskId");
            """);

        migrationBuilder.Sql(
            """
            CREATE INDEX IF NOT EXISTS "IX_ContextWorkPackages_ContextSwarmSessionId"
            ON "ContextWorkPackages" ("ContextSwarmSessionId");
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // 该迁移用于修复历史环境的结构漂移，回滚会带来数据破坏风险，这里保持空实现。
    }
}