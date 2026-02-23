using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace AzureOpsCrew.Infrastructure.Ai.AgentServices.LongTermMemories.InMemory
{
    public record FactDto(
        string Id,
        string Text,
        string? Category,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset UpdatedAtUtc);

    public record FactOperationResult(
        bool Success,
        string Message,
        FactDto? Fact = null);

    public record FactSearchResult(
        string Query,
        int TotalMatches,
        IReadOnlyList<FactDto> Facts);

    internal class FactEntity
    {
        public string Id { get; init; } = default!;
        public string Text { get; set; } = string.Empty;
        public string? Category { get; set; }
        public DateTimeOffset CreatedAtUtc { get; init; }
        public DateTimeOffset UpdatedAtUtc { get; set; }
    }

    internal class AgentFactsBucket
    {
        public object Gate { get; } = new();
        public List<FactEntity> Facts { get; } = new();
    }

    public class InMemoryFactsStore
    {
        public static InMemoryFactsStore Shared { get; } = new();

        private const int MaxFactsPerAgent = 1000;

        private readonly ConcurrentDictionary<string, AgentFactsBucket> _buckets =
            new(StringComparer.OrdinalIgnoreCase);

        private AgentFactsBucket GetBucket(string agentId)
            => _buckets.GetOrAdd(agentId ?? string.Empty, _ => new AgentFactsBucket());

        public FactDto AddFact(string agentId, string text, string? category)
        {
            text = NormalizeText(text);
            category = NormalizeCategory(category);

            var bucket = GetBucket(agentId);
            lock (bucket.Gate)
            {
                var existing = bucket.Facts.FirstOrDefault(f =>
                    string.Equals(f.Text, text, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(f.Category ?? string.Empty, category ?? string.Empty, StringComparison.OrdinalIgnoreCase));

                if (existing is not null)
                {
                    existing.UpdatedAtUtc = DateTimeOffset.UtcNow;

                    return ToDto(existing);
                }

                var now = DateTimeOffset.UtcNow;
                var entity = new FactEntity
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Text = text,
                    Category = category,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };

                bucket.Facts.Add(entity);
                if (bucket.Facts.Count > MaxFactsPerAgent)
                {
                    bucket.Facts.Sort((a, b) => a.UpdatedAtUtc.CompareTo(b.UpdatedAtUtc));
                    var removeCount = bucket.Facts.Count - MaxFactsPerAgent;
                    bucket.Facts.RemoveRange(0, removeCount);
                }

                return ToDto(entity);
            }
        }

        public FactDto? UpdateFact(string agentId, string factId, string newText, string? categoryOrNullToKeep)
        {
            newText = NormalizeText(newText);
            var bucket = GetBucket(agentId);

            lock (bucket.Gate)
            {
                var entity = bucket.Facts.FirstOrDefault(f => string.Equals(f.Id, factId, StringComparison.OrdinalIgnoreCase));
                if (entity is null) return null;

                entity.Text = newText;

                if (categoryOrNullToKeep is not null)
                    entity.Category = NormalizeCategory(categoryOrNullToKeep);

                entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
                return ToDto(entity);
            }
        }

        public FactDto? DeleteFact(string agentId, string factId)
        {
            var bucket = GetBucket(agentId);

            lock (bucket.Gate)
            {
                var idx = bucket.Facts.FindIndex(f => string.Equals(f.Id, factId, StringComparison.OrdinalIgnoreCase));
                if (idx < 0) return null;

                var removed = bucket.Facts[idx];
                bucket.Facts.RemoveAt(idx);
                return ToDto(removed);
            }
        }

        public FactSearchResult SearchFacts(string agentId, string query, int limit = 8)
        {
            limit = Math.Clamp(limit, 1, 50);
            query = (query ?? string.Empty).Trim();

            var bucket = GetBucket(agentId);

            lock (bucket.Gate)
            {
                if (bucket.Facts.Count == 0)
                    return new FactSearchResult(query, 0, Array.Empty<FactDto>());

                if (string.IsNullOrWhiteSpace(query))
                {
                    var recent = bucket.Facts
                        .OrderByDescending(f => f.UpdatedAtUtc)
                        .Take(limit)
                        .Select(ToDto)
                        .ToList();

                    return new FactSearchResult(query, recent.Count, recent);
                }

                var tokens = Tokenize(query);

                var scoredAll = bucket.Facts
                    .Select(f => new { Fact = f, Score = Score(f, query, tokens) })
                    .Where(x => x.Score > 0)
                    .ToList();

                var total = scoredAll.Count;

                var top = scoredAll
                    .OrderByDescending(x => x.Score)
                    .ThenByDescending(x => x.Fact.UpdatedAtUtc)
                    .Take(limit)
                    .Select(x => ToDto(x.Fact))
                    .ToList();

                return new FactSearchResult(query, total, top);
            }
        }

        public IReadOnlyList<FactDto> ListFacts(string agentId, int limit = 50)
        {
            limit = Math.Clamp(limit, 1, 200);
            var bucket = GetBucket(agentId);

            lock (bucket.Gate)
            {
                return bucket.Facts
                    .OrderByDescending(f => f.UpdatedAtUtc)
                    .Take(limit)
                    .Select(ToDto)
                    .ToList();
            }
        }

        private static FactDto ToDto(FactEntity f)
            => new(f.Id, f.Text, f.Category, f.CreatedAtUtc, f.UpdatedAtUtc);

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

        private static readonly Regex TokenSplit = new(@"[\W_]+", RegexOptions.Compiled);

        private static string[] Tokenize(string text)
            => TokenSplit.Split(text.ToLowerInvariant())
                .Where(t => t.Length >= 2)
                .Distinct()
                .Take(12)
                .ToArray();

        private static int Score(FactEntity fact, string rawQuery, string[] tokens)
        {
            if (!string.IsNullOrWhiteSpace(fact.Id) &&
                fact.Id.Contains(rawQuery, StringComparison.OrdinalIgnoreCase))
            {
                return 1000;
            }

            if (fact.Text.Contains(rawQuery, StringComparison.OrdinalIgnoreCase))
            {
                return 200;
            }

            int hits = 0;
            foreach (var t in tokens)
            {
                if (fact.Text.Contains(t, StringComparison.OrdinalIgnoreCase))
                    hits += 10;

                if (!string.IsNullOrWhiteSpace(fact.Category) &&
                    fact.Category!.Contains(t, StringComparison.OrdinalIgnoreCase))
                    hits += 3;
            }

            return hits;
        }
    }
}
