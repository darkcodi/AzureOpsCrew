using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_03_01_11_00_00, "DropMessagesTable")]
public class M023_DropMessagesTable : Migration
{
    public override void Up()
    {
        Delete.Table("Messages");
    }

    public override void Down()
    {
        Create.Table("Messages")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey()
            .WithColumn("ChatId").AsGuid().NotNullable()
            .WithColumn("AuthorName").AsString(256).NotNullable()
            .WithColumn("Text").AsString().NotNullable()
            .WithColumn("PostedAt").AsDateTime().NotNullable()
            .WithColumn("AgentId").AsString().Nullable()
            .WithColumn("UserId").AsString().Nullable()
            .WithColumn("ChannelId").AsGuid().Nullable()
            .WithColumn("DmId").AsGuid().Nullable();

        Create.Index("IX_Messages_PostedAt").OnTable("Messages").OnColumn("PostedAt");
        Create.Index("IX_Messages_ChatId").OnTable("Messages").OnColumn("ChatId");
        Create.Index("IX_Messages_AgentId").OnTable("Messages").OnColumn("AgentId");
        Create.Index("IX_Messages_UserId").OnTable("Messages").OnColumn("UserId");
        Create.Index("IX_Messages_ChannelId").OnTable("Messages").OnColumn("ChannelId");
        Create.Index("IX_Messages_DmId").OnTable("Messages").OnColumn("DmId");
    }
}
