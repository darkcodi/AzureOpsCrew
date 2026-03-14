using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_02_25_15_30_00, "Add ThreadId to LlmChatMessages")]
public class M019_AddThreadIdToLlmChatMessages : Migration
{
    public override void Up()
    {
        Alter.Table("LlmChatMessages")
            .AddColumn("ThreadId").AsGuid().NotNullable().WithDefaultValue(Guid.Empty);

        // PostgreSQL: Convert Guid column to native uuid type
        IfDatabase("Postgres")
            .Execute.Sql(@"ALTER TABLE ""LlmChatMessages"" ALTER COLUMN ""ThreadId"" TYPE uuid USING ""ThreadId""::uuid;");

        Create.Index("IX_LlmChatMessages_ThreadId").OnTable("LlmChatMessages").OnColumn("ThreadId");
    }

    public override void Down()
    {
        Delete.Index("IX_LlmChatMessages_ThreadId").OnTable("LlmChatMessages");
        Delete.Column("ThreadId").FromTable("LlmChatMessages");
    }
}
