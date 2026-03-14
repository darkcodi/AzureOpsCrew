using Neo4j.Driver;
using System.Text.RegularExpressions;

namespace AzureOpsCrew.Infrastructure.Ai.AgentServices.LongTermMemories.Neo4j;

public record Neo4jFactDto(
    string Id,
    string Text,
    string? Category,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public record Neo4jFactOperationResult(
    bool Success,
    string Message,
    Neo4jFactDto? Fact = null);

public record Neo4jFactSearchResult(
    string Query,
    int TotalMatches,
    IReadOnlyList<Neo4jFactDto> Facts);

public class Neo4jFactsStore
{
    private readonly IDriver _driver;

    public Neo4jFactsStore(IDriver driver)
    {
        _driver = driver;
    }

    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        await session.RunAsync(
            "CREATE CONSTRAINT fact_id_unique IF NOT EXISTS FOR (f:Fact) REQUIRE f.id IS UNIQUE");

        await session.RunAsync(
            "CREATE FULLTEXT INDEX fact_fulltext IF NOT EXISTS FOR (n:Fact) ON EACH [n.text, n.category]");
    }

    public async Task<Neo4jFactDto> AddFactAsync(Guid agentId, string text, string? category, CancellationToken cancellationToken = default)
    {
        text = NormalizeText(text);
        category = NormalizeCategory(category);

        await using var session = _driver.AsyncSession();

        return await session.ExecuteWriteAsync(async tx =>
        {
            var checkCursor = await tx.RunAsync(
                @"MATCH (f:Fact)
                  WHERE f.agentId = $agentId
                    AND f.text = $text
                    AND ($category IS NULL AND f.category IS NULL
                         OR $category IS NOT NULL AND f.category = $category)
                  RETURN f LIMIT 1",
                new { agentId = agentId.ToString(), text, category });

            if (await checkCursor.FetchAsync())
            {
                var existing = MapFact(checkCursor.Current["f"].As<INode>());
                var touchedAt = DateTimeOffset.UtcNow;

                await tx.RunAsync(
                    "MATCH (f:Fact {id: $id}) SET f.updatedAtUtc = $now",
                    new { id = existing.Id, now = touchedAt.ToString("O") });

                return existing with { UpdatedAtUtc = touchedAt };
            }

            var now = DateTimeOffset.UtcNow;
            var newId = Guid.NewGuid().ToString("N");

            await tx.RunAsync(
                @"CREATE (f:Fact {
                    id: $id, agentId: $agentId, text: $text,
                    category: $category, createdAtUtc: $now, updatedAtUtc: $now
                })",
                new { id = newId, agentId = agentId.ToString(), text, category, now = now.ToString("O") });

            return new Neo4jFactDto(newId, text, category, now, now);
        });
    }

    public async Task<Neo4jFactDto?> UpdateFactAsync(Guid agentId, string factId, string newText, string? categoryOrNullToKeep, CancellationToken cancellationToken = default)
    {
        newText = NormalizeText(newText);
        var now = DateTimeOffset.UtcNow;

        await using var session = _driver.AsyncSession();

        return await session.ExecuteWriteAsync(async tx =>
        {
            IResultCursor cursor;

            if (categoryOrNullToKeep is not null)
            {
                cursor = await tx.RunAsync(
                    @"MATCH (f:Fact {id: $factId, agentId: $agentId})
                      SET f.text = $text, f.category = $category, f.updatedAtUtc = $now
                      RETURN f",
                    new
                    {
                        factId, agentId = agentId.ToString(), text = newText,
                        category = NormalizeCategory(categoryOrNullToKeep),
                        now = now.ToString("O")
                    });
            }
            else
            {
                cursor = await tx.RunAsync(
                    @"MATCH (f:Fact {id: $factId, agentId: $agentId})
                      SET f.text = $text, f.updatedAtUtc = $now
                      RETURN f",
                    new { factId, agentId = agentId.ToString(), text = newText, now = now.ToString("O") });
            }

            return await cursor.FetchAsync() ? MapFact(cursor.Current["f"].As<INode>()) : null;
        });
    }

    public async Task<Neo4jFactDto?> DeleteFactAsync(Guid agentId, string factId, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        return await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                @"MATCH (f:Fact {id: $factId, agentId: $agentId})
                  WITH f.id AS id, f.text AS text, f.category AS category,
                       f.createdAtUtc AS createdAtUtc, f.updatedAtUtc AS updatedAtUtc, f
                  DETACH DELETE f
                  RETURN id, text, category, createdAtUtc, updatedAtUtc",
                new { factId, agentId = agentId.ToString() });

            if (!await cursor.FetchAsync())
                return null;

            var r = cursor.Current;
            return new Neo4jFactDto(
                r["id"].As<string>(),
                r["text"].As<string>(),
                r["category"].As<string?>(),
                DateTimeOffset.Parse(r["createdAtUtc"].As<string>()),
                DateTimeOffset.Parse(r["updatedAtUtc"].As<string>()));
        });
    }

    public async Task<Neo4jFactSearchResult> SearchFactsAsync(Guid agentId, string query, int limit = 8, CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 50);
        query = (query ?? string.Empty).Trim();

        await using var session = _driver.AsyncSession();

        return await session.ExecuteReadAsync(async tx =>
        {
            List<Neo4jFactDto> facts;

            if (string.IsNullOrWhiteSpace(query))
            {
                var cursor = await tx.RunAsync(
                    @"MATCH (f:Fact {agentId: $agentId})
                      RETURN f ORDER BY f.updatedAtUtc DESC LIMIT $limit",
                    new { agentId = agentId.ToString(), limit });

                facts = await CollectFacts(cursor);
                return new Neo4jFactSearchResult(query, facts.Count, facts);
            }

            var ftCursor = await tx.RunAsync(
                @"CALL db.index.fulltext.queryNodes('fact_fulltext', $query)
                  YIELD node, score
                  WHERE node.agentId = $agentId
                  RETURN node
                  ORDER BY score DESC
                  LIMIT $limit",
                new { query = SanitizeForFullText(query), agentId = agentId.ToString(), limit });

            facts = new List<Neo4jFactDto>();
            while (await ftCursor.FetchAsync())
                facts.Add(MapFact(ftCursor.Current["node"].As<INode>()));

            return new Neo4jFactSearchResult(query, facts.Count, facts);
        });
    }

    public async Task<IReadOnlyList<Neo4jFactDto>> ListFactsAsync(Guid agentId, int limit = 50, CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 200);

        await using var session = _driver.AsyncSession();

        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                @"MATCH (f:Fact {agentId: $agentId})
                  RETURN f ORDER BY f.updatedAtUtc DESC LIMIT $limit",
                new { agentId = agentId.ToString(), limit });

            return (IReadOnlyList<Neo4jFactDto>)await CollectFacts(cursor);
        });
    }

    private static async Task<List<Neo4jFactDto>> CollectFacts(IResultCursor cursor)
    {
        var list = new List<Neo4jFactDto>();
        while (await cursor.FetchAsync())
            list.Add(MapFact(cursor.Current["f"].As<INode>()));
        return list;
    }

    private static Neo4jFactDto MapFact(INode node) => new(
        node["id"].As<string>(),
        node["text"].As<string>(),
        node.Properties.ContainsKey("category") ? node["category"].As<string?>() : null,
        DateTimeOffset.Parse(node["createdAtUtc"].As<string>()),
        DateTimeOffset.Parse(node["updatedAtUtc"].As<string>()));

    // Strip Lucene special chars to avoid query-parse errors in full-text search
    private static string SanitizeForFullText(string query)
        => Regex.Replace(query, @"[+\-!(){}\[\]^""~*?:\\\/]", " ").Trim();

    private static string NormalizeText(string? text)
    {
        text = (text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Fact text cannot be empty.", nameof(text));
        return text;
    }

    private static string? NormalizeCategory(string? category)
    {
        category = category?.Trim();
        return string.IsNullOrWhiteSpace(category) ? null : category;
    }
}
