using System;
using System.Collections.Generic;
using UnityEngine;

public static class OnlineMatchStats
{
    private static readonly Dictionary<string, MatchPlayerStats> StatsByUserId = new Dictionary<string, MatchPlayerStats>();
    private static readonly Dictionary<string, string> DisplayNameByUserId = new Dictionary<string, string>();

    private static string currentMatchId;
    private static float matchStartRealtime;
    private static bool matchStarted;

    public static void StartMatch(string matchId, IEnumerable<RoomService.RoomPlayerData> players)
    {
        if (matchStarted && currentMatchId == matchId)
        {
            RegisterPlayers(players);
            return;
        }

        currentMatchId = matchId;
        matchStartRealtime = Time.realtimeSinceStartup;
        matchStarted = true;
        StatsByUserId.Clear();
        DisplayNameByUserId.Clear();
        RegisterPlayers(players);

        Debug.Log($"OnlineMatchStats: started match '{currentMatchId}'.");
    }

    public static void EnsureStarted()
    {
        if (matchStarted)
        {
            RegisterPlayers(OnlineRoomSession.Players);
            return;
        }

        StartMatch(OnlineRoomSession.MatchId, OnlineRoomSession.Players);
    }

    public static void AddDamage(string userId, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        GetOrCreate(userId).damageDealt += amount;
    }

    public static void AddKill(string userId)
    {
        GetOrCreate(userId).kills++;
    }

    public static void AddDown(string userId)
    {
        GetOrCreate(userId).downs++;
    }

    public static void AddRevive(string userId)
    {
        GetOrCreate(userId).revives++;
    }

    public static void MarkDead(string userId)
    {
        MatchPlayerStats stats = GetOrCreate(userId);
        stats.isDead = true;

        if (stats.eliminatedRealtime < 0f)
        {
            stats.eliminatedRealtime = Time.realtimeSinceStartup;
        }
    }

    public static void AddTestWinStatsToHost(int damage, int kills)
    {
        EnsureStarted();

        string hostUserId = OnlineRoomSession.HostId;
        if (string.IsNullOrWhiteSpace(hostUserId))
        {
            hostUserId = SupabaseSession.UserId;
        }

        if (string.IsNullOrWhiteSpace(hostUserId))
        {
            Debug.LogWarning("OnlineMatchStats: cannot add test win stats because host user id is empty.");
            return;
        }

        int safeDamage = Mathf.Max(0, damage);
        int safeKills = Mathf.Max(0, kills);

        if (safeDamage > 0)
        {
            FusionPlayerAvatar.BroadcastMatchStatEvent(StatEventType.Damage, hostUserId, safeDamage);
        }

        if (safeKills > 0)
        {
            FusionPlayerAvatar.BroadcastMatchStatEvent(StatEventType.Kill, hostUserId, safeKills);
        }

        MatchPlayerStats stats = GetOrCreate(hostUserId);
        Debug.Log(
            $"OnlineMatchStats: test win stats assigned to host={hostUserId}. " +
            $"kills={stats.kills}, damage={stats.damageDealt}."
        );
    }

    public static MatchPlayerStats GetStats(string userId)
    {
        return GetOrCreate(userId);
    }

    public static int GetSurviveTimeSeconds(string userId)
    {
        MatchPlayerStats stats = GetOrCreate(userId);
        float endTime = stats.eliminatedRealtime >= 0f ? stats.eliminatedRealtime : Time.realtimeSinceStartup;
        return Mathf.Max(0, Mathf.FloorToInt(endTime - matchStartRealtime));
    }

    public static int GetMatchElapsedSeconds()
    {
        if (!matchStarted)
        {
            EnsureStarted();
        }

        if (!matchStarted)
        {
            return 0;
        }

        return Mathf.Max(0, Mathf.FloorToInt(Time.realtimeSinceStartup - matchStartRealtime));
    }

    public static void ApplyNetworkEvent(StatEventType eventType, string userId, int amount)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            Debug.LogWarning($"OnlineMatchStats: ignored {eventType} because userId is empty.");
            return;
        }

        EnsureStarted();

        switch (eventType)
        {
            case StatEventType.Damage:
                AddDamage(userId, amount);
                break;
            case StatEventType.Kill:
                int killCount = Mathf.Max(1, amount);
                for (int i = 0; i < killCount; i++)
                {
                    AddKill(userId);
                }
                break;
            case StatEventType.Down:
                AddDown(userId);
                break;
            case StatEventType.Revive:
                AddRevive(userId);
                break;
            case StatEventType.Dead:
                MarkDead(userId);
                break;
        }

        MatchPlayerStats stats = GetOrCreate(userId);
        Debug.Log(
            $"OnlineMatchStats: {eventType} user={userId}, amount={amount}. " +
            $"kills={stats.kills}, downs={stats.downs}, revives={stats.revives}, " +
            $"damage={stats.damageDealt}, survive={GetSurviveTimeSeconds(userId)}, dead={stats.isDead}."
        );
    }

    private static void RegisterPlayers(IEnumerable<RoomService.RoomPlayerData> players)
    {
        if (players == null)
        {
            return;
        }

        foreach (RoomService.RoomPlayerData player in players)
        {
            if (player == null || string.IsNullOrWhiteSpace(player.user_id))
            {
                continue;
            }

            GetOrCreate(player.user_id);
            DisplayNameByUserId[player.user_id] = GetBestDisplayName(player);
        }
    }

    public static List<MatchLeaderboardRow> GetLeaderboardSnapshot()
    {
        EnsureStarted();

        List<MatchLeaderboardRow> rows = new List<MatchLeaderboardRow>();
        foreach (KeyValuePair<string, MatchPlayerStats> entry in StatsByUserId)
        {
            MatchPlayerStats stats = entry.Value;
            rows.Add(new MatchLeaderboardRow
            {
                userId = entry.Key,
                displayName = GetDisplayName(entry.Key),
                kills = stats.kills,
                damageDealt = stats.damageDealt,
                revives = stats.revives
            });
        }

        rows.Sort((left, right) =>
        {
            int compare = right.kills.CompareTo(left.kills);
            if (compare != 0)
            {
                return compare;
            }

            compare = right.damageDealt.CompareTo(left.damageDealt);
            if (compare != 0)
            {
                return compare;
            }

            compare = right.revives.CompareTo(left.revives);
            if (compare != 0)
            {
                return compare;
            }

            return string.Compare(left.displayName, right.displayName, StringComparison.OrdinalIgnoreCase);
        });

        for (int i = 0; i < rows.Count; i++)
        {
            rows[i].rank = i + 1;
        }

        return rows;
    }

    private static string GetDisplayName(string userId)
    {
        if (!string.IsNullOrWhiteSpace(userId) && DisplayNameByUserId.TryGetValue(userId, out string displayName) && !string.IsNullOrWhiteSpace(displayName))
        {
            return displayName;
        }

        if (!string.IsNullOrWhiteSpace(userId) && userId == SupabaseSession.UserId)
        {
            if (!string.IsNullOrWhiteSpace(SupabaseSession.DisplayName))
            {
                return SupabaseSession.DisplayName;
            }

            if (!string.IsNullOrWhiteSpace(SupabaseSession.Username))
            {
                return SupabaseSession.Username;
            }
        }

        return string.IsNullOrWhiteSpace(userId) ? "Player" : userId;
    }

    private static string GetBestDisplayName(RoomService.RoomPlayerData player)
    {
        if (player == null)
        {
            return "Player";
        }

        if (!string.IsNullOrWhiteSpace(player.display_name))
        {
            return player.display_name;
        }

        return string.IsNullOrWhiteSpace(player.user_id) ? "Player" : player.user_id;
    }

    private static MatchPlayerStats GetOrCreate(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            userId = SupabaseSession.UserId;
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            userId = "unknown";
        }

        if (!StatsByUserId.TryGetValue(userId, out MatchPlayerStats stats))
        {
            stats = new MatchPlayerStats
            {
                userId = userId,
                eliminatedRealtime = -1f
            };

            StatsByUserId[userId] = stats;
        }

        return stats;
    }

    public enum StatEventType
    {
        Damage = 0,
        Kill = 1,
        Down = 2,
        Revive = 3,
        Dead = 4
    }

    [Serializable]
    public class MatchPlayerStats
    {
        public string userId;
        public int kills;
        public int downs;
        public int revives;
        public int damageDealt;
        public bool isDead;
        public float eliminatedRealtime = -1f;
    }

    public class MatchLeaderboardRow
    {
        public int rank;
        public string userId;
        public string displayName;
        public int kills;
        public int damageDealt;
        public int revives;
    }
}
