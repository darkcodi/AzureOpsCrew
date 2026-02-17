using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_02_16_21_38_02, "Add SelectedModels to ProviderConfig")]
public class M005_AddSelectedModelsToProviderConfig : Migration
{
    public override void Up()
    {
        Alter.Table("ProviderConfig")
            .AddColumn("SelectedModels").AsString(4000).Nullable();
    }

    public override void Down()
    {
        Delete.Column("SelectedModels").FromTable("ProviderConfig");
    }
}
