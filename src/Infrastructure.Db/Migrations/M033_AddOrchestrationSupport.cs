using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_03_09_10_00_00, "Add orchestration support: ManagerAgentId on Channels, OrchestrationTasks table")]
public class M033_AddOrchestrationSupport : Migration
{
    public override void Up()
    {
        // Add ManagerAgentId to Channels table
        Alter.Table("Channels")
            .AddColumn("ManagerAgentId").AsGuid().Nullable();

        // Create OrchestrationTasks table
        Create.Table("OrchestrationTasks")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey()
            .WithColumn("ChannelId").AsGuid().NotNullable()
            .WithColumn("CreatedByAgentId").AsGuid().NotNullable()
            .WithColumn("AssignedAgentId").AsGuid().NotNullable()
            .WithColumn("Title").AsString(500).NotNullable()
            .WithColumn("Description").AsString(4000).NotNullable()
            .WithColumn("Status").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("ProgressSummary").AsString(4000).Nullable()
            .WithColumn("ResultSummary").AsString(4000).Nullable()
            .WithColumn("FailureReason").AsString(4000).Nullable()
            .WithColumn("AnnounceInChat").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("CreatedAtUtc").AsDateTime().NotNullable()
            .WithColumn("StartedAtUtc").AsDateTime().Nullable()
            .WithColumn("CompletedAtUtc").AsDateTime().Nullable()
            .WithColumn("FailedAtUtc").AsDateTime().Nullable();

        Create.Index("IX_OrchestrationTasks_ChannelId")
            .OnTable("OrchestrationTasks")
            .OnColumn("ChannelId");

        Create.Index("IX_OrchestrationTasks_AssignedAgentId")
            .OnTable("OrchestrationTasks")
            .OnColumn("AssignedAgentId");

        Create.Index("IX_OrchestrationTasks_ChannelId_Status")
            .OnTable("OrchestrationTasks")
            .OnColumn("ChannelId").Ascending()
            .OnColumn("Status").Ascending();
    }

    public override void Down()
    {
        Delete.Table("OrchestrationTasks");
        Delete.Column("ManagerAgentId").FromTable("Channels");
    }
}
