using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_03_07_20_00_00, "Replace Info_AvailableTools with Info_AvailableMcpServerTools on Agents table")]
public class M031_ReplaceAvailableToolsWithMcpServerTools : Migration
{
    public override void Up()
    {
        Delete.Column("Info_AvailableTools").FromTable("Agents");

        Alter.Table("Agents")
            .AddColumn("Info_AvailableMcpServerTools")
            .AsString()
            .NotNullable()
            .WithDefaultValue("[]");
    }

    public override void Down()
    {
        Delete.Column("Info_AvailableMcpServerTools").FromTable("Agents");

        Alter.Table("Agents")
            .AddColumn("Info_AvailableTools")
            .AsString()
            .NotNullable()
            .WithDefaultValue("");
    }
}
