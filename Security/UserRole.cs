namespace etch_ui.Security;

/// <summary>
/// Worker(작업자): 모니터링·Start/Stop. Admin(관리자): 전 권한(알람 리셋·유지보수 등).
/// </summary>
public enum UserRole
{
    Worker = 0,
    Admin = 1
}

public static class UserRoleExtensions
{
    public static string ToDisplayKorean(this UserRole role) => role switch
    {
        UserRole.Admin => "관리자",
        UserRole.Worker => "작업자",
        _ => role.ToString()
    };

    /// <summary>DB에 저장된 문자열(구버전 포함)을 현재 역할로 정규화.</summary>
    public static UserRole ParseFromDatabase(string? roleText)
    {
        if (string.IsNullOrWhiteSpace(roleText))
        {
            return UserRole.Worker;
        }

        string t = roleText.Trim();
        if (Enum.TryParse(t, ignoreCase: true, out UserRole parsed))
        {
            return parsed;
        }

        return t.ToLowerInvariant() switch
        {
            "admin" or "administrator" => UserRole.Admin,
            "worker" or "operator" or "user" => UserRole.Worker,
            "engineer" => UserRole.Admin,
            _ => UserRole.Worker
        };
    }
}
