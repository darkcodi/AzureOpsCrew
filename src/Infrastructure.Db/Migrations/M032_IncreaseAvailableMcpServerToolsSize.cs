using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_03_08_12_15_00, "Increase Info_AvailableMcpServerTools column size to max")]
public class M032_IncreaseAvailableMcpServerToolsSize : Migration
{
    public override void Up()
    {
        // Drop default constraint if it exists (works for both SQL Server and PostgreSQL)
        Delete.DefaultConstraint()
            .OnTable("Agents")
            .OnColumn("Info_AvailableMcpServerTools");

        Alter.Table("Agents")
            .AlterColumn("Info_AvailableMcpServerTools")
            .AsString(int.MaxValue) // Maps to nvarchar(max) / text
            .NotNullable();
    }

    public override void Down()
    {
        Alter.Table("Agents")
            .AlterColumn("Info_AvailableMcpServerTools")
            .AsString()
            .NotNullable()
            .WithDefaultValue(string.Empty);
    }
}
