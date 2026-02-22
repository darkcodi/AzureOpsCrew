using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_02_22_18_00_00, "Add AppUserExternalIdentity table for external IdP mappings")]
public class M009_AddUserExternalIdentityTable : Migration
{
    public override void Up()
    {
        Create.Table("AppUserExternalIdentity")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("UserId").AsInt32().NotNullable()
            .WithColumn("Provider").AsString(50).NotNullable()
            .WithColumn("ProviderSubject").AsString(256).NotNullable()
            .WithColumn("Email").AsString(320).Nullable()
            .WithColumn("DateCreated").AsDateTime().NotNullable()
            .WithColumn("DateModified").AsDateTime().Nullable();

        // FluentMigrator SQLite generator does not support standalone foreign key expressions.
        // Skip FK creation for SQLite to keep local development migrations working.
        IfDatabase("SqlServer").Create.ForeignKey("FK_AppUserExternalIdentity_AppUser_UserId")
            .FromTable("AppUserExternalIdentity").ForeignColumn("UserId")
            .ToTable("AppUser").PrimaryColumn("Id");

        Create.Index("IX_AppUserExternalIdentity_Provider_Subject")
            .OnTable("AppUserExternalIdentity")
            .OnColumn("Provider").Ascending()
            .OnColumn("ProviderSubject").Ascending()
            .WithOptions().Unique();

        Create.Index("IX_AppUserExternalIdentity_UserId")
            .OnTable("AppUserExternalIdentity")
            .OnColumn("UserId");
    }

    public override void Down()
    {
        Delete.Index("IX_AppUserExternalIdentity_UserId").OnTable("AppUserExternalIdentity");
        Delete.Index("IX_AppUserExternalIdentity_Provider_Subject").OnTable("AppUserExternalIdentity");
        IfDatabase("SqlServer").Delete.ForeignKey("FK_AppUserExternalIdentity_AppUser_UserId").OnTable("AppUserExternalIdentity");
        Delete.Table("AppUserExternalIdentity");
    }
}
