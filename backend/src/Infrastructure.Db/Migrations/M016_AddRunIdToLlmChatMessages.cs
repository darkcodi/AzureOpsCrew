using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_02_25_10_01_00, "Add RunId to LlmChatMessages")]
public class M016_AddRunIdToLlmChatMessages : Migration
{
    public override void Up()
    {
        Alter.Table("LlmChatMessages")
            .AddColumn("RunId").AsString(200).NotNullable().WithDefaultValue(string.Empty);

        Create.Index("IX_LlmChatMessages_RunId").OnTable("LlmChatMessages").OnColumn("RunId");
    }

    public override void Down()
    {
        Delete.Index("IX_LlmChatMessages_RunId").OnTable("LlmChatMessages");
        Delete.Column("RunId").FromTable("LlmChatMessages");
    }
}
