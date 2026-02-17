using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_02_16_17_06_28, "Add ModelsCount to ProviderConfig")]
public class M004_AddModelsCountToProviderConfig : Migration
{
    public override void Up()
    {
        Alter.Table("ProviderConfig")
            .AddColumn("ModelsCount").AsInt32().NotNullable().WithDefaultValue(0);
    }

    public override void Down()
    {
        Delete.Column("ModelsCount").FromTable("ProviderConfig");
    }
}
