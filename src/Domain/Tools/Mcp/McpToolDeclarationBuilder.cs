using AzureOpsCrew.Domain.McpServerConfigurations;

namespace AzureOpsCrew.Domain.Tools.Mcp;

// Converts enabled MCP server tool configurations into ToolDeclarations that can be passed to the LLM as available tools.
public static class McpToolDeclarationBuilder
{
    public static List<ToolDeclaration> Build(List<McpServerConfiguration> mcpServers)
    {
        var declarations = new List<ToolDeclaration>();

        foreach (var server in mcpServers)
        {
            if (!server.IsEnabled)
                continue;

            foreach (var tool in server.Tools)
            {
                if (!tool.IsEnabled)
                    continue;

                declarations.Add(new ToolDeclaration
                {
                    Name = tool.Name,
                    Description = FormatDescription(server.Name, tool.Description),
                    JsonSchema = tool.InputSchemaJson ?? "{}",
                    ReturnJsonSchema = tool.OutputSchemaJson ?? "{}",
                    ToolType = ToolType.McpServer,
                    McpServerConfigurationId = server.Id,
                });
            }
        }

        return declarations;
    }

    private static string FormatDescription(string serverName, string? toolDescription)
    {
        var desc = toolDescription ?? string.Empty;
        return $"[MCP: {serverName}] {desc}".Trim();
    }
}
