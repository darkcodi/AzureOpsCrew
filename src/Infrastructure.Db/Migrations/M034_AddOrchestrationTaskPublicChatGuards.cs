using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_03_10_19_00_00, "Add public chat guardrail fields to OrchestrationTasks")]
public class M034_AddOrchestrationTaskPublicChatGuards : Migration
{
    public override void Up()
    {
        Alter.Table("OrchestrationTasks")
            .AddColumn("PublicStartedMessageAtUtc").AsDateTime().Nullable()
            .AddColumn("PublicProgressMessageAtUtc").AsDateTime().Nullable()
            .AddColumn("PublicFinalMessageAtUtc").AsDateTime().Nullable()
            .AddColumn("LastPublicProgressSummary").AsString(4000).Nullable();
    }

    public override void Down()
    {
        Delete.Column("LastPublicProgressSummary").FromTable("OrchestrationTasks");
        Delete.Column("PublicFinalMessageAtUtc").FromTable("OrchestrationTasks");
        Delete.Column("PublicProgressMessageAtUtc").FromTable("OrchestrationTasks");
        Delete.Column("PublicStartedMessageAtUtc").FromTable("OrchestrationTasks");
    }
}
