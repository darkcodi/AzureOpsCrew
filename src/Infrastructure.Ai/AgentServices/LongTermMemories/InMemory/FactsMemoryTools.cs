using System.ComponentModel;

namespace AzureOpsCrew.Infrastructure.Ai.AgentServices.LongTermMemories.InMemory
{
    public class FactsMemoryTools
    {
        private readonly InMemoryFactsStore _store;
        private readonly Guid _agentId;

        public FactsMemoryTools(InMemoryFactsStore store, Guid agentId)
        {
            _store = store;
            _agentId = agentId;
        }

        [Description("Add a durable, long-term fact to memory (preferences, profile, constraints, settings, long-lived decisions).")]
        public FactOperationResult AddFact(
            [Description("Short, atomic fact text. Example: 'User prefers vegetarian meals'.")] string text,
            [Description("Optional category: preference/profile/constraint/setting/project/etc.")] string? category = null)
        {
            var fact = _store.AddFact(_agentId, text, category);
            return new FactOperationResult(true, "Saved.", fact);
        }

        [Description("Update an existing fact by id (use memory_search_facts first to find the correct id).")]
        public FactOperationResult UpdateFact(
            [Description("Fact id to update.")] string factId,
            [Description("New fact text.")] string text,
            [Description("Optional category. If null -> keep current category.")] string? category = null)
        {
            var updated = _store.UpdateFact(_agentId, factId, text, category);
            return updated is null
                ? new FactOperationResult(false, $"Fact '{factId}' not found.")
                : new FactOperationResult(true, "Updated.", updated);
        }

        [Description("Delete a fact from memory by id.")]
        public FactOperationResult DeleteFact(
            [Description("Fact id to delete.")] string factId)
        {
            var deleted = _store.DeleteFact(_agentId, factId);
            return deleted is null
                ? new FactOperationResult(false, $"Fact '{factId}' not found.")
                : new FactOperationResult(true, "Deleted.", deleted);
        }

        [Description("Search facts by free-text query (simple keyword/token search).")]
        public FactSearchResult SearchFacts(
            [Description("Query text: keywords, partial phrase, or an id.")] string query,
            [Description("Max results (1..50).")] int limit = 8)
            => _store.SearchFacts(_agentId, query, limit);

        //For debug
        [Description("List most recent facts (debug / inspection).")]
        public IReadOnlyList<FactDto> ListFacts(
            [Description("Max number of facts to return (1..200).")] int limit = 50)
            => _store.ListFacts(_agentId, limit);
    }
}
