using System.Text.Json;
using AzureOpsCrew.Domain.Utils;
using FluentAssertions;

namespace Domain.Tests.Utils;

public class JsonUtilsTests
{
    [Fact]
    public void Schema_ShouldParseValidJson()
    {
        // Arrange
        var json = """
            {
                "type": "object",
                "properties": {
                    "name": {"type": "string"},
                    "age": {"type": "integer"}
                }
            }
            """;

        // Act
        var result = JsonUtils.Schema(json);

        // Assert
        result.ValueKind.Should().Be(JsonValueKind.Object);
        result.GetProperty("type").GetString().Should().Be("object");
        result.GetProperty("properties").ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public void Schema_ShouldParseEmptyObject()
    {
        // Arrange
        var json = "{}";

        // Act
        var result = JsonUtils.Schema(json);

        // Assert
        result.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public void Schema_ShouldParseNestedObjects()
    {
        // Arrange
        var json = """
            {
                "level1": {
                    "level2": {
                        "level3": {
                            "value": "deep"
                        }
                    }
                }
            }
            """;

        // Act
        var result = JsonUtils.Schema(json);

        // Assert
        result.GetProperty("level1").GetProperty("level2").GetProperty("level3").GetProperty("value").GetString().Should().Be("deep");
    }

    [Fact]
    public void Schema_ShouldParseArrays()
    {
        // Arrange
        var json = """
            {
                "items": [1, 2, 3, 4, 5]
            }
            """;

        // Act
        var result = JsonUtils.Schema(json);

        // Assert
        result.GetProperty("items").ValueKind.Should().Be(JsonValueKind.Array);
        result.GetProperty("items").GetArrayLength().Should().Be(5);
    }

    [Fact]
    public void Schema_ShouldParseStringValues()
    {
        // Arrange
        var json = """
            {
                "name": "John Doe",
                "email": "john@example.com"
            }
            """;

        // Act
        var result = JsonUtils.Schema(json);

        // Assert
        result.GetProperty("name").GetString().Should().Be("John Doe");
        result.GetProperty("email").GetString().Should().Be("john@example.com");
    }

    [Fact]
    public void Schema_ShouldParseNumericValues()
    {
        // Arrange
        var json = """
            {
                "integer": 42,
                "negative": -10,
                "float": 3.14,
                "scientific": 1.5e10
            }
            """;

        // Act
        var result = JsonUtils.Schema(json);

        // Assert
        result.GetProperty("integer").GetInt32().Should().Be(42);
        result.GetProperty("negative").GetInt32().Should().Be(-10);
        result.GetProperty("float").GetDouble().Should().BeApproximately(3.14, 0.01);
    }

    [Fact]
    public void Schema_ShouldParseBooleanValues()
    {
        // Arrange
        var json = """
            {
                "isActive": true,
                "isDeleted": false
            }
            """;

        // Act
        var result = JsonUtils.Schema(json);

        // Assert
        result.GetProperty("isActive").GetBoolean().Should().BeTrue();
        result.GetProperty("isDeleted").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public void Schema_ShouldParseNullValues()
    {
        // Arrange
        var json = """
            {
                "value": null
            }
            """;

        // Act
        var result = JsonUtils.Schema(json);

        // Assert
        result.GetProperty("value").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void Schema_ShouldParseComplexNestedStructure()
    {
        // Arrange
        var json = """
            {
                "type": "object",
                "properties": {
                    "name": {
                        "type": "string",
                        "description": "The person's name"
                    },
                    "age": {
                        "type": "integer",
                        "minimum": 0,
                        "maximum": 150
                    },
                    "hobbies": {
                        "type": "array",
                        "items": {"type": "string"}
                    }
                },
                "required": ["name", "age"]
            }
            """;

        // Act
        var result = JsonUtils.Schema(json);

        // Assert
        result.GetProperty("properties").GetProperty("name").GetProperty("type").GetString().Should().Be("string");
        result.GetProperty("properties").GetProperty("age").GetProperty("minimum").GetInt32().Should().Be(0);
        result.GetProperty("properties").GetProperty("hobbies").GetProperty("type").GetString().Should().Be("array");
    }

    [Fact]
    public void Schema_ShouldHandleEscapedCharacters()
    {
        // Arrange
        var json = """
            {
                "path": "C:\\Users\\Test\\file.txt",
                "quote": "She said \"Hello\"",
                "newline": "Line 1\nLine 2"
            }
            """;

        // Act
        var result = JsonUtils.Schema(json);

        // Assert
        result.GetProperty("path").GetString().Should().Be("C:\\Users\\Test\\file.txt");
        result.GetProperty("quote").GetString().Should().Contain("\"Hello\"");
    }

    [Fact]
    public void Schema_ShouldHandleUnicodeEscapes()
    {
        // Arrange
        var json = """
            {
                "emoji": "\ud83d\ude00",
                "chinese": "\u4e2d\u6587"
            }
            """;

        // Act
        var result = JsonUtils.Schema(json);

        // Assert
        result.GetProperty("emoji").GetString().Should().NotBeEmpty();
        result.GetProperty("chinese").GetString().Should().NotBeEmpty();
    }

    [Fact]
    public void Schema_ShouldReturnClonedElement()
    {
        // Arrange
        var json = """
            {
                "value": "test"
            }
            """;

        // Act
        var result = JsonUtils.Schema(json);
        result.GetProperty("value").GetString(); // Access the value

        // Assert - Cloned element should still be accessible
        result.GetProperty("value").GetString().Should().Be("test");
    }

    [Fact]
    public void Schema_ShouldHandleMinifiedJson()
    {
        // Arrange
        var json = """{"a":1,"b":2,"c":{"d":3}}""";

        // Act
        var result = JsonUtils.Schema(json);

        // Assert
        result.GetProperty("a").GetInt32().Should().Be(1);
        result.GetProperty("b").GetInt32().Should().Be(2);
        result.GetProperty("c").GetProperty("d").GetInt32().Should().Be(3);
    }

    [Fact]
    public void Schema_ShouldHandlePrettyPrintedJson()
    {
        // Arrange
        var json = """
            {
                "a": 1,
                "b": 2,
                "c": {
                    "d": 3
                }
            }
            """;

        // Act
        var result = JsonUtils.Schema(json);

        // Assert
        result.GetProperty("a").GetInt32().Should().Be(1);
    }

    [Fact]
    public void Schema_ShouldThrowException_ForInvalidJson()
    {
        // Arrange
        var invalidJson = "{invalid json}";

        // Act
        var act = () => JsonUtils.Schema(invalidJson);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Schema_ShouldThrowException_ForIncompleteJson()
    {
        // Arrange
        var incompleteJson = "{\"key\": \"value\"";

        // Act
        var act = () => JsonUtils.Schema(incompleteJson);

        // Assert
        act.Should().Throw<JsonException>();
    }
}
