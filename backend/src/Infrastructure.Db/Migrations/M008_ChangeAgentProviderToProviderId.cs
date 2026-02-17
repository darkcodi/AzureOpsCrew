using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_02_17_00_25_46, "Change Agent Provider to ProviderId (no-op - already in M001)")]
public class M008_ChangeAgentProviderToProviderId : Migration
{
    public override void Up()
    {
        // No-op - the ProviderId column was already created in M001
        // This migration exists only to maintain the migration history chain
    }

    public override void Down()
    {
        // No-op
    }
}
