using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AzureOpsCrew.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Agent",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderAgentId = table.Column<string>(type: "TEXT", nullable: false),
                    ClientId = table.Column<int>(type: "INTEGER", nullable: false),
                    Info_Name = table.Column<string>(type: "TEXT", nullable: false),
                    Info_Prompt = table.Column<string>(type: "TEXT", nullable: false),
                    Info_Model = table.Column<string>(type: "TEXT", nullable: false),
                    Info_Description = table.Column<string>(type: "TEXT", nullable: true),
                    Info_AvaliableTools = table.Column<string>(type: "TEXT", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", nullable: false),
                    Color = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "#43b581"),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Agent", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Chat",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClientId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    ConversationId = table.Column<string>(type: "TEXT", nullable: true),
                    AgentIds = table.Column<string>(type: "TEXT", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chat", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Agent_ClientId",
                table: "Agent",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Chat_ClientId",
                table: "Chat",
                column: "ClientId");

            // Seed initial agents
            migrationBuilder.Sql(
                "INSERT INTO \"Agent\" (\"Id\", \"ProviderAgentId\", \"ClientId\", \"Info_Name\", \"Info_Prompt\", \"Info_Model\", \"Info_Description\", \"Info_AvaliableTools\", \"Provider\", \"Color\", \"DateCreated\") " +
                "VALUES ('6a5d8a20-1234-4000-a1b2-c3d4e5f6a7b8', 'manager', 1, 'Manager', " +
                "'You are a Manager AI assistant. You help with planning, priorities, resource allocation, team coordination, and delivery. You think in terms of goals, milestones, risks, and stakeholder communication. Keep answers actionable and concise. When you have tools available (showPipelineStatus, showWorkItems, showResourceInfo, showDeployment, showMetrics), use them proactively to present information visually instead of plain text.', " +
                "'gpt-4o-mini', 'Helps with planning, priorities, resource allocation, team coordination, and delivery', '[]', 'Local0', '#43b581', datetime('now'))");

            migrationBuilder.Sql(
                "INSERT INTO \"Agent\" (\"Id\", \"ProviderAgentId\", \"ClientId\", \"Info_Name\", \"Info_Prompt\", \"Info_Model\", \"Info_Description\", \"Info_AvaliableTools\", \"Provider\", \"Color\", \"DateCreated\") " +
                "VALUES ('7b6e9b30-2345-4111-b2c3-d4e5f6a7b8c9', 'azure-devops', 1, 'Azure DevOps', " +
                "'You are an Azure DevOps expert. You help with pipelines (YAML and classic), CI/CD, Azure Repos, Boards, Artifacts, Test Plans, and release management. You know branching strategies, approvals, variable groups, service connections, and Azure DevOps REST APIs. Give concrete, step-by-step guidance when asked. When you have tools available (showPipelineStatus, showWorkItems, showResourceInfo, showDeployment, showMetrics), use them proactively to present information visually instead of plain text.', " +
                "'gpt-4o-mini', 'Expert in Azure DevOps pipelines, CI/CD, repos, boards, artifacts, and release management', '[]', 'Local0', '#0078d4', datetime('now'))");

            migrationBuilder.Sql(
                "INSERT INTO \"Agent\" (\"Id\", \"ProviderAgentId\", \"ClientId\", \"Info_Name\", \"Info_Prompt\", \"Info_Model\", \"Info_Description\", \"Info_AvaliableTools\", \"Provider\", \"Color\", \"DateCreated\") " +
                "VALUES ('8c7f0c40-3456-4222-c3d4-e5f6a7b8c9d0', 'azure-dev', 1, 'Azure Dev', " +
                "'You are an Azure development expert. You help with building and deploying apps on Azure: App Service, Functions, Container Apps, AKS, Azure SDKs, identity (Microsoft Entra ID), storage, messaging, and serverless. You focus on code, configuration, and best practices for Azure-native development. When you have tools available (showPipelineStatus, showWorkItems, showResourceInfo, showDeployment, showMetrics), use them proactively to present information visually instead of plain text.', " +
                "'gpt-4o-mini', 'Expert in building and deploying apps on Azure: App Service, Functions, Container Apps, AKS, and more', '[]', 'Local0', '#00bcf2', datetime('now'))");

            // Seed initial chat
            migrationBuilder.Sql(
                "INSERT INTO \"Chat\" (\"Id\", \"ClientId\", \"Name\", \"Description\", \"ConversationId\", \"AgentIds\", \"DateCreated\") " +
                "VALUES ('a5d8a20a-1234-4000-a1b2-c3d4e5f6a7b9', 1, 'General', 'General discussion and collaboration', NULL, '6a5d8a20-1234-4000-a1b2-c3d4e5f6a7b8,7b6e9b30-2345-4111-b2c3-d4e5f6a7b8c9,8c7f0c40-3456-4222-c3d4-e5f6a7b8c9d0', datetime('now'))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Agent");

            migrationBuilder.DropTable(
                name: "Chat");
        }
    }
}
