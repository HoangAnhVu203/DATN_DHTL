public static class SupabaseSession
{
    public static string AccessToken;
    public static string RefreshToken;
    public static string UserId;
    public static string Email;
    public static string Username;
    public static string DisplayName;
    public static string AvatarUrl;

    public static bool IsLoggedIn => !string.IsNullOrEmpty(AccessToken);

    public static void Clear()
    {
        AccessToken = null;
        RefreshToken = null;
        UserId = null;
        Email = null;
        Username = null;
        DisplayName = null;
        AvatarUrl = null;
    }
}
