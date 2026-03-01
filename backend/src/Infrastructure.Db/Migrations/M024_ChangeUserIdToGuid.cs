using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_03_02_10_00_00, "ChangeUserIdToGuid")]
public class M024_ChangeUserIdToGuid : Migration
{
    public override void Up()
    {
        // Drop ChatMessages table (references Users via SenderUserId)
        Delete.Table("ChatMessages");

        // Drop Chats table (ParticipantUserIds contains user IDs)
        Delete.Table("Chats");

        // Drop Users table
        Delete.Table("Users");

        // Recreate Users table with Guid Id
        Create.Table("Users")
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("Email").AsString(320).NotNullable()
            .WithColumn("NormalizedEmail").AsString(320).NotNullable()
            .WithColumn("PasswordHash").AsString(512).NotNullable()
            .WithColumn("DisplayName").AsString(120).NotNullable()
            .WithColumn("IsActive").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("DateCreated").AsDateTime().NotNullable()
            .WithColumn("DateModified").AsDateTime().Nullable()
            .WithColumn("LastLoginAt").AsDateTime().Nullable();

        Create.Index("IX_Users_NormalizedEmail")
            .OnTable("Users")
            .OnColumn("NormalizedEmail")
            .Unique();

        // Recreate Chats table
        Create.Table("Chats")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey()
            .WithColumn("Title").AsString(256).NotNullable()
            .WithColumn("ParticipantUserIds").AsString().Nullable()
            .WithColumn("ParticipantAgentIds").AsString().Nullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable()
            .WithColumn("DateModified").AsDateTime().Nullable();

        // Recreate ChatMessages table with SenderUserId as Guid
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

    public override void Down()
    {
        // Rollback - drop and recreate with int Id
        Delete.Table("ChatMessages");
        Delete.Table("Chats");
        Delete.Table("Users");

        Create.Table("Users")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("Email").AsString(320).NotNullable()
            .WithColumn("NormalizedEmail").AsString(320).NotNullable()
            .WithColumn("PasswordHash").AsString(512).NotNullable()
            .WithColumn("DisplayName").AsString(120).NotNullable()
            .WithColumn("IsActive").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("DateCreated").AsDateTime().NotNullable()
            .WithColumn("DateModified").AsDateTime().Nullable()
            .WithColumn("LastLoginAt").AsDateTime().Nullable();

        Create.Index("IX_Users_NormalizedEmail")
            .OnTable("Users")
            .OnColumn("NormalizedEmail")
            .Unique();

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
}
