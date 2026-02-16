using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AzureOpsCrew.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class RenameProviderConfigToProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "ProviderConfig",
                newName: "Provider");

            migrationBuilder.RenameIndex(
                name: "IX_ProviderConfig_ClientId",
                newName: "IX_Provider_ClientId",
                table: "Provider");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "Provider",
                newName: "ProviderConfig");

            migrationBuilder.RenameIndex(
                name: "IX_Provider_ClientId",
                newName: "IX_ProviderConfig_ClientId",
                table: "ProviderConfig");
        }
    }
}
