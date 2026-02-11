using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.Extensions.AI;

namespace AzureOpsCrew.Api.Extensions;

public static class EndpointRouteBuilderExtensions
{
    public static void MapAgents(this IEndpointRouteBuilder app)
    {
        var chatClient = app.ServiceProvider.GetRequiredService<IChatClient>();

        // As for now, hard-code one agent
        var agent = chatClient.AsAIAgent(
            name: "AzureOpsCrew",
            instructions: "You are a helpful AI assistant for the AzureOpsCrew team.");

        app.MapAGUI("/agui", agent);
    }
}
