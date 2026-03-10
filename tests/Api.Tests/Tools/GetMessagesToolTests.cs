using AzureOpsCrew.Api.Background.Tools;

namespace Api.Tests.Tools;

public class GetMessagesToolTests
{
    public static TheoryData<string, int, int, int, int, int, int> ValidDateTimeFormats => new()
    {
        // ISO 8601 with Z
        { "2026-03-10T01:05:32Z", 2026, 3, 10, 1, 5, 32 },

        // ISO 8601 with offset
        { "2026-03-10T01:05:32+00:00", 2026, 3, 10, 1, 5, 32 },
        { "2026-03-10T01:05:32+05:30", 2026, 3, 9, 19, 35, 32 }, // 5:30 ahead means UTC is earlier

        // .NET datetime with fractional seconds (no timezone) - THE FAILING CASE
        { "2026-03-10T01:05:32.5370000", 2026, 3, 10, 1, 5, 32 },
        { "2026-03-10T01:05:32.537000", 2026, 3, 10, 1, 5, 32 },
        { "2026-03-10T01:05:32.53700", 2026, 3, 10, 1, 5, 32 },
        { "2026-03-10T01:05:32.5370", 2026, 3, 10, 1, 5, 32 },
        { "2026-03-10T01:05:32.537", 2026, 3, 10, 1, 5, 32 },
        { "2026-03-10T01:05:32.53", 2026, 3, 10, 1, 5, 32 },
        { "2026-03-10T01:05:32.5", 2026, 3, 10, 1, 5, 32 },

        // ISO 8601 with fractional seconds and Z
        { "2026-03-10T01:05:32.537Z", 2026, 3, 10, 1, 5, 32 },

        // Unix timestamp (seconds) - January 15, 2025 at 10:00:00 UTC
        { "1736935200", 2025, 1, 15, 10, 0, 0 },

        // Unix timestamp (milliseconds) - January 15, 2025 at 10:00:00 UTC
        { "1736935200000", 2025, 1, 15, 10, 0, 0 },

        // Simple date formats
        { "2026-03-10", 2026, 3, 10, 0, 0, 0 },
        { "2026/03/10", 2026, 3, 10, 0, 0, 0 },

        // Date with time (space separator)
        { "2026-03-10 01:05:32", 2026, 3, 10, 1, 5, 32 },
        { "2026/03/10 01:05:32", 2026, 3, 10, 1, 5, 32 },

        // Sortable date/time pattern
        { "2026-03-10T01:05:32", 2026, 3, 10, 1, 5, 32 },

        // Universal sortable date/time pattern
        { "2026-03-10 01:05:32Z", 2026, 3, 10, 1, 5, 32 },
    };

    [Theory]
    [MemberData(nameof(ValidDateTimeFormats))]
    public void TryParseDateTime_ValidFormats_ReturnsCorrectDateTime(
        string input, int year, int month, int day, int hour, int minute, int second)
    {
        // Act
        var result = GetMessagesTool.TryParseDateTime(input);

        // Assert
        Assert.NotNull(result);
        var utcResult = result.Value.ToUniversalTime();
        Assert.Equal(year, utcResult.Year);
        Assert.Equal(month, utcResult.Month);
        Assert.Equal(day, utcResult.Day);
        Assert.Equal(hour, utcResult.Hour);
        Assert.Equal(minute, utcResult.Minute);
        Assert.Equal(second, utcResult.Second);
    }

    public static TheoryData<string> InvalidDateTimeFormats => new()
    {
        "",
        "   ",
        "invalid",
        "not-a-date",
        "9999999999999999999999", // Too large for long
        "abc123def",
        "2025-13-01", // Invalid month
        "2025-01-32", // Invalid day
        "2025-01-01T25:00:00", // Invalid hour
    };

    [Theory]
    [MemberData(nameof(InvalidDateTimeFormats))]
    public void TryParseDateTime_InvalidInput_ReturnsNull(string input)
    {
        // Act
        var result = GetMessagesTool.TryParseDateTime(input);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void TryParseDateTime_NullInput_ReturnsNull()
    {
        // Act
        var result = GetMessagesTool.TryParseDateTime(null);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void TryParseDateTime_NonStringNonDateTimeInput_ReturnsNull()
    {
        // Act
        var result = GetMessagesTool.TryParseDateTime(12345);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void TryParseDateTime_DateTimeInput_ReturnsSameDateTime()
    {
        // Arrange
        var expected = new DateTime(2026, 3, 10, 1, 5, 32, DateTimeKind.Utc);

        // Act
        var result = GetMessagesTool.TryParseDateTime(expected);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TryParseDateTime_WithWhitespace_TrimsAndParses()
    {
        // Act
        var result = GetMessagesTool.TryParseDateTime("  2026-03-10T01:05:32Z  ");

        // Assert
        Assert.NotNull(result);
        var utcResult = result.Value.ToUniversalTime();
        Assert.Equal(2026, utcResult.Year);
        Assert.Equal(3, utcResult.Month);
        Assert.Equal(10, utcResult.Day);
    }

    [Fact]
    public void TryParseDateTime_UnixTimestampBelowLowerBound_ParsesAsIso8601()
    {
        // Arrange - This is a valid ISO 8601 date but would be rejected as a Unix timestamp
        // because it's numerically less than 10000000000 (the lower bound for milliseconds)

        // Act
        var result = GetMessagesTool.TryParseDateTime("1000000000");

        // Assert - Should be parsed as a Unix timestamp in seconds (September 9, 2001)
        Assert.NotNull(result);
        var utcResult = result.Value.ToUniversalTime();
        Assert.Equal(2001, utcResult.Year);
        Assert.Equal(9, utcResult.Month);
    }

    [Fact]
    public void TryParseDateTime_EdgeCase_UnixTimestampBoundary()
    {
        // Test the boundary between seconds and milliseconds (10,000,000,000)
        // 10000000000 ms = Sun Apr 26 1970
        // 10000000000 sec = Nov 20 2286 (beyond the 4_000_000_000 check)
        // Since it's >= 10_000_000_000, it should be treated as milliseconds

        // Act
        var result = GetMessagesTool.TryParseDateTime("10000000000");

        // Assert - Should parse as milliseconds
        Assert.NotNull(result);
        var utcResult = result.Value.ToUniversalTime();
        Assert.Equal(1970, utcResult.Year);
        Assert.Equal(4, utcResult.Month);
    }
}
