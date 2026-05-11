using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class RoomService : MonoBehaviour
{
    [SerializeField] private SupabaseConfig config;

    public IEnumerator CreateRoom(int maxPlayers, Action<bool, CreateRoomData, string> callback)
    {
        if (!SupabaseSession.IsLoggedIn)
        {
            callback?.Invoke(false, null, "Bạn chưa đăng nhập.");
            yield break;
        }

        string url = $"{config.FunctionUrl}/create_room";

        string jsonBody = JsonUtility.ToJson(new CreateRoomRequest
        {
            max_players = maxPlayers
        });

        using UnityWebRequest request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();

        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("apikey", config.AnonKey);
        request.SetRequestHeader("Authorization", $"Bearer {SupabaseSession.AccessToken}");

        yield return request.SendWebRequest();

        string responseText = request.downloadHandler.text;

        if (request.responseCode < 200 || request.responseCode >= 300)
        {
            callback?.Invoke(false, null, responseText);
            yield break;
        }

        CreateRoomResponse response = JsonUtility.FromJson<CreateRoomResponse>(responseText);

        if (response == null || !response.success)
        {
            callback?.Invoke(false, null, responseText);
            yield break;
        }

        callback?.Invoke(true, response.data, null);
    }

    [Serializable]
    private class CreateRoomRequest
    {
        public int max_players;
    }

    [Serializable]
    private class CreateRoomResponse
    {
        public bool success;
        public CreateRoomData data;
        public ErrorData error;
    }

    [Serializable]
    public class CreateRoomData
    {
        public string room_id;
        public string room_code;
        public string host_id;
        public string status;
        public int max_players;
    }

    [Serializable]
    private class ErrorData
    {
        public string code;
        public string message;
    }
}
