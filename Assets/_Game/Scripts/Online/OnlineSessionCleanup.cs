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

    // Ensures the is ready.
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

    // Restores runtime state when this component becomes active.
    private void OnEnable()
    {
        Application.wantsToQuit -= OnWantsToQuit;
        Application.wantsToQuit += OnWantsToQuit;
    }

    // Clears temporary state when this component is disabled.
    private void OnDisable()
    {
        Application.wantsToQuit -= OnWantsToQuit;
    }

    // Starts cleanup before the app quits.
    private bool OnWantsToQuit()
    {
        CleanupOnQuit();
        return true;
    }

    // Runs the cleanup on quit step.
    public void CleanupOnQuit()
    {
        SendCleanupRequestBlocking();
        OnlineRoomSession.Clear();
    }

    // Runs final cleanup when the app quits.
    private void OnApplicationQuit()
    {
        CleanupOnQuit();
    }

    // Sends the cleanup request blocking.
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

    // Checks whether cleanup can run right now.
    private bool CanCleanup()
    {
        return config != null
               && SupabaseSession.IsLoggedIn
               && !string.IsNullOrWhiteSpace(config.FunctionUrl)
               && !string.IsNullOrWhiteSpace(config.AnonKey);
    }
}
