using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_02_21_12_00_00, "Add AppUser table")]
public class M007_AddAppUserTable : Migration
{
    public override void Up()
    {
        Create.Table("AppUser")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("Email").AsString(320).NotNullable()
            .WithColumn("NormalizedEmail").AsString(320).NotNullable()
            .WithColumn("PasswordHash").AsString(512).NotNullable()
            .WithColumn("DisplayName").AsString(120).NotNullable()
            .WithColumn("IsActive").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("DateCreated").AsDateTime().NotNullable()
            .WithColumn("DateModified").AsDateTime().Nullable()
            .WithColumn("LastLoginAt").AsDateTime().Nullable();

        Create.Index("IX_AppUser_NormalizedEmail")
            .OnTable("AppUser")
            .OnColumn("NormalizedEmail")
            .Unique();
    }

    public override void Down()
    {
        Delete.Table("AppUser");
    }
}
