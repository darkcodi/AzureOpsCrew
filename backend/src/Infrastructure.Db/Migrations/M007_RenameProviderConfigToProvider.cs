using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_02_16_23_30_47, "Rename ProviderConfig to Provider")]
public class M007_RenameProviderConfigToProvider : Migration
{
    public override void Up()
    {
        Rename.Table("ProviderConfig").To("Provider");
    }

    public override void Down()
    {
        Rename.Table("Provider").To("ProviderConfig");
    }
}
