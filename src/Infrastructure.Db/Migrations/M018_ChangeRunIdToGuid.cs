using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_02_25_15_00_00, "Change RunId to Guid in LlmChatMessages")]
public class M018_ChangeRunIdToGuid : Migration
{
    public override void Up()
    {
        Delete.Index("IX_LlmChatMessages_RunId").OnTable("LlmChatMessages");

        Delete.DefaultConstraint()
            .OnTable("LlmChatMessages")
            .OnColumn("RunId");

        // For SQL Server
        IfDatabase("SqlServer")
            .Alter.Table("LlmChatMessages")
                .AlterColumn("RunId")
                .AsGuid()
                .NotNullable();

        // For PostgreSQL, clean up any invalid UUID data before type conversion
        IfDatabase("Postgres")
            .Execute.Sql(@"DELETE FROM ""LlmChatMessages"" WHERE ""RunId"" IS NULL OR ""RunId"" = '' OR ""RunId"" !~ '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$'");

        // For PostgreSQL, convert RunId column to uuid
        IfDatabase("Postgres")
            .Execute.Sql(@"ALTER TABLE ""LlmChatMessages"" ALTER COLUMN ""RunId"" TYPE uuid USING ""RunId""::uuid");

        Create.Index("IX_LlmChatMessages_RunId").OnTable("LlmChatMessages").OnColumn("RunId");
    }

    public override void Down()
    {
        Delete.Index("IX_LlmChatMessages_RunId").OnTable("LlmChatMessages");

        // Drop default constraint if it exists (works for both SQL Server and PostgreSQL)
        Delete.DefaultConstraint()
            .OnTable("LlmChatMessages")
            .OnColumn("RunId");

        Alter.Table("LlmChatMessages")
            .AlterColumn("RunId").AsString(200).NotNullable().WithDefaultValue(string.Empty);

        Create.Index("IX_LlmChatMessages_RunId").OnTable("LlmChatMessages").OnColumn("RunId");
    }
}
