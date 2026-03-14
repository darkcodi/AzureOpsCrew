using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_03_11_17_00_00, "Add Triggers and WaitConditions tables")]
public class M033_AddTriggersAndWaitConditions : Migration
{
    public override void Up()
    {
        Create.Table("Triggers")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey()
            .WithColumn("Type").AsInt32().NotNullable()           // TriggerType enum
            .WithColumn("AgentId").AsGuid().NotNullable()
            .WithColumn("ChatId").AsGuid().NotNullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable()
            .WithColumn("StartedAt").AsDateTime().Nullable()
            .WithColumn("CompletedAt").AsDateTime().Nullable()
            .WithColumn("IsSkipped").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("MessageId").AsGuid().Nullable()
            .WithColumn("AuthorId").AsGuid().Nullable()
            .WithColumn("AuthorName").AsString(200).Nullable()
            .WithColumn("MessageContent").AsString(int.MaxValue).Nullable()
            .WithColumn("CallId").AsString(200).Nullable()
            .WithColumn("Resolution").AsInt32().Nullable()         // ApprovalResolution enum
            .WithColumn("ToolName").AsString(200).Nullable()
            .WithColumn("Parameters").AsString(int.MaxValue).Nullable();

        // PostgreSQL: Convert Guid columns to uuid type
        IfDatabase("Postgres")
            .Execute.Sql(@"ALTER TABLE ""Triggers"" ALTER COLUMN ""Id"" TYPE uuid USING ""Id""::uuid;
                   ALTER TABLE ""Triggers"" ALTER COLUMN ""AgentId"" TYPE uuid USING ""AgentId""::uuid;
                   ALTER TABLE ""Triggers"" ALTER COLUMN ""ChatId"" TYPE uuid USING ""ChatId""::uuid;
                   ALTER TABLE ""Triggers"" ALTER COLUMN ""MessageId"" TYPE uuid USING ""MessageId""::uuid;
                   ALTER TABLE ""Triggers"" ALTER COLUMN ""AuthorId"" TYPE uuid USING ""AuthorId""::uuid;");

        Create.Table("WaitConditions")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey()
            .WithColumn("Type").AsInt32().NotNullable()                    // WaitConditionType enum
            .WithColumn("AgentId").AsGuid().NotNullable()
            .WithColumn("ChatId").AsGuid().NotNullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable()
            .WithColumn("CompletedAt").AsDateTime().Nullable()
            .WithColumn("SatisfiedByTriggerId").AsGuid().Nullable()
            .WithColumn("MessageAfterDateTime").AsDateTime().Nullable()
            .WithColumn("ToolCallId").AsString(200).Nullable();

        // PostgreSQL: Convert Guid columns to uuid type
        IfDatabase("Postgres")
            .Execute.Sql(@"ALTER TABLE ""WaitConditions"" ALTER COLUMN ""Id"" TYPE uuid USING ""Id""::uuid;
                   ALTER TABLE ""WaitConditions"" ALTER COLUMN ""AgentId"" TYPE uuid USING ""AgentId""::uuid;
                   ALTER TABLE ""WaitConditions"" ALTER COLUMN ""ChatId"" TYPE uuid USING ""ChatId""::uuid;
                   ALTER TABLE ""WaitConditions"" ALTER COLUMN ""SatisfiedByTriggerId"" TYPE uuid USING ""SatisfiedByTriggerId""::uuid;");

        // Foreign key from WaitConditions.SatisfiedByTriggerId to Triggers.Id
        Create.ForeignKey("FK_WaitConditions_Triggers_SatisfiedByTriggerId")
            .FromTable("WaitConditions").ForeignColumn("SatisfiedByTriggerId")
            .ToTable("Triggers").PrimaryColumn("Id");

        // Indexes for Triggers
        Create.Index("IX_Triggers_AgentId").OnTable("Triggers").OnColumn("AgentId");
        Create.Index("IX_Triggers_ChatId").OnTable("Triggers").OnColumn("ChatId");
        Create.Index("IX_Triggers_Type").OnTable("Triggers").OnColumn("Type");

        // Indexes for WaitConditions
        Create.Index("IX_WaitConditions_AgentId").OnTable("WaitConditions").OnColumn("AgentId");
        Create.Index("IX_WaitConditions_ChatId").OnTable("WaitConditions").OnColumn("ChatId");
        Create.Index("IX_WaitConditions_Type").OnTable("WaitConditions").OnColumn("Type");
        Create.Index("IX_WaitConditions_SatisfiedByTriggerId").OnTable("WaitConditions").OnColumn("SatisfiedByTriggerId");
    }

    public override void Down()
    {
        // Drop indexes
        Delete.Index("IX_WaitConditions_SatisfiedByTriggerId").OnTable("WaitConditions");
        Delete.Index("IX_WaitConditions_Type").OnTable("WaitConditions");
        Delete.Index("IX_WaitConditions_ChatId").OnTable("WaitConditions");
        Delete.Index("IX_WaitConditions_AgentId").OnTable("WaitConditions");

        Delete.Index("IX_Triggers_Type").OnTable("Triggers");
        Delete.Index("IX_Triggers_ChatId").OnTable("Triggers");
        Delete.Index("IX_Triggers_AgentId").OnTable("Triggers");

        // Drop foreign key
        Delete.ForeignKey("FK_WaitConditions_Triggers_SatisfiedByTriggerId").OnTable("WaitConditions");

        // Drop tables (in reverse order)
        Delete.Table("WaitConditions");
        Delete.Table("Triggers");
    }
}
