using FluentMigrator;
namespace AzureOpsCrew.Infrastructure.Db.Migrations;
[Migration(2026_03_06_13_30_00, "Replace MCP ApiKey auth with CustomHeaders")]
public class M029_ReplaceMcpApiKeyAuthWithCustomHeaders : Migration
{
    private const string ConfigurationsTableName = "McpServerConfigurations";
    private const string AuthHeadersTableName = "McpServerConfigurationAuthHeaders";
    private const string AuthHeadersForeignKeyName = "FK_McpServerConfigurationAuthHeaders_McpServerConfigurations";
    private const string AuthHeadersConfigurationIdIndexName = "IX_McpServerConfigurationAuthHeaders_McpServerConfigurationId";
    public override void Up()
    {
        Create.Table(AuthHeadersTableName)
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("McpServerConfigurationId").AsGuid().NotNullable()
            .WithColumn("Name").AsString(200).NotNullable()
            .WithColumn("Value").AsString(4000).NotNullable();
        Create.Index(AuthHeadersConfigurationIdIndexName)
            .OnTable(AuthHeadersTableName)
            .OnColumn("McpServerConfigurationId");
        Create.ForeignKey(AuthHeadersForeignKeyName)
            .FromTable(AuthHeadersTableName).ForeignColumn("McpServerConfigurationId")
            .ToTable(ConfigurationsTableName).PrimaryColumn("Id")
            .OnDelete(System.Data.Rule.Cascade);
        Execute.Sql($@"
INSERT INTO {AuthHeadersTableName} (McpServerConfigurationId, Name, Value)
SELECT Id, COALESCE(NULLIF(ApiKeyHeaderName, ''), 'X-API-Key'), ApiKey
FROM {ConfigurationsTableName}
WHERE AuthType = 'ApiKey'
  AND ApiKey IS NOT NULL
  AND LTRIM(RTRIM(ApiKey)) <> '';");
        Execute.Sql($@"
UPDATE {ConfigurationsTableName}
SET AuthType = 'CustomHeaders'
WHERE AuthType = 'ApiKey';");
        Delete.Column("ApiKey").FromTable(ConfigurationsTableName);
        Delete.Column("ApiKeyHeaderName").FromTable(ConfigurationsTableName);
    }
    public override void Down()
    {
        Alter.Table(ConfigurationsTableName)
            .AddColumn("ApiKey").AsString(4000).Nullable()
            .AddColumn("ApiKeyHeaderName").AsString(200).Nullable();
        Execute.Sql($@"
WITH FirstHeader AS (
    SELECT
        McpServerConfigurationId,
        Name,
        Value,
        ROW_NUMBER() OVER (PARTITION BY McpServerConfigurationId ORDER BY Id) AS RowNumber
    FROM {AuthHeadersTableName}
)
UPDATE config
SET
    ApiKey = header.Value,
    ApiKeyHeaderName = header.Name,
    AuthType = 'ApiKey'
FROM {ConfigurationsTableName} config
INNER JOIN FirstHeader header
    ON header.McpServerConfigurationId = config.Id
   AND header.RowNumber = 1
WHERE config.AuthType = 'CustomHeaders';");
        Delete.ForeignKey(AuthHeadersForeignKeyName)
            .OnTable(AuthHeadersTableName);
        Delete.Index(AuthHeadersConfigurationIdIndexName)
            .OnTable(AuthHeadersTableName);
        Delete.Table(AuthHeadersTableName);
    }
}
