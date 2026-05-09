using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using DevNexus.Infrastructure.Models;

#nullable disable

namespace DevNexus.Infrastructure.Migrations;

/// <summary>
/// 移除 ModelPrices 表遗留的 LLM 外键约束。
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260406133000_RemoveLegacyModelPricingForeignKey")]
public partial class RemoveLegacyModelPricingForeignKey : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ModelPrices 已改为通过 ProviderType + ProviderId 关联多种供应商，
        // 旧的固定 LLM 外键会导致 embedding 定价插入失败，这里兼容性删除。
        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM information_schema.table_constraints
                    WHERE constraint_name = 'FK_ModelPrices_LLMProviders_ProviderId'
                      AND table_name = 'ModelPrices'
                ) THEN
                    ALTER TABLE "ModelPrices"
                    DROP CONSTRAINT "FK_ModelPrices_LLMProviders_ProviderId";
                END IF;
            END
            $$;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1
                    FROM information_schema.table_constraints
                    WHERE constraint_name = 'FK_ModelPrices_LLMProviders_ProviderId'
                      AND table_name = 'ModelPrices'
                ) THEN
                    ALTER TABLE "ModelPrices"
                    ADD CONSTRAINT "FK_ModelPrices_LLMProviders_ProviderId"
                    FOREIGN KEY ("ProviderId")
                    REFERENCES "LLMProviders" ("Id")
                    ON DELETE CASCADE;
                END IF;
            END
            $$;
            """);
    }
}
