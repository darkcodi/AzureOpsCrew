using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Channels;
using AzureOpsCrew.Domain.Chats;
using AzureOpsCrew.Domain.McpServerConfigurations;
using AzureOpsCrew.Domain.Providers;
using AzureOpsCrew.Domain.Triggers;
using AzureOpsCrew.Domain.Users;
using AzureOpsCrew.Domain.WaitConditions;
using AzureOpsCrew.Infrastructure.Db;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Db.Tests;

public class AzureOpsCrewContextTests
{
    [Fact]
    public void Can_CreateContext_Instance()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);

        // Assert
        context.Should().NotBeNull();
    }

    [Fact]
    public void Agents_DbSet_ShouldBeAccessible()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var agents = context.Agents;

        // Assert
        agents.Should().NotBeNull();
    }

    [Fact]
    public void Channels_DbSet_ShouldBeAccessible()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var channels = context.Channels;

        // Assert
        channels.Should().NotBeNull();
    }

    [Fact]
    public void Providers_DbSet_ShouldBeAccessible()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var providers = context.Providers;

        // Assert
        providers.Should().NotBeNull();
    }

    [Fact]
    public void Users_DbSet_ShouldBeAccessible()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var users = context.Users;

        // Assert
        users.Should().NotBeNull();
    }

    [Fact]
    public void Messages_DbSet_ShouldBeAccessible()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var messages = context.Messages;

        // Assert
        messages.Should().NotBeNull();
    }

    [Fact]
    public void McpServerConfigurations_DbSet_ShouldBeAccessible()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var mcpServerConfigurations = context.McpServerConfigurations;

        // Assert
        mcpServerConfigurations.Should().NotBeNull();
    }

    [Fact]
    public void Dms_DbSet_ShouldBeAccessible()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var dms = context.Dms;

        // Assert
        dms.Should().NotBeNull();
    }

    [Fact]
    public void Triggers_DbSet_ShouldBeAccessible()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var triggers = context.Triggers;

        // Assert
        triggers.Should().NotBeNull();
    }

    [Fact]
    public void WaitConditions_DbSet_ShouldBeAccessible()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var waitConditions = context.WaitConditions;

        // Assert
        waitConditions.Should().NotBeNull();
    }

    [Fact]
    public async Task Can_AddAgent_ToDatabase()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var agentId = Guid.NewGuid();

        // Act
        using (var context = new AzureOpsCrewContext(options))
        {
            var agent = new Agent(
                agentId,
                new AgentInfo("TestAgent", "Test prompt", "gpt-4"),
                Guid.NewGuid(),
                "provider-agent-id",
                "#43b581");
            context.Agents.Add(agent);
            await context.SaveChangesAsync();
        }

        // Assert
        using (var context = new AzureOpsCrewContext(options))
        {
            var agent = await context.Agents.FirstOrDefaultAsync(a => a.Id == agentId);
            agent.Should().NotBeNull();
            agent!.Info.Username.Should().Be("TestAgent");
        }
    }

    [Fact]
    public async Task Can_AddProvider_ToDatabase()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var providerId = Guid.NewGuid();

        // Act
        using (var context = new AzureOpsCrewContext(options))
        {
            var provider = new Provider(
                providerId,
                "TestProvider",
                ProviderType.OpenAI,
                "test-api-key");
            context.Providers.Add(provider);
            await context.SaveChangesAsync();
        }

        // Assert
        using (var context = new AzureOpsCrewContext(options))
        {
            var provider = await context.Providers.FirstOrDefaultAsync(p => p.Id == providerId);
            provider.Should().NotBeNull();
            provider!.Name.Should().Be("TestProvider");
        }
    }

    [Fact]
    public async Task Can_AddUser_ToDatabase()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var userId = Guid.NewGuid();

        // Act
        using (var context = new AzureOpsCrewContext(options))
        {
            var user = new User(
                userId,
                "test@example.com",
                "TEST@EXAMPLE.COM",
                "password-hash",
                "testuser",
                "TESTUSER");
            context.Users.Add(user);
            await context.SaveChangesAsync();
        }

        // Assert
        using (var context = new AzureOpsCrewContext(options))
        {
            var user = await context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            user.Should().NotBeNull();
            user!.Username.Should().Be("testuser");
        }
    }

    [Fact]
    public async Task Can_AddChannel_ToDatabase()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var channelId = Guid.NewGuid();

        // Act
        using (var context = new AzureOpsCrewContext(options))
        {
            var channel = new Channel(channelId, "TestChannel");
            context.Channels.Add(channel);
            await context.SaveChangesAsync();
        }

        // Assert
        using (var context = new AzureOpsCrewContext(options))
        {
            var channel = await context.Channels.FirstOrDefaultAsync(c => c.Id == channelId);
            channel.Should().NotBeNull();
            channel!.Name.Should().Be("TestChannel");
        }
    }

    [Fact]
    public async Task Can_AddMessage_ToDatabase()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var messageId = Guid.NewGuid();

        // Act
        using (var context = new AzureOpsCrewContext(options))
        {
            var message = new Message
            {
                Id = messageId,
                Text = "Test message",
                PostedAt = DateTime.UtcNow,
                UserId = Guid.NewGuid(),
                ChannelId = Guid.NewGuid()
            };
            context.Messages.Add(message);
            await context.SaveChangesAsync();
        }

        // Assert
        using (var context = new AzureOpsCrewContext(options))
        {
            var message = await context.Messages.FirstOrDefaultAsync(m => m.Id == messageId);
            message.Should().NotBeNull();
            message!.Text.Should().Be("Test message");
        }
    }

    [Fact]
    public async Task Can_AddTrigger_ToDatabase()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var triggerId = Guid.NewGuid();

        // Act
        using (var context = new AzureOpsCrewContext(options))
        {
            var trigger = new MessageTrigger
            {
                Id = triggerId,
                AgentId = Guid.NewGuid(),
                ChatId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            };
            context.Triggers.Add(trigger);
            await context.SaveChangesAsync();
        }

        // Assert
        using (var context = new AzureOpsCrewContext(options))
        {
            var trigger = await context.Triggers.FirstOrDefaultAsync(t => t.Id == triggerId);
            trigger.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task Can_AddWaitCondition_ToDatabase()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var waitConditionId = Guid.NewGuid();

        // Act
        using (var context = new AzureOpsCrewContext(options))
        {
            var waitCondition = new MessageWaitCondition
            {
                Id = waitConditionId,
                AgentId = Guid.NewGuid(),
                ChatId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                MessageAfterDateTime = DateTime.UtcNow.AddMinutes(-5)
            };
            context.WaitConditions.Add(waitCondition);
            await context.SaveChangesAsync();
        }

        // Assert
        using (var context = new AzureOpsCrewContext(options))
        {
            var waitCondition = await context.WaitConditions.FirstOrDefaultAsync(w => w.Id == waitConditionId);
            waitCondition.Should().NotBeNull();
        }
    }
}
