using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_03_10_20_00_00, "Add Triggers table")]
public class M033_AddTriggersTable : Migration
{
    private const string TableName = "Triggers";
    private const string AgentIdIndexName = "IX_Triggers_AgentId";
    private const string ChatIdIndexName = "IX_Triggers_ChatId";
    private const string TypeEnabledIndexName = "IX_Triggers_TriggerType_IsEnabled";

    public override void Up()
    {
        Create.Table(TableName)
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey()
            .WithColumn("AgentId").AsGuid().NotNullable()
            .WithColumn("ChatId").AsGuid().NotNullable()
            .WithColumn("TriggerType").AsInt32().NotNullable()
            .WithColumn("ConfigurationJson").AsString(int.MaxValue).Nullable()
            .WithColumn("IsEnabled").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("LastFiredAt").AsDateTime().Nullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable()
            .WithColumn("UpdatedAt").AsDateTime().NotNullable();

        Create.Index(AgentIdIndexName)
            .OnTable(TableName)
            .OnColumn("AgentId");

        Create.Index(ChatIdIndexName)
            .OnTable(TableName)
            .OnColumn("ChatId");

        Create.Index(TypeEnabledIndexName)
            .OnTable(TableName)
            .OnColumn("TriggerType").Ascending()
            .OnColumn("IsEnabled").Ascending();
    }

    public override void Down()
    {
        Delete.Index(TypeEnabledIndexName).OnTable(TableName);
        Delete.Index(ChatIdIndexName).OnTable(TableName);
        Delete.Index(AgentIdIndexName).OnTable(TableName);
        Delete.Table(TableName);
    }
}
