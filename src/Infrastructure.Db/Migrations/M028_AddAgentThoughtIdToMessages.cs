using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_03_06_15_00_00, "Add AgentThoughtId column to Messages table")]
public class M028_AddAgentThoughtIdToMessages : Migration
{
    public override void Up()
    {
        Alter.Table("Messages")
            .AddColumn("AgentThoughtId")
            .AsGuid()
            .Nullable();

        // PostgreSQL: Convert Guid column to uuid type
        IfDatabase("Postgres")
            .Execute.Sql(@"ALTER TABLE ""Messages"" ALTER COLUMN ""AgentThoughtId"" TYPE uuid USING ""AgentThoughtId""::uuid;");
    }

    public override void Down()
    {
        Delete.Column("AgentThoughtId")
            .FromTable("Messages");
    }
}
