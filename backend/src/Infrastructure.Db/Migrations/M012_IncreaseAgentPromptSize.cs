using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_02_24_12_01_00, "Increase Agent Info_Prompt column size")]
public class M012_IncreaseAgentPromptSize : Migration
{
    public override void Up()
    {
        Alter.Table("Agent")
            .AlterColumn("Info_Prompt")
            .AsString(8000)
            .NotNullable();
    }

    public override void Down()
    {
        Alter.Table("Agent")
            .AlterColumn("Info_Prompt")
            .AsString()
            .NotNullable();
    }
}
