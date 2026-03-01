namespace AzureOpsCrew.Infrastructure.Ai.Models.Content;

public sealed class AocHostedVectorStoreContent : AocAiContent
{
    public string VectorStoreId { get; set; } = string.Empty;
}
