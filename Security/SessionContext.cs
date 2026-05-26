namespace etch_ui.Security;

public static class SessionContext
{
    public static UserAccount? CurrentUser { get; private set; }

    public static void SetUser(UserAccount user) => CurrentUser = user;

    public static void Clear() => CurrentUser = null;

    public static bool HasRole(UserRole minimumRole)
    {
        if (CurrentUser is null)
        {
            return false;
        }

        return CurrentUser.Role >= minimumRole;
    }
}
