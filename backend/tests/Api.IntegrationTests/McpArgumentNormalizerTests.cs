using System.Text.Json;
using AzureOpsCrew.Api.Mcp;
using Xunit;

namespace AzureOpsCrew.Api.IntegrationTests;

/// <summary>
/// Tests for MCP argument normalization, validation, and auto-repair.
/// Addresses defect: MCP tool calls fail due to argument format mismatches.
/// </summary>
public class McpArgumentNormalizerTests
{
    #region NormalizeAndValidate Tests

    [Fact]
    public void NormalizeAndValidate_ParamsRemapping_RenamesParamsToParameters()
    {
        // Arrange: Schema expects "parameters", but model sent "params"
        var schema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                parameters = new { type = "object" }
            },
            required = new[] { "parameters" }
        });

        var arguments = JsonSerializer.SerializeToElement(new
        {
            // Model incorrectly used "params" instead of "parameters"
            @params = new { resourceGroup = "rg-test", workspace = "log-test" }
        });

        // Act
        var normalized = McpArgumentNormalizer.NormalizeAndValidate("azure_monitor_query", schema, arguments);

        // Assert: Should have remapped to "parameters"
        Assert.True(normalized.TryGetProperty("parameters", out var parameters));
        Assert.True(parameters.TryGetProperty("resourceGroup", out var rg));
        Assert.Equal("rg-test", rg.GetString());
    }

    [Fact]
    public void NormalizeAndValidate_FlatArgsWrapping_WrapsIntoProot()
    {
        // Arrange: Schema expects { parameters: {...} }, but model sent flat args
        var schema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        resourceGroup = new { type = "string" },
                        workspace = new { type = "string" }
                    }
                }
            },
            required = new[] { "parameters" }
        });

        var arguments = JsonSerializer.SerializeToElement(new
        {
            // Model sent flat args without wrapping
            resourceGroup = "rg-test",
            workspace = "log-test"
        });

        // Act
        var normalized = McpArgumentNormalizer.NormalizeAndValidate("azure_query", schema, arguments);

        // Assert: Should have wrapped into "parameters"
        Assert.True(normalized.TryGetProperty("parameters", out var parameters));
        Assert.True(parameters.TryGetProperty("resourceGroup", out _));
    }

    [Fact]
    public void NormalizeAndValidate_AlreadyCorrect_ReturnsUnchanged()
    {
        // Arrange: Arguments already in correct format
        var schema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                parameters = new { type = "object" }
            }
        });

        var arguments = JsonSerializer.SerializeToElement(new
        {
            parameters = new { query = "SELECT *" }
        });

        // Act
        var normalized = McpArgumentNormalizer.NormalizeAndValidate("azure_query", schema, arguments);

        // Assert: Should be unchanged
        Assert.True(normalized.TryGetProperty("parameters", out var parameters));
        Assert.True(parameters.TryGetProperty("query", out _));
    }

    #endregion

    #region ParseErrorAndSuggestRepair Tests

    [Fact]
    public void ParseErrorAndSuggestRepair_MissingParameters_SuggestsWrap()
    {
        // Arrange: Typical MCP error about missing parameters
        var errorResponse = JsonSerializer.SerializeToElement(new
        {
            error = new { message = "Missing Required parameters: resourceGroup, workspace" }
        });

        // Act
        var strategy = McpArgumentNormalizer.ParseErrorAndSuggestRepair("azure_monitor_query", errorResponse);

        // Assert
        Assert.NotNull(strategy);
        Assert.Equal(RepairType.WrapInParametersRoot, strategy.Type);
    }

    [Fact]
    public void ParseErrorAndSuggestRepair_WrapArgumentsError_SuggestsWrap()
    {
        // Arrange: Error explicitly asking to wrap in parameters
        var errorResponse = JsonSerializer.SerializeToElement(new
        {
            content = new[]
            {
                new { text = "Please wrap all command arguments into the root 'parameters' argument" }
            }
        });

        // Act
        var strategy = McpArgumentNormalizer.ParseErrorAndSuggestRepair("azure_command", errorResponse);

        // Assert
        Assert.NotNull(strategy);
        Assert.Equal(RepairType.WrapInParametersRoot, strategy.Type);
    }

    [Fact]
    public void ParseErrorAndSuggestRepair_MissingOptions_ExtractsFieldNames()
    {
        // Arrange: Typical Azure CLI-style error
        var errorResponse = JsonSerializer.SerializeToElement(new
        {
            error = new { message = "Missing Required options: --resource-group, --workspace, --table, --query" }
        });

        // Act
        var strategy = McpArgumentNormalizer.ParseErrorAndSuggestRepair("azure_monitor_query", errorResponse);

        // Assert
        Assert.NotNull(strategy);
        Assert.Equal(RepairType.ProvideMissingFields, strategy.Type);
        Assert.NotNull(strategy.MissingFields);
        Assert.Contains("resource-group", strategy.MissingFields);
        Assert.Contains("workspace", strategy.MissingFields);
    }

    #endregion

    #region ApplyRepair Tests

    [Fact]
    public void ApplyRepair_WrapInParametersRoot_WrapsCorrectly()
    {
        // Arrange
        var strategy = new RepairStrategy { Type = RepairType.WrapInParametersRoot };
        var originalArgs = JsonSerializer.SerializeToElement(new
        {
            resourceGroup = "rg-test",
            query = "SELECT *"
        });

        // Act
        var repaired = McpArgumentNormalizer.ApplyRepair(strategy, originalArgs);

        // Assert
        Assert.True(repaired.TryGetProperty("parameters", out var parameters));
        Assert.True(parameters.TryGetProperty("resourceGroup", out _));
        Assert.True(parameters.TryGetProperty("query", out _));
    }

    [Fact]
    public void ApplyRepair_InferCommandWrapper_WrapsWithCommand()
    {
        // Arrange
        var strategy = new RepairStrategy { Type = RepairType.InferCommandWrapper };
        var originalArgs = JsonSerializer.SerializeToElement(new
        {
            resourceGroup = "rg-test",
            query = "SELECT *"
        });

        // Act
        var repaired = McpArgumentNormalizer.ApplyRepair(strategy, originalArgs, "azure_monitor_query");

        // Assert
        Assert.True(repaired.TryGetProperty("command", out var command));
        Assert.Equal("monitor_query", command.GetString());
        Assert.True(repaired.TryGetProperty("args", out var args));
        Assert.True(args.TryGetProperty("resourceGroup", out _));
    }

    #endregion
}
