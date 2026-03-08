using AzureOpsCrew.Domain.Tools;
using AzureOpsCrew.Infrastructure.Ai.ContextReduction;
using Microsoft.Extensions.AI;

namespace Infrastructure.Ai.Tests.ContextReduction;

public class TokenEstimatorTests
{
    private const double CharsPerToken = 4.0;
    private const double SafetyMargin = 1.0; // No safety margin for predictable tests
    private const double SafetyMarginWithMultiplier = 1.15;

    [Fact]
    public void EstimateTokens_NullString_ReturnsZero()
    {
        var result = TokenEstimator.EstimateTokens(null, CharsPerToken, SafetyMargin);
        Assert.Equal(0, result);
    }

    [Fact]
    public void EstimateTokens_EmptyString_ReturnsZero()
    {
        var result = TokenEstimator.EstimateTokens("", CharsPerToken, SafetyMargin);
        Assert.Equal(0, result);
    }

    [Fact]
    public void EstimateTokens_KnownString_ReturnsExpectedTokenCount()
    {
        // 20 chars / 4 chars per token = 5 tokens
        var result = TokenEstimator.EstimateTokens("12345678901234567890", CharsPerToken, SafetyMargin);
        Assert.Equal(5, result);
    }

    [Fact]
    public void EstimateTokens_SafetyMarginIsApplied()
    {
        // 20 chars / 4 chars per token * 1.15 = 5.75 → ceil = 6
        var result = TokenEstimator.EstimateTokens("12345678901234567890", CharsPerToken, SafetyMarginWithMultiplier);
        Assert.Equal(6, result);
    }

    [Fact]
    public void EstimateMessageTokens_IncludesOverhead()
    {
        // Message with 20-char text: 5 tokens for text + 4 overhead = 9
        var message = new ChatMessage(ChatRole.User, "12345678901234567890");
        var result = TokenEstimator.EstimateMessageTokens(message, CharsPerToken, SafetyMargin);
        Assert.Equal(9, result);
    }

    [Fact]
    public void EstimateMessagesTokens_SumsCorrectly()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "12345678901234567890"),       // 5 + 4 = 9
            new(ChatRole.Assistant, "12345678901234567890"),   // 5 + 4 = 9
        };

        var result = TokenEstimator.EstimateMessagesTokens(messages, CharsPerToken, SafetyMargin);
        Assert.Equal(18, result);
    }

    [Fact]
    public void EstimateToolSchemasTokens_EmptyList_ReturnsZero()
    {
        var result = TokenEstimator.EstimateToolSchemasTokens([], CharsPerToken, SafetyMargin);
        Assert.Equal(0, result);
    }

    [Fact]
    public void EstimateToolSchemasTokens_PopulatedList_EstimatesAllFields()
    {
        var tools = new List<ToolDeclaration>
        {
            new()
            {
                Name = "test",          // 4 chars = 1 token
                Description = "desc",   // 4 chars = 1 token
                JsonSchema = "{}",      // 2 chars = 1 token (ceil)
                ReturnJsonSchema = "{}" // 2 chars = 1 token (ceil)
            }
        };

        var result = TokenEstimator.EstimateToolSchemasTokens(tools, CharsPerToken, SafetyMargin);
        Assert.True(result > 0);
    }

    [Fact]
    public void EstimateSystemPromptTokens_EstimatesCorrectly()
    {
        var result = TokenEstimator.EstimateSystemPromptTokens("12345678901234567890", CharsPerToken, SafetyMargin);
        Assert.Equal(5, result);
    }

    [Fact]
    public void EstimateSystemPromptTokens_Null_ReturnsZero()
    {
        var result = TokenEstimator.EstimateSystemPromptTokens(null, CharsPerToken, SafetyMargin);
        Assert.Equal(0, result);
    }
}
