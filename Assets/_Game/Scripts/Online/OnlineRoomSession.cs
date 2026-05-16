using System.Collections.Generic;

public static class OnlineRoomSession
{
    public static string RoomId;
    public static string RoomCode;
    public static string HostId;
    public static string Status;
    public static int MaxPlayers;
    public static List<RoomService.RoomPlayerData> Players = new List<RoomService.RoomPlayerData>();

    public static bool IsInRoom => !string.IsNullOrEmpty(RoomId);
    public static bool IsHost => IsInRoom && HostId == SupabaseSession.UserId;

    public static void SetRoom(RoomService.RoomData room)
    {
        RoomId = room.room_id;
        RoomCode = room.room_code;
        HostId = room.host_id;
        Status = room.status;
        MaxPlayers = room.max_players;
    }

    public static void Clear()
    {
        RoomId = null;
        RoomCode = null;
        HostId = null;
        Status = null;
        MaxPlayers = 0;
        Players.Clear();
    }
}
