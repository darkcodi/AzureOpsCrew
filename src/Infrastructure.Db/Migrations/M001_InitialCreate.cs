using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_02_15_17_15_13, "Add Agent and Channel tables")]
public class M001_InitialCreate : Migration
{
    public override void Up()
    {
        Create.Table("Agent")
            .WithColumn("Id").AsString().NotNullable().PrimaryKey()
            .WithColumn("ProviderAgentId").AsString().NotNullable()
            .WithColumn("Info_Name").AsString().NotNullable()
            .WithColumn("Info_Prompt").AsString().NotNullable()
            .WithColumn("Info_Model").AsString().NotNullable()
            .WithColumn("Info_Description").AsString().Nullable()
            .WithColumn("Info_AvailableTools").AsString().NotNullable()
            .WithColumn("ProviderId").AsString().NotNullable().WithDefaultValue("00000000-0000-0000-0000-000000000000")
            .WithColumn("Color").AsString().NotNullable().WithDefaultValue("#43b581")
            .WithColumn("DateCreated").AsDateTime().NotNullable();

        Create.Index("IX_Agent_ProviderAgentId").OnTable("Agent").OnColumn("ProviderAgentId");

        Create.Table("Channel")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey()
            .WithColumn("Name").AsString().NotNullable()
            .WithColumn("Description").AsString().Nullable()
            .WithColumn("ConversationId").AsString().Nullable()
            .WithColumn("AgentIds").AsString().NotNullable()
            .WithColumn("DateCreated").AsDateTime().NotNullable();
    }

    public override void Down()
    {
        Delete.Table("Channel");
        Delete.Table("Agent");
    }
}
