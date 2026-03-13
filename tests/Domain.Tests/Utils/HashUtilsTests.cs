using AzureOpsCrew.Domain.Utils;
using FluentAssertions;

namespace Domain.Tests.Utils;

public class HashUtilsTests
{
    [Fact]
    public void HashStringToGuid_ShouldReturnEmptyGuid_ForEmptyString()
    {
        // Arrange & Act
        var result = HashUtils.HashStringToGuid("");

        // Assert
        result.Should().Be(Guid.Empty);
    }

    [Fact]
    public void HashStringToGuid_ShouldReturnEmptyGuid_ForNullString()
    {
        // Arrange & Act
        var result = HashUtils.HashStringToGuid(null!);

        // Assert
        result.Should().Be(Guid.Empty);
    }

    [Fact]
    public void HashStringToGuid_ShouldReturnSameGuid_ForSameInput()
    {
        // Arrange
        var input = "test-string";

        // Act
        var result1 = HashUtils.HashStringToGuid(input);
        var result2 = HashUtils.HashStringToGuid(input);

        // Assert
        result1.Should().Be(result2);
    }

    [Fact]
    public void HashStringToGuid_ShouldReturnDifferentGuids_ForDifferentInputs()
    {
        // Arrange
        var input1 = "test-string-1";
        var input2 = "test-string-2";

        // Act
        var result1 = HashUtils.HashStringToGuid(input1);
        var result2 = HashUtils.HashStringToGuid(input2);

        // Assert
        result1.Should().NotBe(result2);
    }

    [Fact]
    public void HashStringToGuid_ShouldHandleLongStrings()
    {
        // Arrange
        var longInput = new string('a', 10000);

        // Act
        var result = HashUtils.HashStringToGuid(longInput);

        // Assert
        result.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void HashStringToGuid_ShouldHandleSpecialCharacters()
    {
        // Arrange
        var specialChars = "!@#$%^&*()_+-=[]{}|;':\",./<>?`~";

        // Act
        var result = HashUtils.HashStringToGuid(specialChars);

        // Assert
        result.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void HashStringToGuid_ShouldHandleUnicodeCharacters()
    {
        // Arrange
        var unicodeInput = "Hello 世界 🌍";

        // Act
        var result = HashUtils.HashStringToGuid(unicodeInput);

        // Assert
        result.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void HashStringToGuid_ShouldBeDeterministic()
    {
        // Arrange
        var input = "deterministic-test";
        var expected = new Guid("a5e5e5e5-5e5e-5e5e-5e5e-5e5e5e5e5e5e"); // Example, actual value depends on MurmurHash

        // Act
        var result1 = HashUtils.HashStringToGuid(input);
        var result2 = HashUtils.HashStringToGuid(input);
        var result3 = HashUtils.HashStringToGuid(input);

        // Assert
        result1.Should().Be(result2);
        result2.Should().Be(result3);
    }

    [Fact]
    public void HashStringToInt_ShouldReturnZero_ForEmptyString()
    {
        // Arrange & Act
        var result = HashUtils.HashStringToInt("");

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void HashStringToInt_ShouldReturnZero_ForNullString()
    {
        // Arrange & Act
        var result = HashUtils.HashStringToInt(null!);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void HashStringToInt_ShouldReturnSameInt_ForSameInput()
    {
        // Arrange
        var input = "test-string";

        // Act
        var result1 = HashUtils.HashStringToInt(input);
        var result2 = HashUtils.HashStringToInt(input);

        // Assert
        result1.Should().Be(result2);
    }

    [Fact]
    public void HashStringToInt_ShouldReturnDifferentInts_ForDifferentInputs()
    {
        // Arrange
        var input1 = "test-string-1";
        var input2 = "test-string-2";

        // Act
        var result1 = HashUtils.HashStringToInt(input1);
        var result2 = HashUtils.HashStringToInt(input2);

        // Assert
        result1.Should().NotBe(result2);
    }

    [Fact]
    public void HashStringToInt_ShouldHandleLongStrings()
    {
        // Arrange
        var longInput = new string('b', 10000);

        // Act
        var result = HashUtils.HashStringToInt(longInput);

        // Assert
        result.Should().NotBe(0);
    }

    [Fact]
    public void HashStringToInt_ShouldHandleSpecialCharacters()
    {
        // Arrange
        var specialChars = "!@#$%^&*()_+-=[]{}|;':\",./<>?`~";

        // Act
        var result = HashUtils.HashStringToInt(specialChars);

        // Assert
        result.Should().NotBe(0);
    }

    [Fact]
    public void HashStringToInt_ShouldHandleUnicodeCharacters()
    {
        // Arrange
        var unicodeInput = "Привет мир 🌍";

        // Act
        var result = HashUtils.HashStringToInt(unicodeInput);

        // Assert
        result.Should().NotBe(0);
    }

    [Fact]
    public void HashStringToInt_ShouldBeDeterministic()
    {
        // Arrange
        var input = "deterministic-test-int";

        // Act
        var result1 = HashUtils.HashStringToInt(input);
        var result2 = HashUtils.HashStringToInt(input);
        var result3 = HashUtils.HashStringToInt(input);

        // Assert
        result1.Should().Be(result2);
        result2.Should().Be(result3);
    }

    [Fact]
    public void HashStringToInt_CanProduceNegativeValues()
    {
        // Arrange
        var inputs = new[] { "a", "b", "c", "d", "e", "f", "g", "h", "i", "j" };

        // Act
        var results = inputs.Select(HashUtils.HashStringToInt).ToList();

        // Assert
        results.Should().Contain(r => r < 0);
    }

    [Fact]
    public void HashStringToGuid_And_HashStringToInt_ShouldProduceDifferentHashTypesForSameInput()
    {
        // Arrange
        var input = "test-string";

        // Act
        var guidResult = HashUtils.HashStringToGuid(input);
        var intResult = HashUtils.HashStringToInt(input);

        // Assert
        guidResult.Should().NotBeEmpty();
        intResult.Should().NotBe(0);
        // Both hashes should produce valid non-default values for the same input
        // Note: They use different hash algorithms (MurmurHash128 vs MurmurHash32)
        // so the values won't directly correspond, but both should be non-zero/non-empty
    }
}
