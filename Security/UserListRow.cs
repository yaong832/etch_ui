namespace etch_ui.Security;

/// <summary>관리자 화면용 사용자 목록 행.</summary>
public sealed class UserListRow
{
    public int Id { get; init; }
    public string Username { get; init; } = string.Empty;
    public UserRole Role { get; init; }
    public bool IsActive { get; init; }
    public string? CreatedAt { get; init; }

    public string RoleDisplay => Role.ToDisplayKorean();

    public string StatusDisplay => IsActive ? "사용" : "비활성";
}
