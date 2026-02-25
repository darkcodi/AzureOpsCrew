using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_02_25_10_00_00, "Add LlmChatMessages table")]
public class M015_AddLlmChatMessagesTable : Migration
{
    public override void Up()
    {
        Create.Table("LlmChatMessages")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey()
            .WithColumn("AgentId").AsGuid().NotNullable()
            .WithColumn("Role").AsString(50).NotNullable()
            .WithColumn("AuthorName").AsString(256).Nullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable()
            .WithColumn("ContentJson").AsString(int.MaxValue).NotNullable();

        Create.Index("IX_LlmChatMessages_AgentId").OnTable("LlmChatMessages").OnColumn("AgentId");
        Create.Index("IX_LlmChatMessages_CreatedAt").OnTable("LlmChatMessages").OnColumn("CreatedAt");
    }

    public override void Down()
    {
        Delete.Index("IX_LlmChatMessages_CreatedAt").OnTable("LlmChatMessages");
        Delete.Index("IX_LlmChatMessages_AgentId").OnTable("LlmChatMessages");
        Delete.Table("LlmChatMessages");
    }
}
