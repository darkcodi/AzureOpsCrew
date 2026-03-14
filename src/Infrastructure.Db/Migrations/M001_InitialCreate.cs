using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(1, "Initial database schema")]
public class M001_InitialCreate : Migration
{
    public override void Up()
    {
        // 1. Providers
        Create.Table("Providers")
            .WithColumn("Id").AsString().NotNullable().PrimaryKey()
            .WithColumn("Name").AsString(200).NotNullable()
            .WithColumn("ProviderType").AsString().NotNullable()
            .WithColumn("ApiKey").AsString(500).NotNullable()
            .WithColumn("ApiEndpoint").AsString(500).Nullable()
            .WithColumn("DefaultModel").AsString(200).Nullable()
            .WithColumn("SelectedModels").AsString(4000).Nullable()
            .WithColumn("ModelsCount").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("IsEnabled").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("DateCreated").AsDateTime().NotNullable()
            .WithColumn("DateModified").AsDateTime().Nullable();

        // 2. Users
        Create.Table("Users")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey()
            .WithColumn("Email").AsString(320).NotNullable()
            .WithColumn("NormalizedEmail").AsString(320).NotNullable()
            .WithColumn("PasswordHash").AsString(512).NotNullable()
            .WithColumn("Username").AsString(30).NotNullable()
            .WithColumn("NormalizedUsername").AsString(30).NotNullable()
            .WithColumn("IsActive").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("DateCreated").AsDateTime().NotNullable()
            .WithColumn("DateModified").AsDateTime().Nullable()
            .WithColumn("LastLoginAt").AsDateTime().Nullable();

        Create.Index("IX_Users_NormalizedEmail").OnTable("Users").OnColumn("NormalizedEmail").Unique();
        Create.Index("IX_Users_NormalizedUsername").OnTable("Users").OnColumn("NormalizedUsername").Unique();

        // 3. PendingRegistrations
        Create.Table("PendingRegistrations")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("Email").AsString(320).NotNullable()
            .WithColumn("NormalizedEmail").AsString(320).NotNullable()
            .WithColumn("Username").AsString(30).NotNullable()
            .WithColumn("NormalizedUsername").AsString(30).NotNullable()
            .WithColumn("PasswordHash").AsString(512).NotNullable()
            .WithColumn("VerificationCodeHash").AsString(512).NotNullable()
            .WithColumn("VerificationCodeExpiresAt").AsDateTime().NotNullable()
            .WithColumn("VerificationCodeSentAt").AsDateTime().NotNullable()
            .WithColumn("VerificationAttempts").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("DateCreated").AsDateTime().NotNullable()
            .WithColumn("DateModified").AsDateTime().Nullable();

        Create.Index("IX_PendingRegistrations_NormalizedEmail").OnTable("PendingRegistrations").OnColumn("NormalizedEmail").Unique();
        Create.Index("IX_PendingRegistrations_NormalizedUsername").OnTable("PendingRegistrations").OnColumn("NormalizedUsername").Unique();

        // 4. Agents
        Create.Table("Agents")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey()
            .WithColumn("ProviderAgentId").AsString().NotNullable()
            .WithColumn("Info_Username").AsString(30).NotNullable()
            .WithColumn("Info_Prompt").AsString(8000).NotNullable()
            .WithColumn("Info_Model").AsString().NotNullable()
            .WithColumn("Info_Description").AsString().Nullable()
            .WithColumn("Info_AvailableMcpServerTools").AsString(int.MaxValue).NotNullable().WithDefaultValue("[]")
            .WithColumn("ProviderId").AsGuid().NotNullable()
            .WithColumn("Color").AsString().NotNullable().WithDefaultValue("#43b581")
            .WithColumn("DateCreated").AsDateTime().NotNullable();

        Create.Index("IX_Agents_ProviderAgentId").OnTable("Agents").OnColumn("ProviderAgentId");
        Create.Index("IX_Agents_Info_Username").OnTable("Agents").OnColumn("Info_Username").Unique();

        // 5. Channels
        Create.Table("Channels")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey()
            .WithColumn("Name").AsString().NotNullable()
            .WithColumn("Description").AsString().Nullable()
            .WithColumn("ConversationId").AsString().Nullable()
            .WithColumn("AgentIds").AsString().NotNullable()
            .WithColumn("DateCreated").AsDateTime().NotNullable();

        // 6. McpServerConfigurations
        Create.Table("McpServerConfigurations")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey()
            .WithColumn("Name").AsString(200).NotNullable()
            .WithColumn("Description").AsString(4000).Nullable()
            .WithColumn("Url").AsString(1000).NotNullable()
            .WithColumn("IsEnabled").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("ToolsSyncedAt").AsDateTime().Nullable()
            .WithColumn("AuthType").AsString(50).NotNullable().WithDefaultValue("None")
            .WithColumn("BearerToken").AsString(4000).Nullable()
            .WithColumn("DateCreated").AsDateTime().NotNullable();

        // 7. McpServerConfigurationAuthHeaders
        Create.Table("McpServerConfigurationAuthHeaders")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("McpServerConfigurationId").AsGuid().NotNullable()
            .WithColumn("Name").AsString(200).NotNullable()
            .WithColumn("Value").AsString(4000).NotNullable();

        Create.Index("IX_McpServerConfigurationAuthHeaders_McpServerConfigurationId")
            .OnTable("McpServerConfigurationAuthHeaders")
            .OnColumn("McpServerConfigurationId");

        // 8. McpServerConfigurationTools
        Create.Table("McpServerConfigurationTools")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("McpServerConfigurationId").AsGuid().NotNullable()
            .WithColumn("Name").AsString(200).NotNullable()
            .WithColumn("Description").AsString(4000).Nullable()
            .WithColumn("InputSchemaJson").AsString(int.MaxValue).Nullable()
            .WithColumn("OutputSchemaJson").AsString(int.MaxValue).Nullable()
            .WithColumn("IsEnabled").AsBoolean().NotNullable().WithDefaultValue(true);

        Create.Index("IX_McpServerConfigurationTools_McpServerConfigurationId")
            .OnTable("McpServerConfigurationTools")
            .OnColumn("McpServerConfigurationId");

        // 9. DirectMessageChannels
        Create.Table("DirectMessageChannels")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey()
            .WithColumn("User1Id").AsGuid().Nullable()
            .WithColumn("User2Id").AsGuid().Nullable()
            .WithColumn("Agent1Id").AsGuid().Nullable()
            .WithColumn("Agent2Id").AsGuid().Nullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable();

        Create.Index("IX_DirectMessageChannels_User1Id").OnTable("DirectMessageChannels").OnColumn("User1Id");
        Create.Index("IX_DirectMessageChannels_User2Id").OnTable("DirectMessageChannels").OnColumn("User2Id");
        Create.Index("IX_DirectMessageChannels_Agent1Id").OnTable("DirectMessageChannels").OnColumn("Agent1Id");
        Create.Index("IX_DirectMessageChannels_Agent2Id").OnTable("DirectMessageChannels").OnColumn("Agent2Id");

        // 10. Messages
        Create.Table("Messages")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey()
            .WithColumn("Text").AsString(int.MaxValue).NotNullable()
            .WithColumn("PostedAt").AsDateTime().NotNullable()
            .WithColumn("AuthorName").AsString().Nullable()
            .WithColumn("AgentId").AsGuid().Nullable()
            .WithColumn("UserId").AsGuid().Nullable()
            .WithColumn("ChannelId").AsGuid().Nullable()
            .WithColumn("DmId").AsGuid().Nullable()
            .WithColumn("AgentThoughtId").AsGuid().Nullable();

        Create.Index("IX_Messages_PostedAt").OnTable("Messages").OnColumn("PostedAt");
        Create.Index("IX_Messages_AgentId").OnTable("Messages").OnColumn("AgentId");
        Create.Index("IX_Messages_UserId").OnTable("Messages").OnColumn("UserId");
        Create.Index("IX_Messages_ChannelId").OnTable("Messages").OnColumn("ChannelId");
        Create.Index("IX_Messages_DmId").OnTable("Messages").OnColumn("DmId");
        Create.Index("IX_Messages_AgentThoughtId").OnTable("Messages").OnColumn("AgentThoughtId");

        // 11. AgentThoughts
        Create.Table("AgentThoughts")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey()
            .WithColumn("AgentId").AsGuid().NotNullable()
            .WithColumn("ThreadId").AsGuid().NotNullable()
            .WithColumn("RunId").AsGuid().NotNullable()
            .WithColumn("IsHidden").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("Role").AsString(50).NotNullable()
            .WithColumn("AuthorName").AsString(256).Nullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable()
            .WithColumn("ContentType").AsString(100).Nullable()
            .WithColumn("ContentJson").AsString(int.MaxValue).NotNullable()
            .WithColumn("ChatMessageId").AsGuid().NotNullable().WithDefaultValue(Guid.Empty);

        Create.Index("IX_AgentThoughts_AgentId").OnTable("AgentThoughts").OnColumn("AgentId");
        Create.Index("IX_AgentThoughts_ThreadId").OnTable("AgentThoughts").OnColumn("ThreadId");
        Create.Index("IX_AgentThoughts_RunId").OnTable("AgentThoughts").OnColumn("RunId");
        Create.Index("IX_AgentThoughts_CreatedAt").OnTable("AgentThoughts").OnColumn("CreatedAt");
        Create.Index("IX_AgentThoughts_ChatMessageId").OnTable("AgentThoughts").OnColumn("ChatMessageId");

        // 12. RawLlmHttpCalls
        Create.Table("RawLlmHttpCalls")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey()
            .WithColumn("AgentId").AsGuid().NotNullable()
            .WithColumn("ThreadId").AsGuid().NotNullable()
            .WithColumn("RunId").AsGuid().NotNullable()
            .WithColumn("HttpRequest").AsString(int.MaxValue).NotNullable()
            .WithColumn("HttpResponse").AsString(int.MaxValue).NotNullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable();

        Create.Index("IX_RawLlmHttpCalls_AgentId").OnTable("RawLlmHttpCalls").OnColumn("AgentId");
        Create.Index("IX_RawLlmHttpCalls_ThreadId").OnTable("RawLlmHttpCalls").OnColumn("ThreadId");
        Create.Index("IX_RawLlmHttpCalls_RunId").OnTable("RawLlmHttpCalls").OnColumn("RunId");

        // 13. Triggers
        Create.Table("Triggers")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey()
            .WithColumn("Type").AsInt32().NotNullable()
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
            .WithColumn("Resolution").AsInt32().Nullable()
            .WithColumn("ToolName").AsString(200).Nullable()
            .WithColumn("Parameters").AsString(int.MaxValue).Nullable();

        Create.Index("IX_Triggers_Type").OnTable("Triggers").OnColumn("Type");
        Create.Index("IX_Triggers_AgentId").OnTable("Triggers").OnColumn("AgentId");
        Create.Index("IX_Triggers_ChatId").OnTable("Triggers").OnColumn("ChatId");

        // 14. WaitConditions
        Create.Table("WaitConditions")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey()
            .WithColumn("Type").AsInt32().NotNullable()
            .WithColumn("AgentId").AsGuid().NotNullable()
            .WithColumn("ChatId").AsGuid().NotNullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable()
            .WithColumn("CompletedAt").AsDateTime().Nullable()
            .WithColumn("SatisfiedByTriggerId").AsGuid().Nullable()
            .WithColumn("MessageAfterDateTime").AsDateTime().Nullable()
            .WithColumn("ToolCallId").AsString(200).Nullable();

        Create.Index("IX_WaitConditions_Type").OnTable("WaitConditions").OnColumn("Type");
        Create.Index("IX_WaitConditions_AgentId").OnTable("WaitConditions").OnColumn("AgentId");
        Create.Index("IX_WaitConditions_ChatId").OnTable("WaitConditions").OnColumn("ChatId");
        Create.Index("IX_WaitConditions_SatisfiedByTriggerId").OnTable("WaitConditions").OnColumn("SatisfiedByTriggerId");
    }

    public override void Down()
    {
        Delete.Table("WaitConditions");
        Delete.Table("Triggers");
        Delete.Table("RawLlmHttpCalls");
        Delete.Table("AgentThoughts");
        Delete.Table("Messages");
        Delete.Table("DirectMessageChannels");
        Delete.Table("McpServerConfigurationTools");
        Delete.Table("McpServerConfigurationAuthHeaders");
        Delete.Table("McpServerConfigurations");
        Delete.Table("Channels");
        Delete.Table("Agents");
        Delete.Table("PendingRegistrations");
        Delete.Table("Users");
        Delete.Table("Providers");
    }
}
