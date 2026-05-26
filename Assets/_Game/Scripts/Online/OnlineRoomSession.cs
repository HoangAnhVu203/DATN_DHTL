using System.Collections.Generic;

public static class OnlineRoomSession
{
    public static string RoomId;
    public static string RoomCode;
    public static string HostId;
    public static string Status;
    public static int MaxPlayers;
    public static string MatchId;
    public static string MatchStatus;
    public static int MatchSeed;
    public static string MatchStartedAt;
    public static string LastCompletedMatchId;
    public static int ExpectedMatchPlayerCount;
    public static List<RoomService.RoomPlayerData> Players = new List<RoomService.RoomPlayerData>();

    public static bool IsInRoom => !string.IsNullOrEmpty(RoomId);
    public static bool IsHost => IsInRoom && HostId == SupabaseSession.UserId;
    public static bool HasMatch => !string.IsNullOrEmpty(MatchId);

    public static void SetRoom(RoomService.RoomData room)
    {
        RoomId = room.room_id;
        RoomCode = room.room_code;
        HostId = room.host_id;
        Status = room.status;
        MaxPlayers = room.max_players;
    }

    public static void SetMatch(RoomService.MatchData match)
    {
        if (match == null)
        {
            return;
        }

        MatchId = match.match_id;
        MatchStatus = match.status;
        MatchSeed = match.seed;
        MatchStartedAt = match.started_at;
    }

    public static void CacheExpectedMatchPlayerCount()
    {
        ExpectedMatchPlayerCount = Players != null && Players.Count > 0 ? Players.Count : 0;
    }

    public static void MarkCurrentMatchCompleted()
    {
        LastCompletedMatchId = MatchId;
    }

    public static void ClearMatch()
    {
        MatchId = null;
        MatchStatus = null;
        MatchSeed = 0;
        MatchStartedAt = null;
        ExpectedMatchPlayerCount = 0;
    }

    public static void Clear()
    {
        RoomId = null;
        RoomCode = null;
        HostId = null;
        Status = null;
        MaxPlayers = 0;
        MatchId = null;
        MatchStatus = null;
        MatchSeed = 0;
        MatchStartedAt = null;
        LastCompletedMatchId = null;
        ExpectedMatchPlayerCount = 0;
        Players.Clear();
    }
}
