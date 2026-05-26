using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
public class NetworkMatchManager : MonoBehaviour
{
    public static NetworkMatchManager Instance { get; private set; }

    [SerializeField] private bool autoStartWhenOnlineMatchExists = true;
    [SerializeField] private float evaluationStartDelay = 2f;
    [SerializeField] private float evaluationInterval = 0.25f;
    [SerializeField] private bool requireExpectedPlayersBeforeLose = true;

    private float nextEvaluationTime;
    private float evaluationAllowedTime;
    private bool matchFinished;
    private GameState finishedState;

    public static NetworkMatchManager Ensure()
    {
        if (Instance != null)
        {
            return Instance;
        }

        NetworkMatchManager existing = FindFirstObjectByType<NetworkMatchManager>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject managerObject = new GameObject(nameof(NetworkMatchManager));
        Instance = managerObject.AddComponent<NetworkMatchManager>();
        return Instance;
    }

    public static bool IsOnlineMatchActive()
    {
        if (OnlineRoomSession.HasMatch)
        {
            return true;
        }

        NetworkRunner[] runners = FindObjectsByType<NetworkRunner>(FindObjectsSortMode.None);
        foreach (NetworkRunner runner in runners)
        {
            if (runner != null && runner.IsRunning)
            {
                return true;
            }
        }

        return false;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResetMatchState();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (!autoStartWhenOnlineMatchExists || matchFinished || !IsOnlineMatchActive())
        {
            return;
        }

        if (Time.unscaledTime < evaluationAllowedTime || Time.unscaledTime < nextEvaluationTime)
        {
            return;
        }

        nextEvaluationTime = Time.unscaledTime + Mathf.Max(0.05f, evaluationInterval);
        EvaluateMatchState();
    }

    public void ResetMatchState()
    {
        matchFinished = false;
        finishedState = default;
        evaluationAllowedTime = Time.unscaledTime + Mathf.Max(0f, evaluationStartDelay);
        nextEvaluationTime = evaluationAllowedTime;
    }

    public void ApplyNetworkResult(GameState resultState)
    {
        if (resultState != GameState.Victory && resultState != GameState.Lose)
        {
            return;
        }

        if (matchFinished)
        {
            return;
        }

        matchFinished = true;
        finishedState = resultState;

        Debug.Log($"NetworkMatchManager: match finished by network result '{resultState}'.");
        ApplyResultToLocalGameManager(resultState);
    }

    private void EvaluateMatchState()
    {
        if (AreAllSpawnersCleared())
        {
            FinishMatch(GameState.Victory);
            return;
        }

        if (AreAllPlayersUnableToContinue())
        {
            FinishMatch(GameState.Lose);
        }
    }

    private void FinishMatch(GameState resultState)
    {
        if (matchFinished)
        {
            return;
        }

        matchFinished = true;
        finishedState = resultState;

        Debug.Log($"NetworkMatchManager: local client decided match result '{resultState}', broadcasting.");

        if (!BroadcastResult(resultState))
        {
            Debug.LogWarning("NetworkMatchManager: could not broadcast result through a FusionPlayerAvatar. Applying locally only.");
        }

        ApplyResultToLocalGameManager(resultState);
    }

    private bool BroadcastResult(GameState resultState)
    {
        FusionPlayerAvatar[] avatars = FindObjectsByType<FusionPlayerAvatar>(FindObjectsSortMode.None);
        FusionPlayerAvatar fallback = null;

        foreach (FusionPlayerAvatar avatar in avatars)
        {
            if (avatar == null || !avatar.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (fallback == null)
            {
                fallback = avatar;
            }

            if (avatar.IsLocalPlayerAvatar)
            {
                return avatar.BroadcastMatchResult(resultState);
            }
        }

        return fallback != null && fallback.BroadcastMatchResult(resultState);
    }

    private void ApplyResultToLocalGameManager(GameState resultState)
    {
        if (GameManager.Instance == null)
        {
            Debug.LogWarning($"NetworkMatchManager: no GameManager found for result '{resultState}'.");
            return;
        }

        if (resultState == GameState.Victory)
        {
            GameManager.Instance.Victory();
        }
        else if (resultState == GameState.Lose)
        {
            GameManager.Instance.Lose();
        }
    }

    private bool AreAllSpawnersCleared()
    {
        Spawner[] spawners = FindObjectsByType<Spawner>(FindObjectsSortMode.None);
        int validSpawnerCount = 0;

        foreach (Spawner spawner in spawners)
        {
            if (spawner == null || !spawner.gameObject.activeInHierarchy)
            {
                continue;
            }

            validSpawnerCount++;
            if (!spawner.IsCleared)
            {
                return false;
            }
        }

        return validSpawnerCount > 0;
    }

    private bool AreAllPlayersUnableToContinue()
    {
        FusionPlayerAvatar[] avatars = FindObjectsByType<FusionPlayerAvatar>(FindObjectsSortMode.None);
        int activePlayerCount = 0;
        int unablePlayerCount = 0;

        foreach (FusionPlayerAvatar avatar in avatars)
        {
            if (avatar == null || !avatar.gameObject.activeInHierarchy)
            {
                continue;
            }

            NetworkObject networkObject = avatar.Object;
            if (networkObject == null || !networkObject.IsValid)
            {
                continue;
            }

            activePlayerCount++;
            if (avatar.IsUnableToContinueMatch)
            {
                unablePlayerCount++;
            }
        }

        if (activePlayerCount <= 0)
        {
            return false;
        }

        int expectedPlayerCount = GetExpectedPlayerCount();
        if (requireExpectedPlayersBeforeLose && expectedPlayerCount > 0 && activePlayerCount < expectedPlayerCount)
        {
            return false;
        }

        return unablePlayerCount >= activePlayerCount;
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

        return 0;
    }
}
