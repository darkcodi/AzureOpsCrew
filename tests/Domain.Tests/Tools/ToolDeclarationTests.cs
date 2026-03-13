using System.Text.Json;
using AzureOpsCrew.Domain.Tools;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace Domain.Tests.Tools;

public class ToolDeclarationTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaultValues()
    {
        // Act
        var declaration = new ToolDeclaration();

        // Assert
        declaration.Name.Should().BeEmpty();
        declaration.Description.Should().BeEmpty();
        declaration.JsonSchema.Should().Be("{}");
        declaration.ReturnJsonSchema.Should().Be("{}");
        declaration.ToolType.Should().Be(ToolType.BackEnd);
        declaration.McpServerConfigurationId.Should().BeNull();
    }

    [Fact]
    public void Constructor_ShouldSetAllProperties()
    {
        // Arrange
        var name = "test_tool";
        var description = "A test tool";
        var jsonSchema = "{\"type\":\"object\"}";
        var returnJsonSchema = "{\"type\":\"string\"}";
        var toolType = ToolType.FrontEnd;
        var mcpServerConfigId = Guid.NewGuid();

        // Act
        var declaration = new ToolDeclaration
        {
            Name = name,
            Description = description,
            JsonSchema = jsonSchema,
            ReturnJsonSchema = returnJsonSchema,
            ToolType = toolType,
            McpServerConfigurationId = mcpServerConfigId
        };

        // Assert
        declaration.Name.Should().Be(name);
        declaration.Description.Should().Be(description);
        declaration.JsonSchema.Should().Be(jsonSchema);
        declaration.ReturnJsonSchema.Should().Be(returnJsonSchema);
        declaration.ToolType.Should().Be(toolType);
        declaration.McpServerConfigurationId.Should().Be(mcpServerConfigId);
    }

    [Theory]
    [InlineData(ToolType.BackEnd)]
    [InlineData(ToolType.FrontEnd)]
    [InlineData(ToolType.McpServer)]
    public void ToolType_ShouldAcceptAllTypes(ToolType toolType)
    {
        // Arrange & Act
        var declaration = new ToolDeclaration
        {
            ToolType = toolType
        };

        // Assert
        declaration.ToolType.Should().Be(toolType);
    }

    [Fact]
    public void ToAiFunctionDeclaration_ShouldCreateValidFunctionDeclaration()
    {
        // Arrange
        var declaration = new ToolDeclaration
        {
            Name = "deploy_application",
            Description = "Deploys an application to the specified environment",
            JsonSchema = """
                {
                    "type": "object",
                    "properties": {
                        "environment": {
                            "type": "string",
                            "enum": ["dev", "staging", "production"]
                        }
                    },
                    "required": ["environment"]
                }
                """,
            ReturnJsonSchema = """
                {
                    "type": "object",
                    "properties": {
                        "success": {"type": "boolean"},
                        "deploymentId": {"type": "string"}
                    }
                }
                """,
            ToolType = ToolType.BackEnd
        };

        // Act
        var aiFunction = declaration.ToAiFunctionDeclaration();

        // Assert
        aiFunction.Name.Should().Be("deploy_application");
        aiFunction.Description.Should().Be("Deploys an application to the specified environment");
    }

    [Fact]
    public void ToAiFunctionDeclaration_ShouldHandleEmptySchemas()
    {
        // Arrange
        var declaration = new ToolDeclaration
        {
            Name = "simple_tool",
            Description = "A simple tool",
            JsonSchema = "{}",
            ReturnJsonSchema = "{}"
        };

        // Act
        var aiFunction = declaration.ToAiFunctionDeclaration();

        // Assert
        aiFunction.Name.Should().Be("simple_tool");
        aiFunction.Description.Should().Be("A simple tool");
    }

    [Fact]
    public void FormatToolDeclaration_ShouldFormatCorrectly()
    {
        // Arrange
        var declaration = new ToolDeclaration
        {
            Name = "get_weather",
            Description = "Gets the current weather for a location",
            JsonSchema = "{\"type\":\"object\"}",
            ReturnJsonSchema = "{\"type\":\"string\"}",
            ToolType = ToolType.BackEnd
        };

        // Act
        var formatted = declaration.FormatToolDeclaration();

        // Assert
        formatted.Should().Contain("Tool Name: get_weather");
        formatted.Should().Contain("Tool Description: Gets the current weather for a location");
        formatted.Should().Contain("Tool Type: BackEnd");
        formatted.Should().Contain("Tool JSON Schema: {\"type\":\"object\"}");
        formatted.Should().Contain("Tool Return JSON Schema: {\"type\":\"string\"}");
    }

    [Fact]
    public void FormatToolDeclaration_ShouldHandleAllToolTypes()
    {
        // Arrange
        var backEndTool = new ToolDeclaration { Name = "backend_tool", ToolType = ToolType.BackEnd };
        var frontEndTool = new ToolDeclaration { Name = "frontend_tool", ToolType = ToolType.FrontEnd };
        var mcpServerTool = new ToolDeclaration { Name = "mcp_tool", ToolType = ToolType.McpServer };

        // Act
        var backEndFormatted = backEndTool.FormatToolDeclaration();
        var frontEndFormatted = frontEndTool.FormatToolDeclaration();
        var mcpFormatted = mcpServerTool.FormatToolDeclaration();

        // Assert
        backEndFormatted.Should().Contain("Tool Type: BackEnd");
        frontEndFormatted.Should().Contain("Tool Type: FrontEnd");
        mcpFormatted.Should().Contain("Tool Type: McpServer");
    }

    [Fact]
    public void McpServerConfigurationId_ShouldBeNull_ForNonMcpTools()
    {
        // Arrange & Act
        var backEndTool = new ToolDeclaration
        {
            Name = "backend_tool",
            ToolType = ToolType.BackEnd,
            McpServerConfigurationId = null
        };
        var frontEndTool = new ToolDeclaration
        {
            Name = "frontend_tool",
            ToolType = ToolType.FrontEnd,
            McpServerConfigurationId = null
        };

        // Assert
        backEndTool.McpServerConfigurationId.Should().BeNull();
        frontEndTool.McpServerConfigurationId.Should().BeNull();
    }

    [Fact]
    public void McpServerConfigurationId_ShouldBeSet_ForMcpTools()
    {
        // Arrange
        var mcpServerId = Guid.NewGuid();

        // Act
        var mcpTool = new ToolDeclaration
        {
            Name = "mcp_tool",
            ToolType = ToolType.McpServer,
            McpServerConfigurationId = mcpServerId
        };

        // Assert
        mcpTool.McpServerConfigurationId.Should().Be(mcpServerId);
    }

    [Fact]
    public void ToAiFunctionDeclaration_ShouldHandleComplexJsonSchema()
    {
        // Arrange
        var declaration = new ToolDeclaration
        {
            Name = "complex_tool",
            Description = "A tool with complex schema",
            JsonSchema = """
                {
                    "type": "object",
                    "properties": {
                        "name": {"type": "string"},
                        "age": {"type": "integer"},
                        "tags": {
                            "type": "array",
                            "items": {"type": "string"}
                        },
                        "metadata": {
                            "type": "object",
                            "properties": {
                                "created": {"type": "string", "format": "date-time"}
                            }
                        }
                    },
                    "required": ["name", "age"]
                }
                """,
            ReturnJsonSchema = "{}"
        };

        // Act
        var aiFunction = declaration.ToAiFunctionDeclaration();

        // Assert
        aiFunction.Name.Should().Be("complex_tool");
        aiFunction.Description.Should().Be("A tool with complex schema");
    }
}
