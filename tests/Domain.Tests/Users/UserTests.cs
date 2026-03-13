using AzureOpsCrew.Domain.Users;
using FluentAssertions;

namespace Domain.Tests.Users;

public class UserTests
{
    [Fact]
    public void Constructor_ShouldInitializeAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var email = "test@example.com";
        var normalizedEmail = "TEST@EXAMPLE.COM";
        var passwordHash = "hashedPassword";
        var username = "testuser";
        var normalizedUsername = "TESTUSER";

        // Act
        var user = new User(id, email, normalizedEmail, passwordHash, username, normalizedUsername);

        // Assert
        user.Id.Should().Be(id);
        user.Email.Should().Be(email);
        user.NormalizedEmail.Should().Be(normalizedEmail);
        user.PasswordHash.Should().Be(passwordHash);
        user.Username.Should().Be(username);
        user.NormalizedUsername.Should().Be(normalizedUsername);
        user.IsActive.Should().BeTrue(); // Default value
        user.DateCreated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        user.DateModified.Should().BeNull();
        user.LastLoginAt.Should().BeNull();
    }

    [Fact]
    public void UpdateUsername_ShouldUpdateUsernameAndNormalizedUsername()
    {
        // Arrange
        var user = new User(
            Guid.NewGuid(),
            "test@example.com",
            "TEST@EXAMPLE.COM",
            "hashedPassword",
            "oldusername",
            "OLDUSERNAME");

        // Act
        user.UpdateUsername("newusername", "NEWUSERNAME");

        // Assert
        user.Username.Should().Be("newusername");
        user.NormalizedUsername.Should().Be("NEWUSERNAME");
        user.DateModified.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void UpdateUsername_ShouldSetDateModified_WhenPreviouslyNull()
    {
        // Arrange
        var user = new User(
            Guid.NewGuid(),
            "test@example.com",
            "TEST@EXAMPLE.COM",
            "hashedPassword",
            "oldusername",
            "OLDUSERNAME");

        // Act
        user.UpdateUsername("newusername", "NEWUSERNAME");

        // Assert
        user.DateModified.Should().NotBeNull();
        user.DateModified.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void UpdatePasswordHash_ShouldUpdatePasswordHash()
    {
        // Arrange
        var user = new User(
            Guid.NewGuid(),
            "test@example.com",
            "TEST@EXAMPLE.COM",
            "oldHash",
            "testuser",
            "TESTUSER");
        var newHash = "newHash";

        // Act
        user.UpdatePasswordHash(newHash);

        // Assert
        user.PasswordHash.Should().Be(newHash);
        user.DateModified.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void MarkLogin_ShouldSetLastLoginAt()
    {
        // Arrange
        var user = new User(
            Guid.NewGuid(),
            "test@example.com",
            "TEST@EXAMPLE.COM",
            "hashedPassword",
            "testuser",
            "TESTUSER");

        // Act
        user.MarkLogin();

        // Assert
        user.LastLoginAt.Should().NotBeNull();
        user.LastLoginAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void MarkLogin_ShouldUpdateLastLoginAt_WhenCalledMultipleTimes()
    {
        // Arrange
        var user = new User(
            Guid.NewGuid(),
            "test@example.com",
            "TEST@EXAMPLE.COM",
            "hashedPassword",
            "testuser",
            "TESTUSER");
        user.MarkLogin();
        var firstLogin = user.LastLoginAt;

        // Act - Wait a bit and mark login again
        Thread.Sleep(10);
        user.MarkLogin();

        // Assert
        user.LastLoginAt.Should().NotBeNull();
        user.LastLoginAt.Should().BeAfter(firstLogin!.Value);
    }

    [Fact]
    public void SetActive_ShouldSetIsActiveToTrue()
    {
        // Arrange
        var user = new User(
            Guid.NewGuid(),
            "test@example.com",
            "TEST@EXAMPLE.COM",
            "hashedPassword",
            "testuser",
            "TESTUSER");

        // Act
        user.SetActive(true);

        // Assert
        user.IsActive.Should().BeTrue();
        user.DateModified.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void SetActive_ShouldSetIsActiveToFalse()
    {
        // Arrange
        var user = new User(
            Guid.NewGuid(),
            "test@example.com",
            "TEST@EXAMPLE.COM",
            "hashedPassword",
            "testuser",
            "TESTUSER");

        // Act
        user.SetActive(false);

        // Assert
        user.IsActive.Should().BeFalse();
        user.DateModified.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void SetActive_ShouldUpdateDateModified()
    {
        // Arrange
        var user = new User(
            Guid.NewGuid(),
            "test@example.com",
            "TEST@EXAMPLE.COM",
            "hashedPassword",
            "testuser",
            "TESTUSER");

        // Act
        user.SetActive(false);

        // Assert
        user.DateModified.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void MultipleUpdates_ShouldUpdateDateModifiedEachTime()
    {
        // Arrange
        var user = new User(
            Guid.NewGuid(),
            "test@example.com",
            "TEST@EXAMPLE.COM",
            "hashedPassword",
            "testuser",
            "TESTUSER");

        // Act
        user.UpdateUsername("username1", "USERNAME1");
        var firstModified = user.DateModified;
        Thread.Sleep(10);
        user.UpdateUsername("username2", "USERNAME2");
        var secondModified = user.DateModified;

        // Assert
        secondModified.Should().BeAfter(firstModified!.Value);
    }
}
