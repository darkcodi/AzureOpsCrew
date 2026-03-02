using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_03_03_10_00_00, "UnifyParticipantId")]
public class M023_UnifyParticipantId : Migration
{
    public override void Up()
    {
        // Drop ChatMessages table
        Delete.Table("ChatMessages");

        // Drop Chats table
        Delete.Table("Chats");

        // Recreate Chats table with unified ParticipantIds
        Create.Table("Chats")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey()
            .WithColumn("Title").AsString(256).NotNullable()
            .WithColumn("ParticipantIds").AsString().Nullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable()
            .WithColumn("DateModified").AsDateTime().Nullable();

        // Recreate ChatMessages table with unified SenderId
        Create.Table("ChatMessages")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey()
            .WithColumn("ChatId").AsGuid().NotNullable()
            .WithColumn("Content").AsString(30000).NotNullable()
            .WithColumn("SenderId").AsGuid().NotNullable()
            .WithColumn("PostedAt").AsDateTime().NotNullable();

        Create.Index("IX_ChatMessages_PostedAt").OnTable("ChatMessages").OnColumn("PostedAt");
        Create.Index("IX_ChatMessages_ChatId").OnTable("ChatMessages").OnColumn("ChatId");
        Create.Index("IX_ChatMessages_SenderId").OnTable("ChatMessages").OnColumn("SenderId");
    }

    public override void Down()
    {
        // Rollback - drop and recreate with separate user/agent columns
        Delete.Table("ChatMessages");
        Delete.Table("Chats");

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
            .WithColumn("SenderUserId").AsGuid().Nullable()
            .WithColumn("SenderAgentId").AsGuid().Nullable()
            .WithColumn("PostedAt").AsDateTime().NotNullable();

        Create.Index("IX_ChatMessages_PostedAt").OnTable("ChatMessages").OnColumn("PostedAt");
        Create.Index("IX_ChatMessages_ChatId").OnTable("ChatMessages").OnColumn("ChatId");
        Create.Index("IX_ChatMessages_SenderUserId").OnTable("ChatMessages").OnColumn("SenderUserId");
        Create.Index("IX_ChatMessages_SenderAgentId").OnTable("ChatMessages").OnColumn("SenderAgentId");
    }
}
