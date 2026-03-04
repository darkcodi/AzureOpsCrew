namespace Front.Models;

public class LoginInfo
{
    public required string Token { get; init; }
    public required DateTime TokenExpiration { get; init; }
    public required UserDto User { get; init; }
}
