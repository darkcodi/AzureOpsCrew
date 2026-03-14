using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_03_04_10_00_00, "ChangeDmIdsToGuid")]
public class M023_ChangeDmIdsToGuid : Migration
{
    public override void Up()
    {
        // Drop DirectMessageChannels table
        Delete.Table("DirectMessageChannels");

        // Recreate with Guid ID columns
        Create.Table("DirectMessageChannels")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey()
            .WithColumn("User1Id").AsGuid().Nullable()
            .WithColumn("User2Id").AsGuid().Nullable()
            .WithColumn("Agent1Id").AsGuid().Nullable()
            .WithColumn("Agent2Id").AsGuid().Nullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable();

        // PostgreSQL: Convert Guid columns to uuid type
        IfDatabase("Postgres")
            .Execute.Sql(@"ALTER TABLE ""DirectMessageChannels"" ALTER COLUMN ""Id"" TYPE uuid USING ""Id""::uuid;
                   ALTER TABLE ""DirectMessageChannels"" ALTER COLUMN ""User1Id"" TYPE uuid USING ""User1Id""::uuid;
                   ALTER TABLE ""DirectMessageChannels"" ALTER COLUMN ""User2Id"" TYPE uuid USING ""User2Id""::uuid;
                   ALTER TABLE ""DirectMessageChannels"" ALTER COLUMN ""Agent1Id"" TYPE uuid USING ""Agent1Id""::uuid;
                   ALTER TABLE ""DirectMessageChannels"" ALTER COLUMN ""Agent2Id"" TYPE uuid USING ""Agent2Id""::uuid;");

        // Recreate indexes
        Create.Index("IX_DirectMessageChannels_User1Id").OnTable("DirectMessageChannels").OnColumn("User1Id");
        Create.Index("IX_DirectMessageChannels_User2Id").OnTable("DirectMessageChannels").OnColumn("User2Id");
        Create.Index("IX_DirectMessageChannels_Agent1Id").OnTable("DirectMessageChannels").OnColumn("Agent1Id");
        Create.Index("IX_DirectMessageChannels_Agent2Id").OnTable("DirectMessageChannels").OnColumn("Agent2Id");
    }

    public override void Down()
    {
        // Rollback - drop and recreate with string ID columns
        Delete.Table("DirectMessageChannels");

        Create.Table("DirectMessageChannels")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey()
            .WithColumn("User1Id").AsString().Nullable()
            .WithColumn("User2Id").AsString().Nullable()
            .WithColumn("Agent1Id").AsString().Nullable()
            .WithColumn("Agent2Id").AsString().Nullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable();

        Create.Index("IX_DirectMessageChannels_User1Id").OnTable("DirectMessageChannels").OnColumn("User1Id");
        Create.Index("IX_DirectMessageChannels_User2Id").OnTable("DirectMessageChannels").OnColumn("User2Id");
        Create.Index("IX_DirectMessageChannels_Agent1Id").OnTable("DirectMessageChannels").OnColumn("Agent1Id");
        Create.Index("IX_DirectMessageChannels_Agent2Id").OnTable("DirectMessageChannels").OnColumn("Agent2Id");
    }
}
