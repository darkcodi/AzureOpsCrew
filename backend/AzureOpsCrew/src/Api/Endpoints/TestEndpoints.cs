using AzureOpsCrew.Api.Extensions;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Serilog;

namespace AzureOpsCrew.Api.Endpoints
{
    public static class TestEndpoints
    {
        public static void MapTestEndpoints(this IEndpointRouteBuilder routeBuilder)
        {
            var group = routeBuilder.MapGroup("/api/test")
                .WithTags("Test");

            group.MapGet("/ping", () =>
            {
                return Results.Ok(new { message = "pong" });
            })
                .Produces(200, typeof(object));

            group.MapGet("/cosmos-health", async (AzureOpsCrewContext context, CancellationToken cancellationToken) =>
            {
                try
                {
                    await context.Dummies.CountAsync(cancellationToken);
                    return Results.Ok(new { status = "healthy", database = "cosmos" });
                }
                catch (Exception e)
                {
                    Log.Error(e, "Cosmos test failed");
                    return Results.StatusCode(503);
                }
            })
                .Produces(200, typeof(object))
                .Produces(503);

            group.MapGet("/agent", async (IChatClient chatClient) =>
            {
                try
                {
                    var writer = new ChatClientAgent(chatClient,
                        "You are a creative copywriter. Generate catchy slogans and marketing copy. Be concise and impactful.",
                        "CopyWriter",
                        "A creative copywriter agent");
                    var response = await writer.RunAsync(new ChatMessage(ChatRole.User, "Test message"));
                    return Results.Ok(new { status = "healthy", text = response.Text });
                }
                catch (Exception e)
                {
                    Log.Error(e, "Agent test failed");
                    return Results.StatusCode(503);
                }
            })
            .Produces(200, typeof(object))
            .Produces(503);
        }
    }
}
