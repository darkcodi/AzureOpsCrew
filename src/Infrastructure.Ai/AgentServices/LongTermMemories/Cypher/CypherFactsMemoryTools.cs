using System.ComponentModel;

namespace AzureOpsCrew.Infrastructure.Ai.AgentServices.LongTermMemories.Cypher;

public class CypherFactsMemoryTools
{
    private readonly CypherFactsStore _store;
    private readonly Guid _agentId;

    public CypherFactsMemoryTools(CypherFactsStore store, Guid agentId)
    {
        _store = store;
        _agentId = agentId;
    }

    [Description("Add a durable, long-term fact to memory (preferences, profile, constraints, settings, long-lived decisions).")]
    public async Task<CypherFactOperationResult> AddFact(
        [Description("Short, atomic fact text. Example: 'User prefers vegetarian meals'.")] string text,
        [Description("Optional category: preference/profile/constraint/setting/project/etc.")] string? category = null)
    {
        var fact = await _store.AddFactAsync(_agentId, text, category);
        return new CypherFactOperationResult(true, "Saved.", fact);
    }

    [Description("Update an existing fact by id (use memory_search_facts first to find the correct id).")]
    public async Task<CypherFactOperationResult> UpdateFact(
        [Description("Fact id to update.")] string factId,
        [Description("New fact text.")] string text,
        [Description("Optional category. If null -> keep current category.")] string? category = null)
    {
        var updated = await _store.UpdateFactAsync(_agentId, factId, text, category);
        return updated is null
            ? new CypherFactOperationResult(false, $"Fact '{factId}' not found.")
            : new CypherFactOperationResult(true, "Updated.", updated);
    }

    [Description("Delete a fact from memory by id.")]
    public async Task<CypherFactOperationResult> DeleteFact(
        [Description("Fact id to delete.")] string factId)
    {
        var deleted = await _store.DeleteFactAsync(_agentId, factId);
        return deleted is null
            ? new CypherFactOperationResult(false, $"Fact '{factId}' not found.")
            : new CypherFactOperationResult(true, "Deleted.", deleted);
    }

    [Description("Search facts by free-text keywords using full-text search (Lucene). Pass plain keywords — no special operators.")]
    public Task<CypherFactSearchResult> SearchFacts(
        [Description("Plain keywords to search. Example: 'language preference ukrainian'. No special chars needed.")] string query,
        [Description("Max results (1..50).")] int limit = 8)
        => _store.SearchFactsAsync(_agentId, query, limit);

    // For debug
    [Description("List most recent facts (debug / inspection).")]
    public Task<IReadOnlyList<CypherFactDto>> ListFacts(
        [Description("Max number of facts to return (1..200).")] int limit = 50)
        => _store.ListFactsAsync(_agentId, limit);
}
