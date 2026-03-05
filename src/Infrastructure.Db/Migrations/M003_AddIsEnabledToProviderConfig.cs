using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_02_16_05_26_24, "Add IsEnabled to ProviderConfig")]
public class M003_AddIsEnabledToProviderConfig : Migration
{
    public override void Up()
    {
        Alter.Table("ProviderConfig")
            .AddColumn("IsEnabled").AsBoolean().NotNullable().WithDefaultValue(true);
    }

    public override void Down()
    {
        Delete.Column("IsEnabled").FromTable("ProviderConfig");
    }
}
