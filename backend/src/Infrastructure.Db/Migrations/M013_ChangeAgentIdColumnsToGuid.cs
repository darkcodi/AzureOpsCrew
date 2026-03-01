using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_02_24_12_02_00, "Change Agent Id and ProviderId to Guid columns")]
public class M013_ChangeAgentIdColumnsToGuid : Migration
{
    public override void Up()
    {
        Delete.PrimaryKey("PK_Agent")
            .FromTable("Agent");

        Alter.Table("Agent")
            .AlterColumn("Id")
            .AsGuid()
            .NotNullable();

        Execute.Sql("ALTER TABLE [Agent] ADD CONSTRAINT PK_Agent PRIMARY KEY ([Id])");

        // Need to drop the default constraint before altering the column
        Delete.DefaultConstraint()
            .OnTable("Agent")
            .OnColumn("ProviderId");

        Alter.Table("Agent")
            .AlterColumn("ProviderId")
            .AsGuid()
            .NotNullable()
            .WithDefaultValue(new System.Guid("00000000-0000-0000-0000-000000000000"));
    }

    public override void Down()
    {
        Delete.PrimaryKey("PK_Agent")
            .FromTable("Agent");

        Alter.Table("Agent")
            .AlterColumn("Id")
            .AsString(100)
            .NotNullable();

        Execute.Sql("ALTER TABLE [Agent] ADD CONSTRAINT PK_Agent PRIMARY KEY ([Id])");

        Alter.Table("Agent")
            .AlterColumn("ProviderId")
            .AsString(100)
            .NotNullable()
            .WithDefaultValue("00000000-0000-0000-0000-000000000000");
    }
}
