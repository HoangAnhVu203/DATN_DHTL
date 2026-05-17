using System.Text;
using System.Net;
using UnityEngine;
using UnityEngine.Networking;

public class OnlineSessionCleanup : MonoBehaviour
{
    private const string CleanupFunctionName = "cleanup_player_state";
    private const float QuitCleanupTimeoutSeconds = 2f;

    private static OnlineSessionCleanup instance;

    private SupabaseConfig config;

    public static OnlineSessionCleanup Ensure(SupabaseConfig config)
    {
        if (instance == null)
        {
            GameObject runner = new GameObject(nameof(OnlineSessionCleanup));
            instance = runner.AddComponent<OnlineSessionCleanup>();
            DontDestroyOnLoad(runner);
        }

        instance.config = config;
        return instance;
    }

    private void OnEnable()
    {
        Application.wantsToQuit -= OnWantsToQuit;
        Application.wantsToQuit += OnWantsToQuit;
    }

    private void OnDisable()
    {
        Application.wantsToQuit -= OnWantsToQuit;
    }

    private bool OnWantsToQuit()
    {
        CleanupOnQuit();
        return true;
    }

    public void CleanupOnQuit()
    {
        SendCleanupRequestBlocking();
        OnlineRoomSession.Clear();
    }

    private void OnApplicationQuit()
    {
        CleanupOnQuit();
    }

    private void SendCleanupRequestBlocking()
    {
        if (!CanCleanup())
        {
            return;
        }

        string url = $"{config.FunctionUrl}/{CleanupFunctionName}";
        byte[] body = Encoding.UTF8.GetBytes("{}");

        try
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Accept = "application/json";
            request.Timeout = Mathf.RoundToInt(QuitCleanupTimeoutSeconds * 1000f);
            request.ReadWriteTimeout = Mathf.RoundToInt(QuitCleanupTimeoutSeconds * 1000f);
            request.Headers["apikey"] = config.AnonKey;
            request.Headers["Authorization"] = $"Bearer {SupabaseSession.AccessToken}";

            using (System.IO.Stream requestStream = request.GetRequestStream())
            {
                requestStream.Write(body, 0, body.Length);
            }

            using WebResponse response = request.GetResponse();
        }
        catch (WebException exception)
        {
            Debug.LogWarning($"Cleanup on quit failed: {exception.Message}");
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"Cleanup on quit failed: {exception.Message}");
        }
    }

    private bool CanCleanup()
    {
        return config != null
               && SupabaseSession.IsLoggedIn
               && !string.IsNullOrWhiteSpace(config.FunctionUrl)
               && !string.IsNullOrWhiteSpace(config.AnonKey);
    }
}
