using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AzureOpsCrew.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddModelsCountToProviderConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ModelsCount",
                table: "ProviderConfig",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModelsCount",
                table: "ProviderConfig");
        }
    }
}
