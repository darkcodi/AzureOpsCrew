using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_03_06_22_45_00, "Add ChatMessageId column to AgentThoughts table")]
public class M030_AddChatMessageIdToAgentThoughts : Migration
{
    public override void Up()
    {
        Alter.Table("AgentThoughts")
            .AddColumn("ChatMessageId")
            .AsGuid()
            .NotNullable()
            .WithDefaultValue(Guid.Empty);

        // PostgreSQL: Convert Guid column to uuid type
        IfDatabase("Postgres")
            .Execute.Sql(@"ALTER TABLE ""AgentThoughts"" ALTER COLUMN ""ChatMessageId"" TYPE uuid USING ""ChatMessageId""::uuid;");

        Create.Index("IX_AgentThoughts_ChatMessageId").OnTable("AgentThoughts").OnColumn("ChatMessageId");
    }

    public override void Down()
    {
        Delete.Index("IX_AgentThoughts_ChatMessageId").OnTable("AgentThoughts");
        Delete.Column("ChatMessageId")
            .FromTable("AgentThoughts");
    }
}
