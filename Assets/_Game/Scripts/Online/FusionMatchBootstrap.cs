using System;
using System.Collections;
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
    [SerializeField] private float fallbackSpawnRadius = 1.8f;
    [SerializeField] private bool allowOfflineFallback = true;
    [SerializeField] private float waitForAllPlayersTimeout = 30f;

    private NetworkRunner runner;
    private NetworkObject localPlayerObject;
    private bool startRequested;
    private Coroutine waitForPlayersCoroutine;

    private async void Start()
    {
        ResolveReferences();

        string sessionName = GetSessionName();
        if (string.IsNullOrWhiteSpace(sessionName))
        {
            if (allowOfflineFallback)
            {
                Debug.LogWarning("FusionMatchBootstrap: no OnlineRoomSession.MatchId found, keeping offline scene player.");
                OnlineMatchLoadingOverlay.Hide();
                PlaceOfflinePlayer();
            }
            else
            {
                Debug.LogError("FusionMatchBootstrap: cannot start Fusion because OnlineRoomSession.MatchId is empty.");
            }

            return;
        }

        OnlineMatchLoadingOverlay.Show(0.6f);
        OnlineMatchStats.StartMatch(OnlineRoomSession.MatchId, OnlineRoomSession.Players);

        if (networkPlayerPrefab == null)
        {
            Debug.LogError("FusionMatchBootstrap: Network Player Prefab is not assigned.");
            OnlineMatchLoadingOverlay.Hide();
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
            OnlineMatchLoadingOverlay.Hide();
            return;
        }

        OnlineMatchLoadingOverlay.SetProgress(0.8f);
        Debug.Log($"FusionMatchBootstrap: joined Photon Fusion session '{sessionName}'.");
        NetworkMatchManager.Ensure().ResetMatchState();
        SpawnLocalPlayerIfNeeded(runner.LocalPlayer);
    }

    private void OnDestroy()
    {
        if (runner != null)
        {
            runner.RemoveCallbacks(this);
        }

        if (waitForPlayersCoroutine != null)
        {
            StopCoroutine(waitForPlayersCoroutine);
            waitForPlayersCoroutine = null;
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
            StartWaitForAllPlayersSpawned();
            return;
        }

        int spawnIndex = GetSpawnIndexForPlayer(player);
        Transform spawnPoint = GetSpawnPoint(spawnIndex);
        Vector3 spawnPosition = GetSpawnPosition(spawnPoint, spawnIndex);
        Quaternion spawnRotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

        localPlayerObject = runner.Spawn(networkPlayerPrefab, spawnPosition, spawnRotation, player);
        runner.SetPlayerObject(player, localPlayerObject);
        OnlineMatchLoadingOverlay.SetProgress(0.85f);

        FusionPlayerAvatar playerAvatar = localPlayerObject.GetComponent<FusionPlayerAvatar>();
        if (playerAvatar != null)
        {
            playerAvatar.SetInitialSpawn(spawnPosition, spawnRotation, spawnIndex);
        }

        Debug.Log(
            $"FusionMatchBootstrap: spawned local player {player} " +
            $"at index {spawnIndex}, position {spawnPosition}. " +
            $"Actual={localPlayerObject.transform.position}, RoomSlot={GetLocalRoomPlayerIndex()}, " +
            $"PlayerId={player.PlayerId}, AsIndex={player.AsIndex}, SpawnPoints={spawnPoints?.Length ?? 0}."
        );

        StartWaitForAllPlayersSpawned();
    }

    private void StartWaitForAllPlayersSpawned()
    {
        if (waitForPlayersCoroutine != null)
        {
            return;
        }

        waitForPlayersCoroutine = StartCoroutine(WaitForAllPlayersSpawnedRoutine());
    }

    private IEnumerator WaitForAllPlayersSpawnedRoutine()
    {
        int expectedPlayerCount = GetExpectedPlayerCount();
        float elapsedTime = 0f;

        while (elapsedTime < waitForAllPlayersTimeout)
        {
            int spawnedPlayerCount = CountSpawnedNetworkPlayers();
            bool localPlayerSpawned = localPlayerObject != null
                                      && localPlayerObject.IsValid;

            float playerProgress = expectedPlayerCount <= 0
                ? 1f
                : Mathf.Clamp01((float)spawnedPlayerCount / expectedPlayerCount);

            OnlineMatchLoadingOverlay.SetProgress(Mathf.Lerp(0.85f, 0.98f, playerProgress));

            if (localPlayerSpawned && spawnedPlayerCount >= expectedPlayerCount)
            {
                OnlineMatchLoadingOverlay.SetProgress(1f);
                yield return new WaitForSecondsRealtime(0.2f);
                OnlineMatchLoadingOverlay.Hide();
                waitForPlayersCoroutine = null;
                yield break;
            }

            elapsedTime += Time.unscaledDeltaTime;
            yield return null;
        }

        Debug.LogWarning(
            $"FusionMatchBootstrap: timed out waiting for all players to spawn. " +
            $"Spawned={CountSpawnedNetworkPlayers()}, Expected={expectedPlayerCount}."
        );

        OnlineMatchLoadingOverlay.Hide();
        waitForPlayersCoroutine = null;
    }

    private int GetExpectedPlayerCount()
    {
        if (OnlineRoomSession.ExpectedMatchPlayerCount > 0)
        {
            return OnlineRoomSession.ExpectedMatchPlayerCount;
        }

        if (OnlineRoomSession.Players != null && OnlineRoomSession.Players.Count > 0)
        {
            return OnlineRoomSession.Players.Count;
        }

        return 1;
    }

    private int CountSpawnedNetworkPlayers()
    {
        FusionPlayerAvatar[] playerAvatars = FindObjectsByType<FusionPlayerAvatar>(FindObjectsSortMode.None);
        int count = 0;

        foreach (FusionPlayerAvatar playerAvatar in playerAvatars)
        {
            if (playerAvatar == null || !playerAvatar.gameObject.activeInHierarchy)
            {
                continue;
            }

            NetworkObject playerObject = playerAvatar.Object;
            if (playerObject == null || !playerObject.IsValid)
            {
                continue;
            }

            count++;
        }

        return count;
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

    private Transform GetSpawnPoint(int spawnIndex)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            return null;
        }

        int pointIndex = Mathf.Abs(spawnIndex) % spawnPoints.Length;
        return spawnPoints[pointIndex];
    }

    private Vector3 GetSpawnPosition(Transform spawnPoint, int spawnIndex)
    {
        Vector3 basePosition = spawnPoint != null ? spawnPoint.position : Vector3.zero;

        if (spawnPoints != null && spawnPoints.Length > 1)
        {
            return basePosition;
        }

        if (spawnIndex <= 0)
        {
            return basePosition;
        }

        float angle = spawnIndex * 137.508f * Mathf.Deg2Rad;
        float radius = Mathf.Max(0.5f, fallbackSpawnRadius);
        return basePosition + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
    }

    private int GetSpawnIndexForPlayer(PlayerRef player)
    {
        int roomPlayerIndex = GetLocalRoomPlayerIndex();
        if (roomPlayerIndex >= 0)
        {
            return roomPlayerIndex;
        }

        if (player.IsRealPlayer && player.PlayerId > 0)
        {
            return player.PlayerId - 1;
        }

        if (player.IsRealPlayer && player.AsIndex > 0)
        {
            return player.AsIndex - 1;
        }

        return 0;
    }

    private int GetLocalRoomPlayerIndex()
    {
        List<RoomService.RoomPlayerData> players = OnlineRoomSession.Players;
        if (players == null || players.Count == 0 || string.IsNullOrWhiteSpace(SupabaseSession.UserId))
        {
            return -1;
        }

        int index = players.FindIndex(player => player != null && player.user_id == SupabaseSession.UserId);
        return index;
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

        int roomPlayerIndex = GetLocalRoomPlayerIndex();
        Transform spawnPoint = GetSpawnPoint(roomPlayerIndex >= 0 ? roomPlayerIndex : 0);
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
