using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using DevNexus.Infrastructure.Models;

#nullable disable

namespace DevNexus.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260406110500_AddProviderTypeToModelPricingAndAudit")]
    public partial class AddProviderTypeToModelPricingAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "ModelPrices"
                ADD COLUMN IF NOT EXISTS "ProviderType" character varying(32) NOT NULL DEFAULT 'llm';
                """);

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.tables
                        WHERE table_schema = 'public'
                          AND table_name = 'ModelInvocationAudits'
                    ) THEN
                        ALTER TABLE "ModelInvocationAudits"
                        ADD COLUMN IF NOT EXISTS "ProviderType" character varying(32) NOT NULL DEFAULT 'llm';

                        UPDATE "ModelInvocationAudits"
                        SET "ProviderType" = 'embedding'
                        WHERE "InvocationKind" = 'embedding';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS "IX_ModelPrices_ProviderId";
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_ModelPrices_ProviderType_ProviderId"
                ON "ModelPrices" ("ProviderType", "ProviderId");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS "IX_ModelPrices_ProviderType_ProviderId";
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_ModelPrices_ProviderId"
                ON "ModelPrices" ("ProviderId");
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "ModelPrices"
                DROP COLUMN IF EXISTS "ProviderType";
                """);

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.tables
                        WHERE table_schema = 'public'
                          AND table_name = 'ModelInvocationAudits'
                    ) THEN
                        ALTER TABLE "ModelInvocationAudits"
                        DROP COLUMN IF EXISTS "ProviderType";
                    END IF;
                END
                $$;
                """);
        }
    }
}
