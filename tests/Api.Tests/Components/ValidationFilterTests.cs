using System.ComponentModel.DataAnnotations;
using FluentAssertions;

namespace Api.Tests.Components;

public class ValidationFilterTests
{
    [Fact]
    public void TestDto_WithValidData_ShouldPassValidation()
    {
        // Arrange
        var dto = new TestDto { Name = "Valid" };
        var validationContext = new ValidationContext(dto);
        var validationResults = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(dto, validationContext, validationResults, true);

        // Assert
        isValid.Should().BeTrue();
        validationResults.Should().BeEmpty();
    }

    [Fact]
    public void TestDto_WithEmptyName_ShouldFailValidation()
    {
        // Arrange
        var dto = new TestDto { Name = "" };
        var validationContext = new ValidationContext(dto);
        var validationResults = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(dto, validationContext, validationResults, true);

        // Assert
        isValid.Should().BeFalse();
        validationResults.Should().NotBeEmpty();
    }

    [Fact]
    public void TestDto_WithTooShortName_ShouldFailValidation()
    {
        // Arrange
        var dto = new TestDto { Name = "ab" };
        var validationContext = new ValidationContext(dto);
        var validationResults = new List<ValidationResult>();

        //        // Act
        var isValid = Validator.TryValidateObject(dto, validationContext, validationResults, true);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void TestDto_WithTooLongName_ShouldFailValidation()
    {
        // Arrange
        var dto = new TestDto { Name = new string('a', 6) };
        var validationContext = new ValidationContext(dto);
        var validationResults = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(dto, validationContext, validationResults, true);

        // Assert
        isValid.Should().BeFalse();
    }

    private class TestDto
    {
        [Required]
        [StringLength(5, MinimumLength = 3)]
        public string Name { get; set; } = string.Empty;
    }
}
