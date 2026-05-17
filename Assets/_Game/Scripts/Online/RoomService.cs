using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class RoomService : MonoBehaviour
{
    [SerializeField] private SupabaseConfig config;

    public IEnumerator CreateRoom(int maxPlayers, Action<bool, RoomData, string> callback)
    {
        string jsonBody = JsonUtility.ToJson(new CreateRoomRequest
        {
            max_players = maxPlayers
        });

        yield return InvokeFunction<RoomResponse>("create_room", jsonBody, (success, response, error) =>
        {
            callback?.Invoke(success && response != null && response.success, response?.data, error ?? response?.error?.message);
        });
    }

    public IEnumerator JoinRoom(string roomCode, Action<bool, RoomData, string> callback)
    {
        string jsonBody = JsonUtility.ToJson(new JoinRoomRequest
        {
            room_code = roomCode
        });

        yield return InvokeFunction<RoomResponse>("join_room", jsonBody, (success, response, error) =>
        {
            callback?.Invoke(success && response != null && response.success, response?.data, error ?? response?.error?.message);
        });
    }

    public IEnumerator SetReady(string roomId, bool isReady, Action<bool, ReadyData, string> callback)
    {
        string jsonBody = JsonUtility.ToJson(new SetReadyRequest
        {
            room_id = roomId,
            is_ready = isReady
        });

        yield return InvokeFunction<ReadyResponse>("set_ready", jsonBody, (success, response, error) =>
        {
            callback?.Invoke(success && response != null && response.success, response?.data, error ?? response?.error?.message);
        });
    }

    public IEnumerator LeaveRoom(string roomId, Action<bool, string> callback)
    {
        string jsonBody = JsonUtility.ToJson(new RoomIdRequest
        {
            room_id = roomId
        });

        yield return InvokeFunction<BasicResponse>("leave_room", jsonBody, (success, response, error) =>
        {
            callback?.Invoke(success && response != null && response.success, error ?? response?.error?.message);
        });
    }

    public IEnumerator SendRoomHeartbeat(string roomId, Action<bool, string> callback)
    {
        string jsonBody = JsonUtility.ToJson(new RoomIdRequest
        {
            room_id = roomId
        });

        yield return InvokeFunction<BasicResponse>("room_heartbeat", jsonBody, (success, response, error) =>
        {
            callback?.Invoke(success && response != null && response.success, error ?? response?.error?.message);
        });
    }

    public IEnumerator CleanupInactiveRoomPlayers(string roomId, int timeoutSeconds, Action<bool, string> callback)
    {
        string jsonBody = JsonUtility.ToJson(new CleanupInactiveRequest
        {
            room_id = roomId,
            timeout_seconds = timeoutSeconds
        });

        yield return InvokeFunction<BasicResponse>("cleanup_inactive_room_players", jsonBody, (success, response, error) =>
        {
            callback?.Invoke(success && response != null && response.success, error ?? response?.error?.message);
        });
    }

    public IEnumerator StartMatch(string roomId, Action<bool, MatchData, string> callback)
    {
        string jsonBody = JsonUtility.ToJson(new RoomIdRequest
        {
            room_id = roomId
        });

        yield return InvokeFunction<MatchResponse>("start_match", jsonBody, (success, response, error) =>
        {
            callback?.Invoke(success && response != null && response.success, response?.data, error ?? response?.error?.message);
        });
    }

    public IEnumerator GetActiveMatch(string roomId, Action<bool, MatchData, string> callback)
    {
        if (!EnsureReady(callback))
        {
            yield break;
        }

        string escapedRoomId = Uri.EscapeDataString(roomId);
        string url = $"{config.SupabaseUrl}/rest/v1/matches?room_id=eq.{escapedRoomId}&status=in.(starting,active)&select=match_id:id,room_id,host_id,status,seed,started_at,created_at&order=created_at.desc&limit=1";

        yield return GetRest(url, (success, response, error) =>
        {
            if (!success)
            {
                callback?.Invoke(false, null, error);
                return;
            }

            MatchData[] matches = FromJsonArray<MatchData>(response);
            callback?.Invoke(true, matches.Length > 0 ? matches[0] : null, null);
        });
    }

    public IEnumerator GetRoomPlayers(string roomId, Action<bool, List<RoomPlayerData>, string> callback)
    {
        if (!EnsureReady(callback))
        {
            yield break;
        }

        string escapedRoomId = Uri.EscapeDataString(roomId);
        string roomPlayersUrl = $"{config.SupabaseUrl}/rest/v1/room_players?room_id=eq.{escapedRoomId}&select=user_id,is_host,is_ready,joined_at&order=joined_at.asc";

        string roomPlayersJson = null;
        yield return GetRest(roomPlayersUrl, (success, response, error) =>
        {
            if (success)
            {
                roomPlayersJson = response;
            }
            else
            {
                callback?.Invoke(false, null, error);
            }
        });

        if (string.IsNullOrEmpty(roomPlayersJson))
        {
            yield break;
        }

        RoomMembership[] memberships = FromJsonArray<RoomMembership>(roomPlayersJson);
        if (memberships.Length == 0)
        {
            callback?.Invoke(true, new List<RoomPlayerData>(), null);
            yield break;
        }

        string userIds = BuildInFilter(memberships);
        string usersUrl = $"{config.SupabaseUrl}/rest/v1/users?id=in.({userIds})&select=id,display_name,username,avatar_url";

        string usersJson = null;
        yield return GetRest(usersUrl, (success, response, error) =>
        {
            if (success)
            {
                usersJson = response;
            }
            else
            {
                callback?.Invoke(false, null, error);
            }
        });

        if (string.IsNullOrEmpty(usersJson))
        {
            yield break;
        }

        UserProfile[] profiles = FromJsonArray<UserProfile>(usersJson);
        Dictionary<string, UserProfile> profileById = new Dictionary<string, UserProfile>();

        foreach (UserProfile profile in profiles)
        {
            if (profile != null && !string.IsNullOrEmpty(profile.id))
            {
                profileById[profile.id] = profile;
            }
        }

        List<RoomPlayerData> players = new List<RoomPlayerData>();
        foreach (RoomMembership membership in memberships)
        {
            profileById.TryGetValue(membership.user_id, out UserProfile profile);
            players.Add(new RoomPlayerData
            {
                user_id = membership.user_id,
                is_host = membership.is_host,
                is_ready = membership.is_ready,
                display_name = profile?.GetDisplayName() ?? membership.user_id,
                avatar_url = profile?.avatar_url
            });
        }

        callback?.Invoke(true, players, null);
    }

    private IEnumerator InvokeFunction<T>(string functionName, string jsonBody, Action<bool, T, string> callback)
    {
        if (!EnsureReady(callback))
        {
            yield break;
        }

        string url = $"{config.FunctionUrl}/{functionName}";

        using UnityWebRequest request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.timeout = 10;

        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("apikey", config.AnonKey);
        request.SetRequestHeader("Authorization", $"Bearer {SupabaseSession.AccessToken}");

        yield return request.SendWebRequest();

        string responseText = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;

        if (request.responseCode < 200 || request.responseCode >= 300 || request.result != UnityWebRequest.Result.Success)
        {
            callback?.Invoke(false, default, BuildErrorMessage(request.responseCode, request.error, responseText));
            yield break;
        }

        if (functionName == "room_heartbeat")
        {
            Debug.Log($"room_heartbeat response: {responseText}");
        }

        T response = JsonUtility.FromJson<T>(responseText);
        callback?.Invoke(true, response, null);
    }

    private IEnumerator GetRest(string url, Action<bool, string, string> callback)
    {
        using UnityWebRequest request = UnityWebRequest.Get(url);
        request.timeout = 10;
        request.SetRequestHeader("apikey", config.AnonKey);
        request.SetRequestHeader("Authorization", $"Bearer {SupabaseSession.AccessToken}");
        request.SetRequestHeader("Accept", "application/json");

        yield return request.SendWebRequest();

        string responseText = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;

        if (request.responseCode < 200 || request.responseCode >= 300 || request.result != UnityWebRequest.Result.Success)
        {
            callback?.Invoke(false, null, BuildErrorMessage(request.responseCode, request.error, responseText));
            yield break;
        }

        callback?.Invoke(true, responseText, null);
    }

    private bool EnsureReady<T>(Action<bool, T, string> callback)
    {
        if (config == null)
        {
            callback?.Invoke(false, default, "Supabase config is not assigned.");
            return false;
        }

        if (!SupabaseSession.IsLoggedIn)
        {
            callback?.Invoke(false, default, "Bạn chưa đăng nhập.");
            return false;
        }

        return true;
    }

    private bool EnsureReady(Action<bool, List<RoomPlayerData>, string> callback)
    {
        if (config == null)
        {
            callback?.Invoke(false, null, "Supabase config is not assigned.");
            return false;
        }

        if (!SupabaseSession.IsLoggedIn)
        {
            callback?.Invoke(false, null, "Bạn chưa đăng nhập.");
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

    private string BuildInFilter(RoomMembership[] memberships)
    {
        StringBuilder builder = new StringBuilder();

        for (int i = 0; i < memberships.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append(Uri.EscapeDataString(memberships[i].user_id));
        }

        return builder.ToString();
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

    [Serializable]
    private class CreateRoomRequest
    {
        public int max_players;
    }

    [Serializable]
    private class JoinRoomRequest
    {
        public string room_code;
    }

    [Serializable]
    private class SetReadyRequest
    {
        public string room_id;
        public bool is_ready;
    }

    [Serializable]
    private class RoomIdRequest
    {
        public string room_id;
    }

    [Serializable]
    private class CleanupInactiveRequest
    {
        public string room_id;
        public int timeout_seconds;
    }

    [Serializable]
    private class BasicResponse
    {
        public bool success;
        public ErrorData error;
    }

    [Serializable]
    private class RoomResponse
    {
        public bool success;
        public RoomData data;
        public ErrorData error;
    }

    [Serializable]
    private class ReadyResponse
    {
        public bool success;
        public ReadyData data;
        public ErrorData error;
    }

    [Serializable]
    private class MatchResponse
    {
        public bool success;
        public MatchData data;
        public ErrorData error;
    }

    [Serializable]
    public class RoomData
    {
        public string room_id;
        public string room_code;
        public string host_id;
        public string status;
        public int max_players;
    }

    [Serializable]
    public class ReadyData
    {
        public string room_id;
        public string room_code;
        public string user_id;
        public bool is_ready;
    }

    [Serializable]
    public class MatchData
    {
        public string match_id;
        public string room_id;
        public string host_id;
        public string status;
        public int seed;
        public string started_at;
        public string created_at;
    }

    [Serializable]
    public class RoomPlayerData
    {
        public string user_id;
        public string display_name;
        public string avatar_url;
        public bool is_host;
        public bool is_ready;
    }

    [Serializable]
    private class RoomMembership
    {
        public string user_id;
        public bool is_host;
        public bool is_ready;
        public string joined_at;
    }

    [Serializable]
    private class UserProfile
    {
        public string id;
        public string display_name;
        public string username;
        public string avatar_url;

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

            return id;
        }
    }

    [Serializable]
    private class ErrorData
    {
        public string code;
        public string message;
    }

    [Serializable]
    private class JsonArrayWrapper<T>
    {
        public T[] items;
    }
}
