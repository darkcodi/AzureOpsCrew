using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_02_28_16_00_00, "Add RawLlmHttpCalls table")]
public class M020_AddRawLlmHttpCallsTable : Migration
{
    public override void Up()
    {
        Create.Table("RawLlmHttpCalls")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey()
            .WithColumn("AgentId").AsGuid().NotNullable()
            .WithColumn("ThreadId").AsGuid().NotNullable()
            .WithColumn("RunId").AsGuid().NotNullable()
            .WithColumn("HttpRequest").AsString(int.MaxValue).NotNullable()
            .WithColumn("HttpResponse").AsString(int.MaxValue).NotNullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable();

        // PostgreSQL: Convert Guid columns to uuid type
        IfDatabase("Postgres")
            .Execute.Sql(@"ALTER TABLE ""RawLlmHttpCalls"" ALTER COLUMN ""Id"" TYPE uuid USING ""Id""::uuid;
                   ALTER TABLE ""RawLlmHttpCalls"" ALTER COLUMN ""AgentId"" TYPE uuid USING ""AgentId""::uuid;
                   ALTER TABLE ""RawLlmHttpCalls"" ALTER COLUMN ""ThreadId"" TYPE uuid USING ""ThreadId""::uuid;
                   ALTER TABLE ""RawLlmHttpCalls"" ALTER COLUMN ""RunId"" TYPE uuid USING ""RunId""::uuid;");

        Create.Index("IX_RawLlmHttpCalls_AgentId").OnTable("RawLlmHttpCalls").OnColumn("AgentId");
        Create.Index("IX_RawLlmHttpCalls_ThreadId").OnTable("RawLlmHttpCalls").OnColumn("ThreadId");
        Create.Index("IX_RawLlmHttpCalls_RunId").OnTable("RawLlmHttpCalls").OnColumn("RunId");
    }

    public override void Down()
    {
        Delete.Index("IX_RawLlmHttpCalls_RunId").OnTable("RawLlmHttpCalls");
        Delete.Index("IX_RawLlmHttpCalls_ThreadId").OnTable("RawLlmHttpCalls");
        Delete.Index("IX_RawLlmHttpCalls_AgentId").OnTable("RawLlmHttpCalls");
        Delete.Table("RawLlmHttpCalls");
    }
}
