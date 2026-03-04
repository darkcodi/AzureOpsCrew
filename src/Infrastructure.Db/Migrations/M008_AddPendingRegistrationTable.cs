using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_02_22_09_00_00, "Add PendingRegistration table for email verification")]
public class M008_AddPendingRegistrationTable : Migration
{
    public override void Up()
    {
        Create.Table("PendingRegistration")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("Email").AsString(320).NotNullable()
            .WithColumn("NormalizedEmail").AsString(320).NotNullable()
            .WithColumn("DisplayName").AsString(120).NotNullable()
            .WithColumn("PasswordHash").AsString(512).NotNullable()
            .WithColumn("VerificationCodeHash").AsString(512).NotNullable()
            .WithColumn("VerificationCodeExpiresAt").AsDateTime().NotNullable()
            .WithColumn("VerificationCodeSentAt").AsDateTime().NotNullable()
            .WithColumn("VerificationAttempts").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("DateCreated").AsDateTime().NotNullable()
            .WithColumn("DateModified").AsDateTime().Nullable();

        Create.Index("IX_PendingRegistration_NormalizedEmail")
            .OnTable("PendingRegistration")
            .OnColumn("NormalizedEmail")
            .Unique();
    }

    public override void Down()
    {
        Delete.Table("PendingRegistration");
    }
}
