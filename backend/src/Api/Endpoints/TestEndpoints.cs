using AzureOpsCrew.Domain.Agents;
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
        }
    }
}
