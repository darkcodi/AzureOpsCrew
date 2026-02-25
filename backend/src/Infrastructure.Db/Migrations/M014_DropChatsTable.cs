using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_02_24_14_00_00, "Drop Chats table and remove ChatId from Messages")]
public class M014_DropChatsTable : Migration
{
    public override void Up()
    {
        Delete.Index("IX_Messages_ChatId").OnTable("Messages");
        Delete.Column("ChatId").FromTable("Messages");
        Delete.Table("Chats");
    }

    public override void Down()
    {
        Create.Table("Chats")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey()
            .WithColumn("CreatedAt").AsDateTime().NotNullable();

        Alter.Table("Messages")
            .AddColumn("ChatId").AsGuid().Nullable();

        Create.Index("IX_Messages_ChatId").OnTable("Messages").OnColumn("ChatId");
    }
}
