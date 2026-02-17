using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_02_16_05_05_54, "Add ProviderConfig table")]
public class M003_AddProviderConfigTable : Migration
{
    public override void Up()
    {
        Create.Table("ProviderConfig")
            .WithColumn("Id").AsString().NotNullable().PrimaryKey()
            .WithColumn("ClientId").AsInt32().NotNullable()
            .WithColumn("Name").AsString(200).NotNullable()
            .WithColumn("ProviderType").AsString().NotNullable()
            .WithColumn("ApiKey").AsString(500).NotNullable()
            .WithColumn("ApiEndpoint").AsString(500).Nullable()
            .WithColumn("DefaultModel").AsString(200).Nullable()
            .WithColumn("DateCreated").AsDateTime().NotNullable()
            .WithColumn("DateModified").AsDateTime().Nullable();

        Create.Index("IX_ProviderConfig_ClientId").OnTable("ProviderConfig").OnColumn("ClientId");
    }

    public override void Down()
    {
        Delete.Table("ProviderConfig");
    }
}
