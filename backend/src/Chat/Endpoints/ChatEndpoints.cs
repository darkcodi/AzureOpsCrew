using AzureOpsCrew.Domain.Chats;

namespace Chat.Endpoints
{
    public static class ChatEndpoints
    {
        public static void MapChatEndpoints(this IEndpointRouteBuilder routeBuilder)
        {
            var group = routeBuilder.MapGroup("/api/chat")
                .WithTags("Chat");

            group.MapGet("/chats", () =>
                {
                })
                .Produces<List<ChatEntity>>(StatusCodes.Status200OK);
        }
    }
}
