using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_02_24_16_00_00, "Alter LlmOutputs column sizes")]
public class M016_AlterLlmOutputColumnSizes : Migration
{
    public override void Up()
    {
        Alter.Table("LlmOutputs")
            .AlterColumn("Text").AsString(int.MaxValue).Nullable()
            .AlterColumn("ToolCall").AsString(int.MaxValue).Nullable();
    }

    public override void Down()
    {
        Alter.Table("LlmOutputs")
            .AlterColumn("Text").AsString().Nullable()
            .AlterColumn("ToolCall").AsString().Nullable();
    }
}
