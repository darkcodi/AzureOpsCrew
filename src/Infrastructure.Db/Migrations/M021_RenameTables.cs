using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_02_28_16_30_00, "RenameTables")]
public class M021_RenameTables : Migration
{
    public override void Up()
    {
        Rename.Table("LlmChatMessages").To("AgentThoughts");
        Rename.Table("Agent").To("Agents");
        Rename.Table("AppUser").To("Users");
        Rename.Table("Channel").To("Channels");
        Rename.Table("PendingRegistration").To("PendingRegistrations");
        Rename.Table("Provider").To("Providers");
        Rename.Table("Dms").To("DirectMessageChannels");
    }

    public override void Down()
    {
        Rename.Table("AgentThoughts").To("LlmChatMessages");
        Rename.Table("Agents").To("Agent");
        Rename.Table("Users").To("AppUser");
        Rename.Table("Channels").To("Channel");
        Rename.Table("PendingRegistrations").To("PendingRegistration");
        Rename.Table("Providers").To("Provider");
        Rename.Table("DirectMessageChannels").To("Dms");
    }
}
