using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_03_10_20_30_00, "Add TriggerExecutions table")]
public class M034_AddTriggerExecutionsTable : Migration
{
    private const string TableName = "TriggerExecutions";
    private const string TriggerIdIndexName = "IX_TriggerExecutions_TriggerId";
    private const string FiredAtIndexName = "IX_TriggerExecutions_FiredAt";

    public override void Up()
    {
        Create.Table(TableName)
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey()
            .WithColumn("TriggerId").AsGuid().NotNullable()
            .WithColumn("FiredAt").AsDateTime2().NotNullable()
            .WithColumn("ContextJson").AsString(int.MaxValue).Nullable()
            .WithColumn("Success").AsBoolean().NotNullable()
            .WithColumn("ErrorMessage").AsString(int.MaxValue).Nullable()
            .WithColumn("CompletedAt").AsDateTime2().Nullable();

        Create.Index(TriggerIdIndexName)
            .OnTable(TableName)
            .OnColumn("TriggerId");

        Create.Index(FiredAtIndexName)
            .OnTable(TableName)
            .OnColumn("FiredAt");
    }

    public override void Down()
    {
        Delete.Index(FiredAtIndexName).OnTable(TableName);
        Delete.Index(TriggerIdIndexName).OnTable(TableName);
        Delete.Table(TableName);
    }
}
