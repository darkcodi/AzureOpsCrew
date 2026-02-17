using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AzureOpsCrew.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class ChangeAgentProviderToProviderId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add new ProviderId column as nullable TEXT (for Guid storage)
            migrationBuilder.AddColumn<string>(
                name: "ProviderId",
                table: "Agent",
                type: "TEXT",
                nullable: true);

            // Since old Provider values (Local0, Local1, etc.) are not valid Guids,
            // we'll set ProviderId to null for all existing agents.
            // Agents will need to be recreated or have their provider set manually.
            // Alternatively, you could map old values to specific provider Guids here.

            // Drop the old Provider column
            migrationBuilder.DropColumn(
                name: "Provider",
                table: "Agent");

            // Make ProviderId required
            migrationBuilder.AlterColumn<string>(
                name: "ProviderId",
                table: "Agent",
                type: "TEXT",
                nullable: false,
                defaultValue: "00000000-0000-0000-0000-000000000000");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert: Add back Provider column
            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "Agent",
                type: "TEXT",
                nullable: false,
                defaultValue: "Local0");

            // Drop ProviderId
            migrationBuilder.DropColumn(
                name: "ProviderId",
                table: "Agent");
        }
    }
}
