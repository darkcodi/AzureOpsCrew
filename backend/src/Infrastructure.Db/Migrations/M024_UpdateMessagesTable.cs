using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_03_04_11_00_00, "Update Messages table schema")]
public class M024_UpdateMessagesTable : Migration
{
    public override void Up()
    {
        // Drop indexes for columns that will be modified
        Delete.Index("IX_Messages_ChatId").OnTable("Messages");
        Delete.Index("IX_Messages_AgentId").OnTable("Messages");
        Delete.Index("IX_Messages_UserId").OnTable("Messages");

        // Remove columns
        Delete.Column("ChatId").FromTable("Messages");
        Delete.Column("AuthorName").FromTable("Messages");
        Delete.Column("AgentId").FromTable("Messages");
        Delete.Column("UserId").FromTable("Messages");

        // Add back AgentId and UserId as Guid
        Alter.Table("Messages")
            .AddColumn("AgentId").AsGuid().Nullable()
            .AddColumn("UserId").AsGuid().Nullable();

        // Recreate indexes for AgentId and UserId
        Create.Index("IX_Messages_AgentId").OnTable("Messages").OnColumn("AgentId");
        Create.Index("IX_Messages_UserId").OnTable("Messages").OnColumn("UserId");
    }

    public override void Down()
    {
        // Rollback - drop indexes
        Delete.Index("IX_Messages_UserId").OnTable("Messages");
        Delete.Index("IX_Messages_AgentId").OnTable("Messages");

        // Remove Guid columns
        Delete.Column("UserId").FromTable("Messages");
        Delete.Column("AgentId").FromTable("Messages");

        // Add back columns as they were
        Alter.Table("Messages")
            .AddColumn("ChatId").AsGuid().NotNullable()
            .AddColumn("AuthorName").AsString(256).NotNullable()
            .AddColumn("AgentId").AsString().Nullable()
            .AddColumn("UserId").AsString().Nullable();

        // Recreate indexes
        Create.Index("IX_Messages_ChatId").OnTable("Messages").OnColumn("ChatId");
        Create.Index("IX_Messages_AgentId").OnTable("Messages").OnColumn("AgentId");
        Create.Index("IX_Messages_UserId").OnTable("Messages").OnColumn("UserId");
    }
}
