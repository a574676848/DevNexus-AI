using DevNexus.Infrastructure.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevNexus.Infrastructure.Migrations;

/// <summary>
/// 修复文件任务表的历史结构漂移，补齐缺失列与索引。
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260418164000_RepairFileTaskSchemaDrift")]
public partial class RepairFileTaskSchemaDrift : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE IF NOT EXISTS "FileTasks" (
                "Id" uuid NOT NULL,
                "SessionId" uuid NULL,
                "TaskType" character varying(128) NOT NULL,
                "InputAssetIds" jsonb NOT NULL DEFAULT '[]'::jsonb,
                "TemplateAssetIds" jsonb NOT NULL DEFAULT '[]'::jsonb,
                "OutputAssetIds" jsonb NOT NULL DEFAULT '[]'::jsonb,
                "TaskDirectoryPath" character varying(1024) NULL,
                "Instructions" text NULL,
                "Status" integer NOT NULL DEFAULT 0,
                "Stage" integer NOT NULL DEFAULT 0,
                "StageSummary" character varying(1024) NULL,
                "ErrorSummary" character varying(2048) NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL,
                "CreatedBy" uuid NULL,
                "UpdatedBy" uuid NULL,
                "IsDeleted" boolean NOT NULL DEFAULT FALSE,
                "DeletedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_FileTasks" PRIMARY KEY ("Id")
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
                      AND table_name = 'FileTasks'
                ) THEN
                    ALTER TABLE "FileTasks"
                        ADD COLUMN IF NOT EXISTS "SessionId" uuid,
                        ADD COLUMN IF NOT EXISTS "TaskType" character varying(128) NOT NULL DEFAULT '',
                        ADD COLUMN IF NOT EXISTS "InputAssetIds" jsonb NOT NULL DEFAULT '[]'::jsonb,
                        ADD COLUMN IF NOT EXISTS "TemplateAssetIds" jsonb NOT NULL DEFAULT '[]'::jsonb,
                        ADD COLUMN IF NOT EXISTS "OutputAssetIds" jsonb NOT NULL DEFAULT '[]'::jsonb,
                        ADD COLUMN IF NOT EXISTS "TaskDirectoryPath" character varying(1024),
                        ADD COLUMN IF NOT EXISTS "Instructions" text,
                        ADD COLUMN IF NOT EXISTS "Status" integer NOT NULL DEFAULT 0,
                        ADD COLUMN IF NOT EXISTS "Stage" integer NOT NULL DEFAULT 0,
                        ADD COLUMN IF NOT EXISTS "StageSummary" character varying(1024),
                        ADD COLUMN IF NOT EXISTS "ErrorSummary" character varying(2048),
                        ADD COLUMN IF NOT EXISTS "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
                        ADD COLUMN IF NOT EXISTS "UpdatedAt" timestamp with time zone NOT NULL DEFAULT now(),
                        ADD COLUMN IF NOT EXISTS "CreatedBy" uuid,
                        ADD COLUMN IF NOT EXISTS "UpdatedBy" uuid,
                        ADD COLUMN IF NOT EXISTS "IsDeleted" boolean NOT NULL DEFAULT FALSE,
                        ADD COLUMN IF NOT EXISTS "DeletedAt" timestamp with time zone;
                END IF;
            END
            $$;
            """);

        migrationBuilder.Sql(
            """
            CREATE INDEX IF NOT EXISTS "IX_FileTasks_SessionId"
            ON "FileTasks" ("SessionId");
            """);

        migrationBuilder.Sql(
            """
            CREATE INDEX IF NOT EXISTS "IX_FileTasks_CreatedBy"
            ON "FileTasks" ("CreatedBy");
            """);

        migrationBuilder.Sql(
            """
            CREATE INDEX IF NOT EXISTS "IX_FileTasks_CreatedBy_Status_UpdatedAt"
            ON "FileTasks" ("CreatedBy", "Status", "UpdatedAt");
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // 该迁移用于修复历史环境的结构漂移，避免在回滚时删除真实业务数据。
    }
}