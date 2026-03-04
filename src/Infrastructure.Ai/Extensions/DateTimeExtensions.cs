namespace AzureOpsCrew.Infrastructure.Ai.Extensions;

public static class DateTimeExtensions
{
    public static long ToUnixTimeMilliseconds(this DateTime dt)
    {
        return new DateTimeOffset(dt).ToUnixTimeMilliseconds();
    }
}
