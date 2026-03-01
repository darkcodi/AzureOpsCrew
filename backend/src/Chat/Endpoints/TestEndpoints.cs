namespace Chat.Endpoints
{
    public static class TestEndpoints
    {
        public static void MapChatEndpoints(this IEndpointRouteBuilder routeBuilder)
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
