using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_03_02_10_00_00, "Add execution engine tables: runs, tasks, artifacts, journal, approvals")]
public class M009_AddExecutionEngineTables : Migration
{
    public override void Up()
    {
        // ── ExecutionRun ──
        Create.Table("ExecutionRun")
            .WithColumn("Id").AsString(36).PrimaryKey()
            .WithColumn("ChannelId").AsString(36).NotNullable()
            .WithColumn("UserId").AsInt32().NotNullable()
            .WithColumn("ThreadId").AsString(128).NotNullable()
            .WithColumn("UserRequest").AsString(4000).NotNullable()
            .WithColumn("Goal").AsString(2000).Nullable()
            .WithColumn("Service").AsString(256).Nullable()
            .WithColumn("Environment").AsString(128).Nullable()
            .WithColumn("Severity").AsString(64).Nullable()
            .WithColumn("Status").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("InitialPlan").AsString(8000).Nullable()
            .WithColumn("CurrentPlan").AsString(8000).Nullable()
            .WithColumn("PlanRevision").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("LastReplanReason").AsString(2000).Nullable()
            .WithColumn("TotalSteps").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("TotalToolCalls").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("TotalReplans").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("ConsecutiveNonProgressSteps").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("CreatedAt").AsDateTime().NotNullable()
            .WithColumn("UpdatedAt").AsDateTime().NotNullable()
            .WithColumn("StartedAt").AsDateTime().Nullable()
            .WithColumn("CompletedAt").AsDateTime().Nullable()
            .WithColumn("ResultSummary").AsString(4000).Nullable()
            .WithColumn("ErrorMessage").AsString(4000).Nullable();

        Create.Index("IX_ExecutionRun_ChannelId").OnTable("ExecutionRun").OnColumn("ChannelId");
        Create.Index("IX_ExecutionRun_UserId").OnTable("ExecutionRun").OnColumn("UserId");
        Create.Index("IX_ExecutionRun_Status").OnTable("ExecutionRun").OnColumn("Status");

        // ── ExecutionTask ──
        Create.Table("ExecutionTask")
            .WithColumn("Id").AsString(36).PrimaryKey()
            .WithColumn("RunId").AsString(36).NotNullable()
            .WithColumn("ParentTaskId").AsString(36).Nullable()
            .WithColumn("Title").AsString(512).NotNullable()
            .WithColumn("Description").AsString(4000).Nullable()
            .WithColumn("TaskType").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("AssignedAgent").AsString(128).Nullable()
            .WithColumn("Priority").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("Status").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("DependsOn").AsString(2000).Nullable()
            .WithColumn("Goal").AsString(2000).Nullable()
            .WithColumn("Inputs").AsString(8000).Nullable()
            .WithColumn("ResultSummary").AsString(4000).Nullable()
            .WithColumn("StepCount").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("RetryCount").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("ReplanCount").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("CreatedAt").AsDateTime().NotNullable()
            .WithColumn("UpdatedAt").AsDateTime().NotNullable()
            .WithColumn("StartedAt").AsDateTime().Nullable()
            .WithColumn("CompletedAt").AsDateTime().Nullable();

        Create.Index("IX_ExecutionTask_RunId").OnTable("ExecutionTask").OnColumn("RunId");
        Create.Index("IX_ExecutionTask_ParentTaskId").OnTable("ExecutionTask").OnColumn("ParentTaskId");
        Create.Index("IX_ExecutionTask_Status").OnTable("ExecutionTask").OnColumn("Status");

        // ── Artifact ──
        Create.Table("Artifact")
            .WithColumn("Id").AsString(36).PrimaryKey()
            .WithColumn("RunId").AsString(36).NotNullable()
            .WithColumn("TaskId").AsString(36).Nullable()
            .WithColumn("ArtifactType").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("Source").AsString(256).Nullable()
            .WithColumn("CreatedBy").AsString(128).Nullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable()
            .WithColumn("Content").AsCustom("TEXT").NotNullable() // unbounded text
            .WithColumn("Summary").AsString(2000).Nullable()
            .WithColumn("Tags").AsString(512).Nullable();

        Create.Index("IX_Artifact_RunId").OnTable("Artifact").OnColumn("RunId");
        Create.Index("IX_Artifact_TaskId").OnTable("Artifact").OnColumn("TaskId");

        // ── JournalEntry ──
        Create.Table("JournalEntry")
            .WithColumn("Id").AsString(36).PrimaryKey()
            .WithColumn("RunId").AsString(36).NotNullable()
            .WithColumn("TaskId").AsString(36).Nullable()
            .WithColumn("EntryType").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("Agent").AsString(128).Nullable()
            .WithColumn("Message").AsString(4000).NotNullable()
            .WithColumn("Detail").AsCustom("TEXT").Nullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable();

        Create.Index("IX_JournalEntry_RunId").OnTable("JournalEntry").OnColumn("RunId");
        Create.Index("IX_JournalEntry_TaskId").OnTable("JournalEntry").OnColumn("TaskId");

        // ── ApprovalRequest ──
        Create.Table("ApprovalRequest")
            .WithColumn("Id").AsString(36).PrimaryKey()
            .WithColumn("RunId").AsString(36).NotNullable()
            .WithColumn("TaskId").AsString(36).Nullable()
            .WithColumn("ActionType").AsString(128).NotNullable()
            .WithColumn("ProposedAction").AsString(4000).NotNullable()
            .WithColumn("Target").AsString(512).Nullable()
            .WithColumn("EvidenceRefs").AsString(2000).Nullable()
            .WithColumn("RiskLevel").AsInt32().NotNullable().WithDefaultValue(10)
            .WithColumn("RollbackPlan").AsString(4000).Nullable()
            .WithColumn("VerificationPlan").AsString(4000).Nullable()
            .WithColumn("AffectedResources").AsString(2000).Nullable()
            .WithColumn("Status").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("DecisionReason").AsString(2000).Nullable()
            .WithColumn("RequestedAt").AsDateTime().NotNullable()
            .WithColumn("RespondedAt").AsDateTime().Nullable()
            .WithColumn("RespondedBy").AsString(256).Nullable();

        Create.Index("IX_ApprovalRequest_RunId").OnTable("ApprovalRequest").OnColumn("RunId");
        Create.Index("IX_ApprovalRequest_Status").OnTable("ApprovalRequest").OnColumn("Status");
    }

    public override void Down()
    {
        Delete.Table("ApprovalRequest");
        Delete.Table("JournalEntry");
        Delete.Table("Artifact");
        Delete.Table("ExecutionTask");
        Delete.Table("ExecutionRun");
    }
}
