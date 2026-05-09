using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using DevNexus.Infrastructure.Models;

#nullable disable

namespace DevNexus.Infrastructure.Migrations;

/// <summary>
/// 新增 QueuedChatMessages 表，用于支持长作业场景的消息排队等待机制。
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260408000000_AddQueuedChatMessages")]
public partial class AddQueuedChatMessages : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE IF NOT EXISTS "QueuedChatMessages" (
                "Id" uuid NOT NULL,
                "ChatSessionId" uuid NOT NULL,
                "UserId" uuid NOT NULL,
                "ParentMessageId" uuid NULL,
                "Content" character varying(8000) NOT NULL,
                "MessageType" character varying(32) NOT NULL,
                "SelectedSkillName" character varying(128) NULL,
                "ArtifactIdsJson" jsonb NULL,
                "LLMProviderId" uuid NULL,
                "MetadataJson" jsonb NULL,
                "Status" integer NOT NULL,
                "SequenceNumber" integer NOT NULL,
                "StartedAt" timestamp with time zone NULL,
                "CompletedAt" timestamp with time zone NULL,
                "CancelledAt" timestamp with time zone NULL,
                "FailureReason" character varying(2000) NULL,
                "ActualMessageId" uuid NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL,
                "CreatedBy" uuid NULL,
                "UpdatedBy" uuid NULL,
                "IsDeleted" boolean NOT NULL,
                "DeletedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_QueuedChatMessages" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_QueuedChatMessages_ChatSessions_ChatSessionId"
                    FOREIGN KEY ("ChatSessionId") REFERENCES "ChatSessions" ("Id") ON DELETE CASCADE
            );
            """);

        migrationBuilder.Sql(
            """
            CREATE INDEX IF NOT EXISTS "IX_QueuedChatMessages_SessionId_Status_SequenceNumber"
            ON "QueuedChatMessages" ("ChatSessionId", "Status", "SequenceNumber");

            CREATE INDEX IF NOT EXISTS "IX_QueuedChatMessages_UserId_SessionId"
            ON "QueuedChatMessages" ("UserId", "ChatSessionId");

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_QueuedChatMessages_SessionId_SequenceNumber"
            ON "QueuedChatMessages" ("ChatSessionId", "SequenceNumber");
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS \"QueuedChatMessages\";");
    }
}
