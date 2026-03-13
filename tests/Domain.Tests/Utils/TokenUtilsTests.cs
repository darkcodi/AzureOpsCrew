using AzureOpsCrew.Domain.Utils;
using FluentAssertions;

namespace Domain.Tests.Utils;

public class TokenUtilsTests
{
    [Fact]
    public void EstimateTokensCount_ShouldReturnZero_ForEmptyString()
    {
        // Arrange & Act
        var result = TokenUtils.EstimateTokensCount("");

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void EstimateTokensCount_ShouldReturnZero_ForNullString()
    {
        // Arrange & Act
        var result = TokenUtils.EstimateTokensCount(null!);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void EstimateTokensCount_ShouldReturnPositive_ForSingleWord()
    {
        // Arrange & Act
        var result = TokenUtils.EstimateTokensCount("hello");

        // Assert
        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public void EstimateTokensCount_ShouldReturnHigherCount_ForLongerText()
    {
        // Arrange
        var shortText = "hello";
        var longText = "hello world, how are you doing today? I hope you are having a great time!";

        // Act
        var shortCount = TokenUtils.EstimateTokensCount(shortText);
        var longCount = TokenUtils.EstimateTokensCount(longText);

        // Assert
        longCount.Should().BeGreaterThan(shortCount);
    }

    [Fact]
    public void EstimateTokensCount_ShouldHandleMultipleLines()
    {
        // Arrange
        var multiLineText = """
            Line 1: First line of text
            Line 2: Second line of text
            Line 3: Third line of text
            """;

        // Act
        var result = TokenUtils.EstimateTokensCount(multiLineText);

        // Assert
        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public void EstimateTokensCount_ShouldHandleSpecialCharacters()
    {
        // Arrange
        var specialCharsText = "Hello! @#$%^&*()_+-=[]{}|;':\",./<>?`~";

        // Act
        var result = TokenUtils.EstimateTokensCount(specialCharsText);

        // Assert
        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public void EstimateTokensCount_ShouldHandleUnicodeCharacters()
    {
        // Arrange
        var unicodeText = "Hello 世界 🌍 Привет мир";

        // Act
        var result = TokenUtils.EstimateTokensCount(unicodeText);

        // Assert
        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public void EstimateTokensCount_ShouldHandleCodeSnippets()
    {
        // Arrange
        var codeSnippet = """
            public class TestClass {
                public void TestMethod() {
                    var x = 1 + 1;
                    Console.WriteLine(x);
                }
            }
            """;

        // Act
        var result = TokenUtils.EstimateTokensCount(codeSnippet);

        // Assert
        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public void EstimateTokensCount_ShouldHandleJson()
    {
        // Arrange
        var jsonText = """
            {
                "name": "John Doe",
                "age": 30,
                "email": "john@example.com",
                "address": {
                    "street": "123 Main St",
                    "city": "New York"
                }
            }
            """;

        // Act
        var result = TokenUtils.EstimateTokensCount(jsonText);

        // Assert
        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public void EstimateTokensCount_ShouldHandleVeryLongText()
    {
        // Arrange
        var longText = string.Join(" ", Enumerable.Repeat("word", 10000));

        // Act
        var result = TokenUtils.EstimateTokensCount(longText);

        // Assert
        result.Should().BeGreaterOrEqualTo(10000);
    }

    [Fact]
    public void EstimateTokensCount_ShouldBeDeterministic()
    {
        // Arrange
        var text = "This is a test string for token counting.";

        // Act
        var result1 = TokenUtils.EstimateTokensCount(text);
        var result2 = TokenUtils.EstimateTokensCount(text);
        var result3 = TokenUtils.EstimateTokensCount(text);

        // Assert
        result1.Should().Be(result2);
        result2.Should().Be(result3);
    }

    [Fact]
    public void EstimateTokensCount_ShouldHandleNumbers()
    {
        // Arrange
        var numbersText = "12345 67890 3.14159 1000000";

        // Act
        var result = TokenUtils.EstimateTokensCount(numbersText);

        // Assert
        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public void EstimateTokensCount_ShouldHandleWhitespace()
    {
        // Arrange
        var whitespaceText = "   \t\n\r   ";

        // Act
        var result = TokenUtils.EstimateTokensCount(whitespaceText);

        // Assert
        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public void EstimateTokensCount_ShouldHandleUrls()
    {
        // Arrange
        var urlText = "Check out https://example.com/path?query=value for more info";

        // Act
        var result = TokenUtils.EstimateTokensCount(urlText);

        // Assert
        result.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData("Hello", 1)] // Approximately 1 token
    [InlineData("Hello world", 2)] // Approximately 2 tokens
    [InlineData("The quick brown fox jumps over the lazy dog", 9)] // Approximately 9 tokens
    public void EstimateTokensCount_ShouldProvideReasonableEstimates(string text, int expectedApproximateTokens)
    {
        // Act
        var result = TokenUtils.EstimateTokensCount(text);

        // Assert
        // Allow for some variance in tokenization
        result.Should().BeGreaterOrEqualTo(expectedApproximateTokens - 1);
        result.Should().BeLessOrEqualTo(expectedApproximateTokens + 2);
    }
}
