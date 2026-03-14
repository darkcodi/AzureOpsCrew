using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_03_14_12_00_00, "Fix PostgreSQL UUID column types")]
public class M034_FixPostgresUuidColumns : Migration
{
    public override void Up()
    {
        IfDatabase("Postgres")
            .Execute.Sql(@"DELETE FROM ""AgentThoughts"" WHERE ""RunId"" IS NULL OR ""RunId"" = '' OR ""RunId"" !~ '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$';");

        IfDatabase("Postgres")
            .Execute.Sql(@"ALTER TABLE ""AgentThoughts"" ALTER COLUMN ""RunId"" TYPE uuid USING ""RunId""::uuid;");
    }

    public override void Down()
    {
        // No down migration needed
    }
}
