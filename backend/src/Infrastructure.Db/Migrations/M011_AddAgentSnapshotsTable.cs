using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_02_24_12_00_00, "Add AgentSnapshots table")]
public class M011_AddAgentSnapshotsTable : Migration
{
    public override void Up()
    {
        Create.Table("AgentSnapshots")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey()
            .WithColumn("AgentId").AsGuid().NotNullable().Unique()
            .WithColumn("MemorySummary").AsString().NotNullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable()
            .WithColumn("UpdatedAt").AsDateTime().NotNullable();

        // Note: RecentTranscript is stored as JSON in a separate table
        // EF Core OwnsMany maps to a separate table with JSON value conversion
        Create.Table("AgentSnapshotTranscriptEntries")
            .WithColumn("Id").AsInt32().NotNullable().PrimaryKey().Identity()
            .WithColumn("AgentSnapshotId").AsGuid().NotNullable()
            .WithColumn("Role").AsString().NotNullable()
            .WithColumn("Text").AsString().NotNullable();

        Create.Index("IX_AgentSnapshotTranscriptEntries_AgentSnapshotId")
            .OnTable("AgentSnapshotTranscriptEntries")
            .OnColumn("AgentSnapshotId");

        Create.ForeignKey("FK_AgentSnapshotTranscriptEntries_AgentSnapshots_AgentSnapshotId")
            .FromTable("AgentSnapshotTranscriptEntries")
            .ForeignColumn("AgentSnapshotId")
            .ToTable("AgentSnapshots")
            .PrimaryColumn("Id")
            .OnDelete(System.Data.Rule.Cascade);
    }

    public override void Down()
    {
        Delete.ForeignKey("FK_AgentSnapshotTranscriptEntries_AgentSnapshots_AgentSnapshotId");
        Delete.Index("IX_AgentSnapshotTranscriptEntries_AgentSnapshotId").OnTable("AgentSnapshotTranscriptEntries");
        Delete.Table("AgentSnapshotTranscriptEntries");
        Delete.Table("AgentSnapshots");
    }
}
