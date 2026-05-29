using System;
using System.Collections.Generic;
using UnityEngine;

public static class OnlineMatchStats
{
    private static readonly Dictionary<string, MatchPlayerStats> StatsByUserId = new Dictionary<string, MatchPlayerStats>();

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
                AddKill(userId);
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
        }
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
}
