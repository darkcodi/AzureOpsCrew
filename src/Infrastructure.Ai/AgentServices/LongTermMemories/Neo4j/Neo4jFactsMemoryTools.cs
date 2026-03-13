using System.ComponentModel;

namespace AzureOpsCrew.Infrastructure.Ai.AgentServices.LongTermMemories.Neo4j;

public class Neo4jFactsMemoryTools
{
    private readonly Neo4jFactsStore _store;
    private readonly Guid _agentId;

    public Neo4jFactsMemoryTools(Neo4jFactsStore store, Guid agentId)
    {
        _store = store;
        _agentId = agentId;
    }

    [Description("Add a durable, long-term fact to memory (preferences, profile, constraints, settings, long-lived decisions).")]
    public async Task<Neo4jFactOperationResult> AddFact(
        [Description("Short, atomic fact text. Example: 'User prefers vegetarian meals'.")] string text,
        [Description("Optional category: preference/profile/constraint/setting/project/etc.")] string? category = null)
    {
        var fact = await _store.AddFactAsync(_agentId, text, category);
        return new Neo4jFactOperationResult(true, "Saved.", fact);
    }

    [Description("Update an existing fact by id (use memory_search_facts first to find the correct id).")]
    public async Task<Neo4jFactOperationResult> UpdateFact(
        [Description("Fact id to update.")] string factId,
        [Description("New fact text.")] string text,
        [Description("Optional category. If null -> keep current category.")] string? category = null)
    {
        var updated = await _store.UpdateFactAsync(_agentId, factId, text, category);
        return updated is null
            ? new Neo4jFactOperationResult(false, $"Fact '{factId}' not found.")
            : new Neo4jFactOperationResult(true, "Updated.", updated);
    }

    [Description("Delete a fact from memory by id.")]
    public async Task<Neo4jFactOperationResult> DeleteFact(
        [Description("Fact id to delete.")] string factId)
    {
        var deleted = await _store.DeleteFactAsync(_agentId, factId);
        return deleted is null
            ? new Neo4jFactOperationResult(false, $"Fact '{factId}' not found.")
            : new Neo4jFactOperationResult(true, "Deleted.", deleted);
    }

    [Description("Search facts by free-text keywords using full-text search (Lucene). Pass plain keywords — no special operators.")]
    public Task<Neo4jFactSearchResult> SearchFacts(
        [Description("Plain keywords to search. Example: 'language preference ukrainian'. No special chars needed.")] string query,
        [Description("Max results (1..50).")] int limit = 8)
        => _store.SearchFactsAsync(_agentId, query, limit);

    // For debug
    [Description("List most recent facts (debug / inspection).")]
    public Task<IReadOnlyList<Neo4jFactDto>> ListFacts(
        [Description("Max number of facts to return (1..200).")] int limit = 50)
        => _store.ListFactsAsync(_agentId, limit);
}
