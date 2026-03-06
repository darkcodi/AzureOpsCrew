using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_03_06_09_30_00, "Add MCP server configuration tables")]
public class M028_AddMcpServerConfigurations : Migration
{
    private const string ConfigurationsTableName = "McpServerConfigurations";
    private const string ToolsTableName = "McpServerConfigurationTools";
    private const string ToolsForeignKeyName = "FK_McpServerConfigurationTools_McpServerConfigurations";
    private const string ToolsConfigurationIdIndexName = "IX_McpServerConfigurationTools_McpServerConfigurationId";

    public override void Up()
    {
        Create.Table(ConfigurationsTableName)
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey()
            .WithColumn("Name").AsString(200).NotNullable()
            .WithColumn("Description").AsString(4000).Nullable()
            .WithColumn("Url").AsString(1000).NotNullable()
            .WithColumn("IsEnabled").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("ToolsSyncedAt").AsDateTime().Nullable()
            .WithColumn("AuthType").AsString(50).NotNullable().WithDefaultValue("None")
            .WithColumn("BearerToken").AsString(4000).Nullable()
            .WithColumn("ApiKey").AsString(4000).Nullable()
            .WithColumn("ApiKeyHeaderName").AsString(200).Nullable()
            .WithColumn("DateCreated").AsDateTime().NotNullable();

        Create.Table(ToolsTableName)
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("McpServerConfigurationId").AsGuid().NotNullable()
            .WithColumn("Name").AsString(200).NotNullable()
            .WithColumn("Description").AsString(4000).Nullable()
            .WithColumn("InputSchemaJson").AsString(int.MaxValue).Nullable()
            .WithColumn("OutputSchemaJson").AsString(int.MaxValue).Nullable()
            .WithColumn("IsEnabled").AsBoolean().NotNullable().WithDefaultValue(true);

        Create.Index(ToolsConfigurationIdIndexName)
            .OnTable(ToolsTableName)
            .OnColumn("McpServerConfigurationId");

        Create.ForeignKey(ToolsForeignKeyName)
            .FromTable(ToolsTableName).ForeignColumn("McpServerConfigurationId")
            .ToTable(ConfigurationsTableName).PrimaryColumn("Id")
            .OnDelete(System.Data.Rule.Cascade);
    }

    public override void Down()
    {
        Delete.ForeignKey(ToolsForeignKeyName)
            .OnTable(ToolsTableName);

        Delete.Index(ToolsConfigurationIdIndexName)
            .OnTable(ToolsTableName);

        Delete.Table(ToolsTableName);
        Delete.Table(ConfigurationsTableName);
    }
}