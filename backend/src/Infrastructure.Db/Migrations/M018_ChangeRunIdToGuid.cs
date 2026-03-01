using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_02_25_15_00_00, "Change RunId to Guid in LlmChatMessages")]
public class M018_ChangeRunIdToGuid : Migration
{
    public override void Up()
    {
        Delete.Index("IX_LlmChatMessages_RunId").OnTable("LlmChatMessages");

        // Drop default constraint if it exists
        Execute.Sql(@"
            IF EXISTS (
                SELECT 1 FROM sys.default_constraints
                WHERE name = 'DF_LlmChatMessages_RunId'
                AND parent_object_id = OBJECT_ID('LlmChatMessages')
            )
            BEGIN
                ALTER TABLE [dbo].[LlmChatMessages] DROP CONSTRAINT [DF_LlmChatMessages_RunId]
            END
        ");

        Alter.Table("LlmChatMessages")
            .AlterColumn("RunId").AsGuid().NotNullable();

        Create.Index("IX_LlmChatMessages_RunId").OnTable("LlmChatMessages").OnColumn("RunId");
    }

    public override void Down()
    {
        Delete.Index("IX_LlmChatMessages_RunId").OnTable("LlmChatMessages");

        // Drop default constraint if it exists
        Execute.Sql(@"
            IF EXISTS (
                SELECT 1 FROM sys.default_constraints
                WHERE name = 'DF_LlmChatMessages_RunId'
                AND parent_object_id = OBJECT_ID('LlmChatMessages')
            )
            BEGIN
                ALTER TABLE [dbo].[LlmChatMessages] DROP CONSTRAINT [DF_LlmChatMessages_RunId]
            END
        ");

        Alter.Table("LlmChatMessages")
            .AlterColumn("RunId").AsString(200).NotNullable().WithDefaultValue(string.Empty);

        Create.Index("IX_LlmChatMessages_RunId").OnTable("LlmChatMessages").OnColumn("RunId");
    }
}
