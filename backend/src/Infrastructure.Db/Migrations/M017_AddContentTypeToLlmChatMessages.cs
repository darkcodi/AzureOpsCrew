using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_02_25_14_00_00, "Add ContentType to LlmChatMessages")]
public class M017_AddContentTypeToLlmChatMessages : Migration
{
    public override void Up()
    {
        Alter.Table("LlmChatMessages")
            .AddColumn("ContentType").AsString(100).NotNullable().WithDefaultValue(string.Empty);
    }

    public override void Down()
    {
        Delete.Column("ContentType").FromTable("LlmChatMessages");
    }
}
