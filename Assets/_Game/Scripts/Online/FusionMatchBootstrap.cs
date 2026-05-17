using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using UnityEngine;

public class FusionMatchBootstrap : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private NetworkObject networkPlayerPrefab;
    [SerializeField] private Transform spawnPointRoot;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private Player scenePlayerTemplate;
    [SerializeField] private int maxPlayers = 4;
    [SerializeField] private bool allowOfflineFallback = true;

    private NetworkRunner runner;
    private NetworkObject localPlayerObject;
    private bool startRequested;

    private async void Start()
    {
        ResolveReferences();

        string sessionName = GetSessionName();
        if (string.IsNullOrWhiteSpace(sessionName))
        {
            if (allowOfflineFallback)
            {
                Debug.LogWarning("FusionMatchBootstrap: no OnlineRoomSession.MatchId found, keeping offline scene player.");
                PlaceOfflinePlayer();
            }
            else
            {
                Debug.LogError("FusionMatchBootstrap: cannot start Fusion because OnlineRoomSession.MatchId is empty.");
            }

            return;
        }

        if (networkPlayerPrefab == null)
        {
            Debug.LogError("FusionMatchBootstrap: Network Player Prefab is not assigned.");
            return;
        }

        Debug.Log(
            $"FusionMatchBootstrap: starting Photon. " +
            $"User={SupabaseSession.UserId}, Room={OnlineRoomSession.RoomId}, Match={OnlineRoomSession.MatchId}, " +
            $"Session={sessionName}, RoomPlayers={OnlineRoomSession.Players?.Count ?? 0}"
        );

        DisableScenePlayerTemplate();

        runner = gameObject.AddComponent<NetworkRunner>();
        runner.ProvideInput = false;
        runner.AddCallbacks(this);

        NetworkSceneManagerDefault sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();
        startRequested = true;

        StartGameResult result = await runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Shared,
            SessionName = sessionName,
            SceneManager = sceneManager,
            PlayerCount = Mathf.Max(1, maxPlayers),
            IsOpen = true,
            IsVisible = true,
            EnableClientSessionCreation = true,
            AuthValues = new AuthenticationValues(GetPhotonUserId())
        });

        startRequested = false;

        if (!result.Ok)
        {
            Debug.LogError($"FusionMatchBootstrap: failed to join match session '{sessionName}'. Reason: {result.ShutdownReason}");
            return;
        }

        Debug.Log($"FusionMatchBootstrap: joined Photon Fusion session '{sessionName}'.");
        SpawnLocalPlayerIfNeeded(runner.LocalPlayer);
    }

    private void OnDestroy()
    {
        if (runner != null)
        {
            runner.RemoveCallbacks(this);
        }
    }

    public void OnPlayerJoined(NetworkRunner currentRunner, PlayerRef player)
    {
        Debug.Log($"FusionMatchBootstrap: OnPlayerJoined {player}, LocalPlayer={currentRunner.LocalPlayer}.");

        if (currentRunner != runner || player != currentRunner.LocalPlayer)
        {
            return;
        }

        SpawnLocalPlayerIfNeeded(player);
    }

    public void OnPlayerLeft(NetworkRunner currentRunner, PlayerRef player)
    {
        if (currentRunner == null || player != currentRunner.LocalPlayer)
        {
            return;
        }

        if (currentRunner.TryGetPlayerObject(player, out NetworkObject playerObject) && playerObject != null)
        {
            currentRunner.Despawn(playerObject);
        }
    }

    public void OnShutdown(NetworkRunner currentRunner, ShutdownReason shutdownReason)
    {
        Debug.Log($"FusionMatchBootstrap: runner shutdown. Reason: {shutdownReason}");
    }

    public void OnDisconnectedFromServer(NetworkRunner currentRunner, NetDisconnectReason reason)
    {
        Debug.LogWarning($"FusionMatchBootstrap: disconnected from server. Reason: {reason}");
    }

    public void OnConnectFailed(NetworkRunner currentRunner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Debug.LogError($"FusionMatchBootstrap: connect failed to {remoteAddress}. Reason: {reason}");
    }

    public void OnInput(NetworkRunner currentRunner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner currentRunner, PlayerRef player, NetworkInput input) { }
    public void OnConnectedToServer(NetworkRunner currentRunner) { }
    public void OnConnectRequest(NetworkRunner currentRunner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnUserSimulationMessage(NetworkRunner currentRunner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner currentRunner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner currentRunner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner currentRunner, HostMigrationToken hostMigrationToken) { }
    public void OnSceneLoadDone(NetworkRunner currentRunner) { }
    public void OnSceneLoadStart(NetworkRunner currentRunner) { }
    public void OnObjectEnterAOI(NetworkRunner currentRunner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner currentRunner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataReceived(NetworkRunner currentRunner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner currentRunner, PlayerRef player, ReliableKey key, float progress) { }

    private void SpawnLocalPlayerIfNeeded(PlayerRef player)
    {
        if (runner == null || !runner.IsRunning || startRequested || localPlayerObject != null)
        {
            return;
        }

        if (runner.TryGetPlayerObject(player, out NetworkObject existingPlayerObject) && existingPlayerObject != null)
        {
            localPlayerObject = existingPlayerObject;
            return;
        }

        Transform spawnPoint = GetLocalSpawnPoint();
        Vector3 spawnPosition = spawnPoint != null ? spawnPoint.position : Vector3.zero;
        Quaternion spawnRotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

        localPlayerObject = runner.Spawn(networkPlayerPrefab, spawnPosition, spawnRotation, player);
        runner.SetPlayerObject(player, localPlayerObject);

        Debug.Log($"FusionMatchBootstrap: spawned local player at {spawnPosition}.");
    }

    private void ResolveReferences()
    {
        if (scenePlayerTemplate == null)
        {
            scenePlayerTemplate = FindFirstObjectByType<Player>();
        }

        if (spawnPointRoot == null)
        {
            GameObject spawnRootObject = GameObject.Find("PlayerSpawnPoints");
            if (spawnRootObject != null)
            {
                spawnPointRoot = spawnRootObject.transform;
            }
        }

        if ((spawnPoints == null || spawnPoints.Length == 0) && spawnPointRoot != null)
        {
            List<Transform> points = new List<Transform>();
            foreach (Transform child in spawnPointRoot)
            {
                if (child.name.StartsWith("SpawnPoint_"))
                {
                    points.Add(child);
                }
            }

            points.Sort((a, b) => GetSpawnIndex(a.name).CompareTo(GetSpawnIndex(b.name)));
            spawnPoints = points.ToArray();
        }

        if (maxPlayers <= 0 && OnlineRoomSession.Players != null && OnlineRoomSession.Players.Count > 0)
        {
            maxPlayers = OnlineRoomSession.Players.Count;
        }
    }

    private string GetSessionName()
    {
        if (!string.IsNullOrWhiteSpace(OnlineRoomSession.MatchId))
        {
            return OnlineRoomSession.MatchId;
        }

        return null;
    }

    private string GetPhotonUserId()
    {
        if (!string.IsNullOrWhiteSpace(SupabaseSession.UserId))
        {
            return SupabaseSession.UserId;
        }

        if (!string.IsNullOrWhiteSpace(SupabaseSession.Email))
        {
            return SupabaseSession.Email;
        }

        return SystemInfo.deviceUniqueIdentifier;
    }

    private Transform GetLocalSpawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            return null;
        }

        int spawnIndex = GetLocalPlayerIndex();
        spawnIndex = Mathf.Clamp(spawnIndex, 0, spawnPoints.Length - 1);
        return spawnPoints[spawnIndex];
    }

    private int GetLocalPlayerIndex()
    {
        List<RoomService.RoomPlayerData> players = OnlineRoomSession.Players;
        if (players == null || players.Count == 0 || string.IsNullOrWhiteSpace(SupabaseSession.UserId))
        {
            return 0;
        }

        int index = players.FindIndex(player => player != null && player.user_id == SupabaseSession.UserId);
        return index >= 0 ? index : 0;
    }

    private void DisableScenePlayerTemplate()
    {
        if (scenePlayerTemplate != null)
        {
            scenePlayerTemplate.gameObject.SetActive(false);
        }
    }

    private void PlaceOfflinePlayer()
    {
        if (scenePlayerTemplate == null)
        {
            return;
        }

        Transform spawnPoint = GetLocalSpawnPoint();
        if (spawnPoint != null)
        {
            scenePlayerTemplate.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
        }

        scenePlayerTemplate.gameObject.SetActive(true);
        scenePlayerTemplate.gameObject.tag = "Player";
        scenePlayerTemplate.enabled = true;
    }

    private int GetSpawnIndex(string spawnName)
    {
        int underscoreIndex = spawnName.LastIndexOf('_');
        if (underscoreIndex < 0 || underscoreIndex >= spawnName.Length - 1)
        {
            return int.MaxValue;
        }

        string indexText = spawnName.Substring(underscoreIndex + 1);
        return int.TryParse(indexText, out int index) ? index : int.MaxValue;
    }
}
