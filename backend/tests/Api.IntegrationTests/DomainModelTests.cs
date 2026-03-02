using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Channels;
using AzureOpsCrew.Domain.Providers;
using AzureOpsCrew.Domain.Users;
using FluentAssertions;
using Xunit;

namespace AzureOpsCrew.Api.IntegrationTests;

public class DomainModelTests
{
    [Fact]
    public void User_Constructor_SetsProperties()
    {
        var user = new User("test@example.com", "TEST@EXAMPLE.COM", "hash", "Test User");

        user.Email.Should().Be("test@example.com");
        user.NormalizedEmail.Should().Be("TEST@EXAMPLE.COM");
        user.PasswordHash.Should().Be("hash");
        user.DisplayName.Should().Be("Test User");
        user.IsActive.Should().BeTrue();
        user.DateCreated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void User_MarkLogin_UpdatesLastLoginAt()
    {
        var user = new User("t@e.com", "T@E.COM", "h", "T");
        user.LastLoginAt.Should().BeNull();

        user.MarkLogin();

        user.LastLoginAt.Should().NotBeNull();
        user.LastLoginAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void User_UpdateDisplayName_ChangesName()
    {
        var user = new User("t@e.com", "T@E.COM", "h", "Old Name");

        user.UpdateDisplayName("New Name");

        user.DisplayName.Should().Be("New Name");
        user.DateModified.Should().NotBeNull();
    }

    [Fact]
    public void User_SetActive_ChangesActiveState()
    {
        var user = new User("t@e.com", "T@E.COM", "h", "T");
        user.IsActive.Should().BeTrue();

        user.SetActive(false);
        user.IsActive.Should().BeFalse();

        user.SetActive(true);
        user.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Channel_AddAgent_AppendsToAgentIds()
    {
        var channel = new Channel(Guid.NewGuid(), 1, "Test");
        channel.AgentIds.Should().BeEmpty();

        channel.AddAgent("agent-1");
        channel.AgentIds.Should().BeEquivalentTo(["agent-1"]);

        channel.AddAgent("agent-2");
        channel.AgentIds.Should().BeEquivalentTo(["agent-1", "agent-2"]);
    }

    [Fact]
    public void Channel_RemoveAgent_FiltersAgentIds()
    {
        var channel = new Channel(Guid.NewGuid(), 1, "Test")
        {
            AgentIds = ["agent-1", "agent-2", "agent-3"]
        };

        channel.RemoveAgent("agent-2");

        channel.AgentIds.Should().BeEquivalentTo(["agent-1", "agent-3"]);
    }

    [Fact]
    public void Channel_RemoveNonexistentAgent_NoChange()
    {
        var channel = new Channel(Guid.NewGuid(), 1, "Test")
        {
            AgentIds = ["agent-1"]
        };

        channel.RemoveAgent("agent-999");

        channel.AgentIds.Should().BeEquivalentTo(["agent-1"]);
    }

    [Fact]
    public void Agent_Constructor_SetsProperties()
    {
        var id = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var info = new AgentInfo("TestAgent", "System prompt", "gpt-4o");

        var agent = new Agent(id, 1, info, providerId, "test-agent-id", "#ff0000");

        agent.Id.Should().Be(id);
        agent.ClientId.Should().Be(1);
        agent.Info.Name.Should().Be("TestAgent");
        agent.Info.Prompt.Should().Be("System prompt");
        agent.Info.Model.Should().Be("gpt-4o");
        agent.ProviderId.Should().Be(providerId);
        agent.ProviderAgentId.Should().Be("test-agent-id");
        agent.Color.Should().Be("#ff0000");
    }

    [Fact]
    public void Agent_Update_ChangesInfo()
    {
        var agent = new Agent(Guid.NewGuid(), 1,
            new AgentInfo("OldName", "OldPrompt", "model1"),
            Guid.NewGuid(), "agent-id", "#000000");

        var newProviderId = Guid.NewGuid();
        agent.Update(new AgentInfo("NewName", "NewPrompt", "model2"), newProviderId, "#ffffff");

        agent.Info.Name.Should().Be("NewName");
        agent.Info.Prompt.Should().Be("NewPrompt");
        agent.Info.Model.Should().Be("model2");
        agent.ProviderId.Should().Be(newProviderId);
        agent.Color.Should().Be("#ffffff");
    }

    [Fact]
    public void Provider_Constructor_SetsProperties()
    {
        var id = Guid.NewGuid();
        var provider = new Provider(id, 1, "OpenAI", ProviderType.OpenAI,
            apiKey: "sk-test", defaultModel: "gpt-4o");

        provider.Id.Should().Be(id);
        provider.Name.Should().Be("OpenAI");
        provider.ProviderType.Should().Be(ProviderType.OpenAI);
        provider.ApiKey.Should().Be("sk-test");
        provider.DefaultModel.Should().Be("gpt-4o");
        provider.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Provider_Update_ChangesFields()
    {
        var provider = new Provider(Guid.NewGuid(), 1, "Old", ProviderType.OpenAI, "key1");

        provider.Update("New", "key2", "https://api.example.com", "gpt-4", isEnabled: false);

        provider.Name.Should().Be("New");
        provider.ApiKey.Should().Be("key2");
        provider.ApiEndpoint.Should().Be("https://api.example.com");
        provider.DefaultModel.Should().Be("gpt-4");
        provider.IsEnabled.Should().BeFalse();
        provider.DateModified.Should().NotBeNull();
    }

    [Fact]
    public void Provider_SetEnabled_TogglesState()
    {
        var provider = new Provider(Guid.NewGuid(), 1, "P", ProviderType.OpenAI, "k");
        provider.IsEnabled.Should().BeTrue();

        provider.SetEnabled(false);
        provider.IsEnabled.Should().BeFalse();

        provider.SetEnabled(true);
        provider.IsEnabled.Should().BeTrue();
    }
}
