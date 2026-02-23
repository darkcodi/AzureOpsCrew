using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_02_23_10_00_00, "Add Chats and Messages tables")]
public class M007_AddChatTables : Migration
{
    public override void Up()
    {
        Create.Table("Chats")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey()
            .WithColumn("CreatedAt").AsDateTime().NotNullable();

        Create.Table("Messages")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey()
            .WithColumn("ChatId").AsGuid().NotNullable()
            .WithColumn("AuthorName").AsString(256).NotNullable()
            .WithColumn("Text").AsString().NotNullable()
            .WithColumn("PostedAt").AsDateTime().NotNullable()
            .ForeignKey("FK_Messages_Chats_ChatId", "Chats", "Id");

        Create.Index("IX_Messages_PostedAt").OnTable("Messages").OnColumn("PostedAt");
        Create.Index("IX_Messages_ChatId").OnTable("Messages").OnColumn("ChatId");
    }

    public override void Down()
    {
        Delete.Table("Messages");
        Delete.Table("Chats");
    }
}
