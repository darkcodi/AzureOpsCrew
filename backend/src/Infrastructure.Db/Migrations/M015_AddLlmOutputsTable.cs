using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_02_24_15_00_00, "Add LlmOutputs table")]
public class M015_AddLlmOutputsTable : Migration
{
    public override void Up()
    {
        Create.Table("LlmOutputs")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey()
            .WithColumn("RunId").AsString().NotNullable()
            .WithColumn("Text").AsString(int.MaxValue).Nullable()
            .WithColumn("ToolCall").AsString(int.MaxValue).Nullable()
            .WithColumn("InputTokens").AsInt64().Nullable()
            .WithColumn("OutputTokens").AsInt64().Nullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable();

        Create.Index("IX_LlmOutputs_RunId").OnTable("LlmOutputs").OnColumn("RunId");
    }

    public override void Down()
    {
        Delete.Table("LlmOutputs");
    }
}
