using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_03_04_12_00_00, "Increase Messages.Text column size to max")]
public class M025_IncreaseMessageTextSize : Migration
{
    public override void Up()
    {
        Alter.Table("Messages")
            .AlterColumn("Text")
            .AsString(int.MaxValue) // Maps to nvarchar(max)
            .NotNullable();
    }

    public override void Down()
    {
        Alter.Table("Messages")
            .AlterColumn("Text")
            .AsString()
            .NotNullable();
    }
}
