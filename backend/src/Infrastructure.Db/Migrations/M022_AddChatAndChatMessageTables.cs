using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_03_01_10_00_00, "AddChatAndChatMessageTables")]
public class M022_AddChatAndChatMessageTables : Migration
{
    public override void Up()
    {
        Create.Table("Chats")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey()
            .WithColumn("Title").AsString(256).NotNullable()
            .WithColumn("ParticipantUserIds").AsString().Nullable()
            .WithColumn("ParticipantAgentIds").AsString().Nullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable()
            .WithColumn("DateModified").AsDateTime().Nullable();

        Create.Table("ChatMessages")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey()
            .WithColumn("ChatId").AsGuid().NotNullable()
            .WithColumn("Content").AsString(30000).NotNullable()
            .WithColumn("SenderUserId").AsInt32().Nullable()
            .WithColumn("SenderAgentId").AsGuid().Nullable()
            .WithColumn("PostedAt").AsDateTime().NotNullable();

        Create.Index("IX_ChatMessages_PostedAt").OnTable("ChatMessages").OnColumn("PostedAt");
        Create.Index("IX_ChatMessages_ChatId").OnTable("ChatMessages").OnColumn("ChatId");
        Create.Index("IX_ChatMessages_SenderUserId").OnTable("ChatMessages").OnColumn("SenderUserId");
        Create.Index("IX_ChatMessages_SenderAgentId").OnTable("ChatMessages").OnColumn("SenderAgentId");
    }

    public override void Down()
    {
        Delete.Table("ChatMessages");
        Delete.Table("Chats");
    }
}
