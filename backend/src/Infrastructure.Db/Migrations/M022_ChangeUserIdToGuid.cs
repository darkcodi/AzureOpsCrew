using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_03_02_10_00_00, "ChangeUserIdToGuid")]
public class M022_ChangeUserIdToGuid : Migration
{
    public override void Up()
    {
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
    }

    public override void Down()
    {
        // Rollback - drop and recreate with int Id
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
    }
}
