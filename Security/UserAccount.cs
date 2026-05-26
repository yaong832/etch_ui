namespace etch_ui.Security;

public sealed class UserAccount
{
    public int Id { get; init; }
    public string Username { get; init; } = string.Empty;
    public UserRole Role { get; init; } = UserRole.Worker;
}
