public static class SupabaseSession
{
    public static event System.Action<int> CoinChanged;

    public static string AccessToken;
    public static string RefreshToken;
    public static string UserId;
    public static string Email;
    public static string Username;
    public static string DisplayName;
    public static string AvatarUrl;
    public static int Coin;
    public static SupabaseConfig Config;

    public static bool IsLoggedIn => !string.IsNullOrEmpty(AccessToken);

    public static void SetCoin(int coin)
    {
        Coin = System.Math.Max(0, coin);
        CoinChanged?.Invoke(Coin);
    }

    public static void AddCoin(int coin)
    {
        if (coin <= 0)
        {
            return;
        }

        SetCoin(Coin + coin);
    }

    public static void SetConfig(SupabaseConfig config)
    {
        if (config != null)
        {
            Config = config;
        }
    }

    public static void Clear()
    {
        AccessToken = null;
        RefreshToken = null;
        UserId = null;
        Email = null;
        Username = null;
        DisplayName = null;
        AvatarUrl = null;
        SetCoin(0);
    }
}
