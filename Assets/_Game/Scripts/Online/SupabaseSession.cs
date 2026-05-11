public static class SupabaseSession
{
    public static string AccessToken;
    public static string RefreshToken;
    public static string UserId;
    public static string Email;
    public static string DisplayName;

    public static bool IsLoggedIn => !string.IsNullOrEmpty(AccessToken);

    public static void Clear()
    {
        AccessToken = null;
        RefreshToken = null;
        UserId = null;
        Email = null;
        DisplayName = null;
    }
}
