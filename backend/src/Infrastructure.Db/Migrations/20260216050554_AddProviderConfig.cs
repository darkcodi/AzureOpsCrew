using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AzureOpsCrew.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProviderConfig",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ClientId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ProviderType = table.Column<string>(type: "TEXT", nullable: false),
                    ApiKey = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ApiEndpoint = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    DefaultModel = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DateModified = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderConfig", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderConfig_ClientId",
                table: "ProviderConfig",
                column: "ClientId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProviderConfig");
        }
    }
}
