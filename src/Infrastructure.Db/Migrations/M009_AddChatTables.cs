using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_02_23_10_00_00, "Add Chats and Messages tables")]
public class M009_AddChatTables : Migration
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
            .WithColumn("PostedAt").AsDateTime().NotNullable();

        // PostgreSQL: Convert Guid columns to native uuid type
        IfDatabase("Postgres")
            .Execute.Sql(@"ALTER TABLE ""Messages"" ALTER COLUMN ""Id"" TYPE uuid USING ""Id""::uuid;
                 ALTER TABLE ""Chats"" ALTER COLUMN ""Id"" TYPE uuid USING ""Id""::uuid;
                 ALTER TABLE ""Messages"" ALTER COLUMN ""ChatId"" TYPE uuid USING ""ChatId""::uuid;");

        Create.Index("IX_Messages_PostedAt").OnTable("Messages").OnColumn("PostedAt");
        Create.Index("IX_Messages_ChatId").OnTable("Messages").OnColumn("ChatId");
    }

    public override void Down()
    {
        Delete.Table("Messages");
        Delete.Table("Chats");
    }
}
