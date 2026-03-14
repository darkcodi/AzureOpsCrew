using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_02_24_12_02_00, "Change Agent Id and ProviderId to Guid columns")]
public class M013_ChangeAgentIdColumnsToGuid : Migration
{
    public override void Up()
    {
        Delete.PrimaryKey("PK_Agent")
            .FromTable("Agent");

        // For SQL Server, use FluentMigrator's AlterColumn
        IfDatabase("SqlServer")
            .Alter.Table("Agent")
                .AlterColumn("Id")
                .AsGuid()
                .NotNullable();

        // For PostgreSQL, clean up any invalid UUID data before type conversion (safely handles fresh databases)
        IfDatabase("Postgres")
            .Execute.Sql(@"DELETE FROM ""Agent"" WHERE ""Id"" IS NULL OR ""Id"" = '' OR ""Id"" !~ '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$'");

        IfDatabase("Postgres")
            .Execute.Sql(@"DELETE FROM ""Agent"" WHERE ""ProviderId"" IS NULL OR ""ProviderId"" = '' OR ""ProviderId"" !~ '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$'");

        // For PostgreSQL, convert Id column to uuid (idempotent - checks if already uuid)
        IfDatabase("Postgres")
            .Execute.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'Agent' AND column_name = 'Id' AND data_type = 'text'
                    ) THEN
                        ALTER TABLE ""Agent"" ALTER COLUMN ""Id"" TYPE uuid USING ""Id""::uuid;
                    END IF;
                END $$;");

        // Create primary key using provider-specific SQL
        IfDatabase("SqlServer")
            .Execute.Sql("ALTER TABLE [Agent] ADD CONSTRAINT PK_Agent PRIMARY KEY ([Id])");

        IfDatabase("Postgres")
            .Execute.Sql(@"ALTER TABLE ""Agent"" ADD CONSTRAINT PK_Agent PRIMARY KEY (""Id"")");

        // Need to drop the default constraint before altering the column
        Delete.DefaultConstraint()
            .OnTable("Agent")
            .OnColumn("ProviderId");

        // ProviderId column
        IfDatabase("SqlServer")
            .Alter.Table("Agent")
                .AlterColumn("ProviderId")
                .AsGuid()
                .NotNullable()
                .WithDefaultValue(new System.Guid("00000000-0000-0000-0000-000000000000"));

        IfDatabase("Postgres")
            .Execute.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'Agent' AND column_name = 'ProviderId' AND data_type = 'text'
                    ) THEN
                        ALTER TABLE ""Agent"" ALTER COLUMN ""ProviderId"" TYPE uuid USING ""ProviderId""::uuid;
                    END IF;
                END $$;");
    }

    public override void Down()
    {
        Delete.PrimaryKey("PK_Agent")
            .FromTable("Agent");

        Alter.Table("Agent")
            .AlterColumn("Id")
            .AsString(100)
            .NotNullable();

        // Create primary key using provider-specific SQL
        IfDatabase("SqlServer")
            .Execute.Sql("ALTER TABLE [Agent] ADD CONSTRAINT PK_Agent PRIMARY KEY ([Id])");

        IfDatabase("Postgres")
            .Execute.Sql("ALTER TABLE \"Agent\" ADD CONSTRAINT PK_Agent PRIMARY KEY (\"Id\")");

        Alter.Table("Agent")
            .AlterColumn("ProviderId")
            .AsString(100)
            .NotNullable()
            .WithDefaultValue("00000000-0000-0000-0000-000000000000");
    }
}
