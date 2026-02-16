using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AzureOpsCrew.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddSelectedModelsToProviderConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SelectedModels",
                table: "ProviderConfig",
                type: "TEXT",
                maxLength: 4000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SelectedModels",
                table: "ProviderConfig");
        }
    }
}
