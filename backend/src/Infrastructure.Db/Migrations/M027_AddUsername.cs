using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_03_04_14_00_00, "Add Username to Users and PendingRegistrations, rename Name to Username in Agents")]
public class M027_AddUsername : Migration
{
    public override void Up()
    {
        // Users table: Rename DisplayName to Username and add NormalizedUsername
        Rename.Column("DisplayName").OnTable("Users").To("Username");
        Alter.Table("Users")
            .AlterColumn("Username")
            .AsString(30)
            .NotNullable();

        Create.Column("NormalizedUsername")
            .OnTable("Users")
            .AsString(30)
            .NotNullable()
            .SetExistingRowsTo(string.Empty);

        Create.Index("IX_Users_NormalizedUsername")
            .OnTable("Users")
            .OnColumn("NormalizedUsername")
            .Unique();

        // PendingRegistrations table: Rename DisplayName to Username and add NormalizedUsername
        Rename.Column("DisplayName").OnTable("PendingRegistrations").To("Username");
        Alter.Table("PendingRegistrations")
            .AlterColumn("Username")
            .AsString(30)
            .NotNullable();

        Create.Column("NormalizedUsername")
            .OnTable("PendingRegistrations")
            .AsString(30)
            .NotNullable()
            .SetExistingRowsTo(string.Empty);

        Create.Index("IX_PendingRegistrations_NormalizedUsername")
            .OnTable("PendingRegistrations")
            .OnColumn("NormalizedUsername")
            .Unique();

        // Agents table: Rename Info_Name to Info_Username
        Rename.Column("Info_Name").OnTable("Agents").To("Info_Username");
        Alter.Table("Agents")
            .AlterColumn("Info_Username")
            .AsString(30)
            .NotNullable();

        Create.Index("IX_Agents_Info_Username")
            .OnTable("Agents")
            .OnColumn("Info_Username")
            .Unique();
    }

    public override void Down()
    {
        // Agents table
        Delete.Index("IX_Agents_Info_Username")
            .OnTable("Agents");
        Rename.Column("Info_Username").OnTable("Agents").To("Info_Name");

        // PendingRegistrations table
        Delete.Index("IX_PendingRegistrations_NormalizedUsername")
            .OnTable("PendingRegistrations");
        Delete.Column("NormalizedUsername")
            .FromTable("PendingRegistrations");

        Rename.Column("Username").OnTable("PendingRegistrations").To("DisplayName");
        Alter.Table("PendingRegistrations")
            .AlterColumn("DisplayName")
            .AsString(120)
            .NotNullable();

        // Users table
        Delete.Index("IX_Users_NormalizedUsername")
            .OnTable("Users");
        Delete.Column("NormalizedUsername")
            .FromTable("Users");

        Rename.Column("Username").OnTable("Users").To("DisplayName");
        Alter.Table("Users")
            .AlterColumn("DisplayName")
            .AsString(120)
            .NotNullable();
    }
}
