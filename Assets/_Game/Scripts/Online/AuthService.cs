using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class AuthService : MonoBehaviour
{
    [SerializeField] private SupabaseConfig config;

    public SupabaseConfig Config => config;

    private void Awake()
    {
        SupabaseSession.SetConfig(config);
    }

    public IEnumerator SignUp(string email, string password, Action<bool, string> callback)
    {
        if (!HasValidConfig(callback))
        {
            yield break;
        }

        string url = $"{config.SupabaseUrl}/auth/v1/signup";

        string jsonBody = JsonUtility.ToJson(new SignUpRequest
        {
            email = email,
            password = password,
            data = new UserMetadata
            {
                display_name = BuildDefaultDisplayName(email),
                username = BuildDefaultUsername(email)
            }
        });

        using UnityWebRequest request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();

        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("apikey", config.AnonKey);
        request.SetRequestHeader("Authorization", $"Bearer {config.AnonKey}");

        yield return request.SendWebRequest();

        string responseText = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;

        if (request.responseCode < 200 || request.responseCode >= 300)
        {
            callback?.Invoke(false, BuildErrorMessage(request.responseCode, request.error, responseText));
            yield break;
        }

        if (request.result != UnityWebRequest.Result.Success)
        {
            callback?.Invoke(false, BuildErrorMessage(request.responseCode, request.error, responseText));
            yield break;
        }

        callback?.Invoke(true, "Đăng ký thành công.");
    }

    public IEnumerator SignIn(string email, string password, Action<bool, string> callback)
    {
        if (!HasValidConfig(callback))
        {
            yield break;
        }

        string url = $"{config.SupabaseUrl}/auth/v1/token?grant_type=password";

        string jsonBody = JsonUtility.ToJson(new SignInRequest
        {
            email = email,
            password = password
        });

        using UnityWebRequest request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();

        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("apikey", config.AnonKey);
        request.SetRequestHeader("Authorization", $"Bearer {config.AnonKey}");

        yield return request.SendWebRequest();

        string responseText = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;

        if (request.responseCode < 200 || request.responseCode >= 300)
        {
            callback?.Invoke(false, BuildErrorMessage(request.responseCode, request.error, responseText));
            yield break;
        }

        if (request.result != UnityWebRequest.Result.Success)
        {
            callback?.Invoke(false, BuildErrorMessage(request.responseCode, request.error, responseText));
            yield break;
        }

        AuthResponse response = JsonUtility.FromJson<AuthResponse>(responseText);

        if (response == null || string.IsNullOrEmpty(response.access_token) || response.user == null)
        {
            callback?.Invoke(false, $"Supabase returned an invalid auth response: {responseText}");
            yield break;
        }

        SupabaseSession.AccessToken = response.access_token;
        SupabaseSession.RefreshToken = response.refresh_token;
        SupabaseSession.UserId = response.user.id;
        SupabaseSession.Email = response.user.email;
        SupabaseSession.DisplayName = response.user.GetDisplayName();
        SupabaseSession.SetConfig(config);
        SupabaseSession.SetCoin(0);

        yield return LoadUserProfile(SupabaseSession.UserId, profile =>
        {
            if (profile == null)
            {
                return;
            }

            SupabaseSession.Username = profile.username;
            SupabaseSession.AvatarUrl = profile.avatar_url;
            SupabaseSession.SetCoin(profile.coin);

            if (!string.IsNullOrWhiteSpace(profile.GetDisplayName()))
            {
                SupabaseSession.DisplayName = profile.GetDisplayName();
            }
        });

        yield return CleanupPlayerStateAfterLogin();
        OnlineRoomSession.Clear();
        OnlineSessionCleanup.Ensure(config);

        callback?.Invoke(true, SupabaseSession.DisplayName);
    }

    public IEnumerator SignOut(Action<bool, string> callback)
    {
        string accessToken = SupabaseSession.AccessToken;

        if (config != null
            && !string.IsNullOrWhiteSpace(config.FunctionUrl)
            && !string.IsNullOrWhiteSpace(config.AnonKey)
            && !string.IsNullOrWhiteSpace(accessToken))
        {
            yield return CleanupPlayerState(accessToken, "logout");
        }

        if (config != null
            && !string.IsNullOrWhiteSpace(config.SupabaseUrl)
            && !string.IsNullOrWhiteSpace(config.AnonKey)
            && !string.IsNullOrWhiteSpace(accessToken))
        {
            string logoutUrl = $"{config.SupabaseUrl}/auth/v1/logout";
            using UnityWebRequest logoutRequest = new UnityWebRequest(logoutUrl, "POST");
            logoutRequest.uploadHandler = new UploadHandlerRaw(Array.Empty<byte>());
            logoutRequest.downloadHandler = new DownloadHandlerBuffer();
            logoutRequest.SetRequestHeader("Content-Type", "application/json");
            logoutRequest.SetRequestHeader("apikey", config.AnonKey);
            logoutRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

            yield return logoutRequest.SendWebRequest();

            string responseText = logoutRequest.downloadHandler != null ? logoutRequest.downloadHandler.text : string.Empty;
            if (logoutRequest.responseCode < 200
                || logoutRequest.responseCode >= 300
                || logoutRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"Supabase logout failed: {BuildErrorMessage(logoutRequest.responseCode, logoutRequest.error, responseText)}");
            }
        }

        OnlineRoomSession.Clear();
        SupabaseSession.Clear();
        callback?.Invoke(true, "Đăng xuất thành công.");
    }

    private IEnumerator CleanupPlayerStateAfterLogin()
    {
        yield return CleanupPlayerState(SupabaseSession.AccessToken, "login");
    }

    private IEnumerator CleanupPlayerState(string accessToken, string context)
    {
        string url = $"{config.FunctionUrl}/cleanup_player_state";

        using UnityWebRequest request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes("{}");

        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();

        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("apikey", config.AnonKey);
        request.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        yield return request.SendWebRequest();

        string responseText = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
        if (request.responseCode < 200 || request.responseCode >= 300 || request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"Cleanup during {context} failed: {BuildErrorMessage(request.responseCode, request.error, responseText)}");
            yield break;
        }

        Debug.Log($"Cleanup during {context} completed: {responseText}");
    }

    private IEnumerator LoadUserProfile(string userId, Action<UserProfile> callback)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            callback?.Invoke(null);
            yield break;
        }

        string escapedUserId = Uri.EscapeDataString(userId);
        string url = $"{config.SupabaseUrl}/rest/v1/users?id=eq.{escapedUserId}&select=id,display_name,username,avatar_url,coin";

        using UnityWebRequest request = UnityWebRequest.Get(url);
        request.SetRequestHeader("apikey", config.AnonKey);
        request.SetRequestHeader("Authorization", $"Bearer {SupabaseSession.AccessToken}");
        request.SetRequestHeader("Accept", "application/json");

        yield return request.SendWebRequest();

        string responseText = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;

        if (request.responseCode < 200 || request.responseCode >= 300 || request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning(BuildErrorMessage(request.responseCode, request.error, responseText));
            callback?.Invoke(null);
            yield break;
        }

        UserProfile[] profiles = FromJsonArray<UserProfile>(responseText);
        if (profiles == null || profiles.Length == 0)
        {
            callback?.Invoke(null);
            yield break;
        }

        callback?.Invoke(profiles[0]);
    }

    private bool HasValidConfig(Action<bool, string> callback)
    {
        if (config == null)
        {
            callback?.Invoke(false, "Supabase config is not assigned on AuthService.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(config.SupabaseUrl) || string.IsNullOrWhiteSpace(config.AnonKey))
        {
            callback?.Invoke(false, "Supabase URL or anon key is empty.");
            return false;
        }

        return true;
    }

    private string BuildErrorMessage(long statusCode, string requestError, string responseText)
    {
        if (!string.IsNullOrWhiteSpace(responseText))
        {
            return $"HTTP {statusCode}: {responseText}";
        }

        if (!string.IsNullOrWhiteSpace(requestError))
        {
            return $"HTTP {statusCode}: {requestError}";
        }

        return $"HTTP {statusCode}: Supabase request failed.";
    }

    [Serializable]
    private class SignInRequest
    {
        public string email;
        public string password;
    }

    [Serializable]
    private class SignUpRequest
    {
        public string email;
        public string password;
        public UserMetadata data;
    }

    [Serializable]
    private class AuthResponse
    {
        public string access_token;
        public string refresh_token;
        public AuthUser user;
    }

    [Serializable]
    private class AuthUser
    {
        public string id;
        public string email;
        public UserMetadata user_metadata;

        public string GetDisplayName()
        {
            if (user_metadata != null)
            {
                if (!string.IsNullOrWhiteSpace(user_metadata.display_name))
                {
                    return user_metadata.display_name;
                }

                if (!string.IsNullOrWhiteSpace(user_metadata.username))
                {
                    return user_metadata.username;
                }

                if (!string.IsNullOrWhiteSpace(user_metadata.full_name))
                {
                    return user_metadata.full_name;
                }

                if (!string.IsNullOrWhiteSpace(user_metadata.name))
                {
                    return user_metadata.name;
                }
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                int atIndex = email.IndexOf('@');
                return atIndex > 0 ? email.Substring(0, atIndex) : email;
            }

            return "Player";
        }
    }

    [Serializable]
    private class UserMetadata
    {
        public string display_name;
        public string username;
        public string full_name;
        public string name;
    }

    [Serializable]
    private class UserProfile
    {
        public string id;
        public string display_name;
        public string username;
        public string avatar_url;
        public int coin;

        public string GetDisplayName()
        {
            if (!string.IsNullOrWhiteSpace(display_name))
            {
                return display_name;
            }

            if (!string.IsNullOrWhiteSpace(username))
            {
                return username;
            }

            return null;
        }
    }

    [Serializable]
    private class JsonArrayWrapper<T>
    {
        public T[] items;
    }

    private T[] FromJsonArray<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<T>();
        }

        JsonArrayWrapper<T> wrapper = JsonUtility.FromJson<JsonArrayWrapper<T>>($"{{\"items\":{json}}}");
        return wrapper?.items ?? Array.Empty<T>();
    }

    private string BuildDefaultDisplayName(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return "Player";
        }

        int atIndex = email.IndexOf('@');
        string localPart = atIndex > 0 ? email.Substring(0, atIndex) : email;
        return string.IsNullOrWhiteSpace(localPart) ? "Player" : localPart.Trim();
    }

    private string BuildDefaultUsername(string email)
    {
        string source = BuildDefaultDisplayName(email).ToLowerInvariant();
        StringBuilder usernameBuilder = new StringBuilder(source.Length);

        foreach (char character in source)
        {
            if (char.IsLetterOrDigit(character) || character == '_')
            {
                usernameBuilder.Append(character);
            }
        }

        string username = usernameBuilder.ToString();
        return string.IsNullOrWhiteSpace(username) ? "player" : username;
    }
}
