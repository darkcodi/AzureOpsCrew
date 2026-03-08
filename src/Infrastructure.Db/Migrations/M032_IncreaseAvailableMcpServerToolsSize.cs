using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_03_08_12_15_00, "Increase Info_AvailableMcpServerTools column size to max")]
public class M032_IncreaseAvailableMcpServerToolsSize : Migration
{
    public override void Up()
    {
        // Drop default constraint if it exists
        Execute.Sql(@"
            IF EXISTS (
                SELECT 1 FROM sys.default_constraints
                WHERE name = 'DF_Agents_Info_AvailableMcpServerTools'
                AND parent_object_id = OBJECT_ID('Agents')
            )
            BEGIN
                ALTER TABLE [dbo].[Agents] DROP CONSTRAINT [DF_Agents_Info_AvailableMcpServerTools]
            END
        ");

        Alter.Table("Agents")
            .AlterColumn("Info_AvailableMcpServerTools")
            .AsString(int.MaxValue) // Maps to nvarchar(max)
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
