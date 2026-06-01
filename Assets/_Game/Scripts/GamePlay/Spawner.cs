using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.AI;

public class Spawner : MonoBehaviour
{
    [SerializeField] private int networkId;
    [SerializeField] private Transform spawnPointRoot;
    [SerializeField] private Gate[] gatesToOpen;
    [SerializeField] private bool projectSpawnPointDownToNavMesh = true;
    [SerializeField] private float navMeshProjectionDistance = 20f;
    [Header("Difficulty Scaling")]
    [SerializeField] private bool enableDifficultyScaling = true;
    [SerializeField] private float difficultyPerWave = 0.2f;
    [SerializeField] private float difficultyPerMinute = 0.05f;
    [SerializeField] private float difficultyPerExtraPlayer = 0.15f;
    [SerializeField] private float maxDifficultyMultiplier = 3f;

    private List<SpawnPoint> spawnPointList;

    private bool hasSpawned;
    private bool hasCleared;
    private float nextSpawnRequestTime;
    private int aliveEnemyCount;

    public new Collider collider;
    public bool IsCleared => hasCleared;
    public event Action<Spawner> Cleared;
    private int NetworkId => networkId != 0 ? networkId : ComputeStableNetworkId();
    public int NetworkIdValue => NetworkId;

    private void Awake()
    {
        Transform root = spawnPointRoot != null ? spawnPointRoot : transform.parent;
        if (root == null)
        {
            root = transform;
        }

        SpawnPoint[] spawnPointArray = root.GetComponentsInChildren<SpawnPoint>();
        spawnPointList = new List<SpawnPoint>(spawnPointArray);

        Debug.Log(
            $"Spawner[{NetworkId}] '{GetHierarchyPath(transform)}': Awake. " +
            $"spawnPointRoot='{root.name}', spawnPoints={spawnPointList.Count}, gates={gatesToOpen?.Length ?? 0}."
        );
    }

    public void SpawnCharacters()
    {
        Debug.Log(
            $"Spawner[{NetworkId}] '{GetHierarchyPath(transform)}': SpawnCharacters called. " +
            $"hasSpawned={hasSpawned}, hasCleared={hasCleared}, spawnPoints={spawnPointList?.Count ?? 0}."
        );

        if (hasSpawned)
        {
            Debug.Log($"Spawner[{NetworkId}] '{name}': ignored because this spawner already spawned.");
            return;
        }

        NetworkRunner networkRunner = FindActiveNetworkRunner();
        if (networkRunner != null && networkRunner.IsRunning)
        {
            Debug.Log(
                $"Spawner[{NetworkId}] '{name}': active NetworkRunner found. " +
                $"LocalPlayer={networkRunner.LocalPlayer}, IsSharedModeMasterClient={networkRunner.IsSharedModeMasterClient}."
            );

            hasSpawned = true;
            aliveEnemyCount = 0;
            int spawnedCount = SpawnNetworkCharacters(networkRunner);
            if (spawnedCount > 0)
            {
                BroadcastSpawnRequestToNetwork();
            }

            return;
        }

        hasSpawned = true;
        aliveEnemyCount = 0;
        Debug.Log($"Spawner[{NetworkId}] '{name}': no active NetworkRunner, spawning offline enemies.");
        SpawnOfflineCharacters();
    }

    private void SpawnFromNetworkRequest(PlayerRef activatingPlayer)
    {
        Debug.Log(
            $"Spawner[{NetworkId}] '{GetHierarchyPath(transform)}': received spawn request. " +
            $"activatingPlayer={activatingPlayer}, hasSpawned={hasSpawned}, hasCleared={hasCleared}."
        );

        if (hasCleared)
        {
            Debug.Log($"Spawner[{NetworkId}] '{name}': spawn request ignored because hasCleared=True.");
            return;
        }

        NetworkRunner networkRunner = FindActiveNetworkRunner();
        if (networkRunner == null || !networkRunner.IsRunning)
        {
            Debug.LogWarning($"Spawner[{NetworkId}] '{name}': spawn request ignored because there is no active NetworkRunner.");
            return;
        }

        if (activatingPlayer != PlayerRef.None && networkRunner.LocalPlayer != activatingPlayer)
        {
            if (!hasSpawned)
            {
                hasSpawned = true;
                aliveEnemyCount = 0;
                Debug.Log(
                    $"Spawner[{NetworkId}] '{name}': marked as spawned by remote activating player {activatingPlayer}. " +
                    "Waiting for network enemies from that client."
                );
            }

            return;
        }

        if (hasSpawned)
        {
            Debug.Log($"Spawner[{NetworkId}] '{name}': spawn request ignored because hasSpawned={hasSpawned}, hasCleared={hasCleared}.");
            return;
        }

        hasSpawned = true;
        aliveEnemyCount = 0;
        SpawnNetworkCharacters(networkRunner);
    }

    private void SpawnOfflineCharacters()
    {
        foreach (SpawnPoint point in spawnPointList)
        {
            if (point.EnemyToSpawn == null)
            {
                Debug.LogWarning($"Spawner[{NetworkId}] '{name}': spawn point '{point.name}' has no EnemyToSpawn assigned.");
                continue;
            }

            Vector3 spawnPosition = ResolveSpawnPosition(point);
            Quaternion spawnRotation = point.transform.rotation;

            Debug.Log(
                $"Spawner[{NetworkId}] '{name}': offline spawn '{point.EnemyToSpawn.name}' " +
                $"at {spawnPosition} from point '{point.name}'. original={point.transform.position}."
            );

            GameObject spawnedGameobject = Instantiate(point.EnemyToSpawn, spawnPosition, spawnRotation);
            Character spawnedCharacter = spawnedGameobject.GetComponent<Character>();

            if (spawnedCharacter == null)
            {
                Debug.LogWarning($"Spawner[{NetworkId}] '{name}': offline spawned object has no Character component.");
                continue;
            }

            aliveEnemyCount++;
            spawnedCharacter.Died += OnSpawnedCharacterDied;
            ApplyDifficultyToSpawnedCharacter(spawnedCharacter, null);
            RefreshEnemyTarget(spawnedCharacter);
            spawnedCharacter.PlaySpawnDissolve();
        }

        Debug.Log($"Spawner[{NetworkId}] '{name}': offline spawn finished. aliveEnemyCount={aliveEnemyCount}.");

        if (aliveEnemyCount <= 0)
        {
            ClearSpawner();
        }
    }

    private int SpawnNetworkCharacters(NetworkRunner networkRunner)
    {
        int spawnedCount = 0;

        foreach (SpawnPoint point in spawnPointList)
        {
            if (point.EnemyToSpawn == null)
            {
                Debug.LogWarning($"Spawner[{NetworkId}] '{name}': spawn point '{point.name}' has no EnemyToSpawn assigned.");
                continue;
            }

            NetworkObject enemyNetworkObject = point.EnemyToSpawn.GetComponent<NetworkObject>();
            if (enemyNetworkObject == null)
            {
                Debug.LogError($"Spawner[{NetworkId}] '{name}': enemy prefab '{point.EnemyToSpawn.name}' is missing NetworkObject. Falling back to local spawn.");
                SpawnOfflineCharacter(point);
                continue;
            }

            Vector3 spawnPosition = ResolveSpawnPosition(point);
            Quaternion spawnRotation = point.transform.rotation;

            Debug.Log(
                $"Spawner[{NetworkId}] '{name}': network spawning '{point.EnemyToSpawn.name}' " +
                $"at {spawnPosition} from point '{point.name}'. original={point.transform.position}."
            );

            NetworkObject spawnedObject = networkRunner.Spawn(
                enemyNetworkObject,
                spawnPosition,
                spawnRotation,
                PlayerRef.None,
                null,
                NetworkSpawnFlags.SharedModeStateAuthLocalPlayer
            );

            if (spawnedObject == null)
            {
                Debug.LogError(
                    $"Spawner[{NetworkId}] '{name}': runner.Spawn returned null for '{point.EnemyToSpawn.name}'. " +
                    "Check Fusion Network Prefab/Object Table."
                );
                continue;
            }

            Character spawnedCharacter = spawnedObject.GetComponent<Character>();
            if (spawnedCharacter == null)
            {
                spawnedCharacter = spawnedObject.GetComponentInChildren<Character>();
            }

            if (spawnedCharacter == null)
            {
                Debug.LogWarning($"Spawner[{NetworkId}] '{name}': spawned object '{spawnedObject.name}' has no Character component.");
                continue;
            }

            aliveEnemyCount++;
            spawnedCount++;
            spawnedCharacter.Died += OnSpawnedCharacterDied;
            ApplyDifficultyToSpawnedCharacter(spawnedCharacter, spawnedObject);
            RefreshEnemyTarget(spawnedCharacter);
            Debug.Log($"Spawner[{NetworkId}] '{name}': spawned '{spawnedObject.name}'. aliveEnemyCount={aliveEnemyCount}.");
        }

        Debug.Log($"Spawner[{NetworkId}] '{name}': network spawn finished. aliveEnemyCount={aliveEnemyCount}.");

        if (aliveEnemyCount <= 0)
        {
            hasSpawned = false;
            Debug.LogWarning(
                $"Spawner[{NetworkId}] '{name}': no network enemies were counted after spawn. " +
                "Keeping spawner uncleared so it can be retried. Check enemy prefab Character/NetworkObject setup."
            );
        }

        return spawnedCount;
    }

    private void SpawnOfflineCharacter(SpawnPoint point)
    {
        Vector3 spawnPosition = ResolveSpawnPosition(point);
        Quaternion spawnRotation = point.transform.rotation;

        Debug.Log(
            $"Spawner[{NetworkId}] '{name}': offline fallback spawn '{point.EnemyToSpawn.name}' " +
            $"at {spawnPosition} from point '{point.name}'. original={point.transform.position}."
        );

        GameObject spawnedGameobject = Instantiate(point.EnemyToSpawn, spawnPosition, spawnRotation);
        Character spawnedCharacter = spawnedGameobject.GetComponent<Character>();

        if (spawnedCharacter == null)
        {
            Debug.LogWarning($"Spawner[{NetworkId}] '{name}': offline fallback object has no Character component.");
            return;
        }

        aliveEnemyCount++;
        spawnedCharacter.Died += OnSpawnedCharacterDied;
        ApplyDifficultyToSpawnedCharacter(spawnedCharacter, null);
        RefreshEnemyTarget(spawnedCharacter);
        spawnedCharacter.PlaySpawnDissolve();
    }

    private void ApplyDifficultyToSpawnedCharacter(Character spawnedCharacter, NetworkObject spawnedNetworkObject)
    {
        if (!enableDifficultyScaling || spawnedCharacter == null)
        {
            return;
        }

        float multiplier = CalculateDifficultyMultiplier();

        FusionEnemyAvatar enemyAvatar = spawnedNetworkObject != null
            ? spawnedNetworkObject.GetComponent<FusionEnemyAvatar>()
            : spawnedCharacter.GetComponent<FusionEnemyAvatar>();

        if (enemyAvatar != null && enemyAvatar.Object != null && enemyAvatar.Object.IsValid)
        {
            enemyAvatar.ApplyDifficulty(NetworkId, multiplier);
            return;
        }

        Health spawnedHealth = spawnedCharacter.GetComponent<Health>();
        if (spawnedHealth != null)
        {
            spawnedHealth.ApplyMaxHealthMultiplier(multiplier, true);
        }

        DamageCaster spawnedDamageCaster = spawnedCharacter.GetComponentInChildren<DamageCaster>(true);
        if (spawnedDamageCaster != null)
        {
            spawnedDamageCaster.ApplyDamageMultiplier(multiplier);
        }

        Enemy_02_shoot rangedAttack = spawnedCharacter.GetComponent<Enemy_02_shoot>();
        if (rangedAttack != null)
        {
            rangedAttack.ApplyDamageMultiplier(multiplier);
        }

        float speedMultiplier = Mathf.Lerp(1f, multiplier, 0.35f);
        spawnedCharacter.ApplyRuntimeMoveSpeedMultiplier(speedMultiplier);

        Debug.Log(
            $"Spawner[{NetworkId}] '{name}': applied offline difficulty multiplier {multiplier:0.00} " +
            $"to '{spawnedCharacter.name}'."
        );
    }

    private float CalculateDifficultyMultiplier()
    {
        if (!enableDifficultyScaling)
        {
            return 1f;
        }

        int waveIndex = Mathf.Max(0, NetworkId - 1);
        int elapsedSeconds = OnlineMatchStats.GetMatchElapsedSeconds();
        float elapsedMinutes = elapsedSeconds / 60f;
        int extraPlayers = Mathf.Max(0, GetExpectedPlayerCount() - 1);

        float multiplier = 1f
            + waveIndex * Mathf.Max(0f, difficultyPerWave)
            + elapsedMinutes * Mathf.Max(0f, difficultyPerMinute)
            + extraPlayers * Mathf.Max(0f, difficultyPerExtraPlayer);

        return Mathf.Clamp(multiplier, 1f, Mathf.Max(1f, maxDifficultyMultiplier));
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

        return Mathf.Max(1, FindObjectsByType<FusionPlayerAvatar>(FindObjectsSortMode.None).Length);
    }

    private void RefreshEnemyTarget(Character spawnedCharacter)
    {
        if (spawnedCharacter is Enemy spawnedEnemy)
        {
            spawnedEnemy.RefreshClosestPlayerTarget();
        }
    }

    private Vector3 ResolveSpawnPosition(SpawnPoint point)
    {
        Vector3 position = point != null ? point.transform.position : transform.position;
        if (!projectSpawnPointDownToNavMesh)
        {
            return position;
        }

        if (TryProjectDownToNavMesh(position, out Vector3 projectedPosition))
        {
            Debug.Log(
                $"Spawner[{NetworkId}] '{name}': projected spawn point '{point?.name}' down to NavMesh. " +
                $"from {position} to {projectedPosition}."
            );
            return projectedPosition;
        }

        Debug.LogWarning(
            $"Spawner[{NetworkId}] '{name}': could not project spawn point '{point?.name}' down to NavMesh. " +
            $"Using original position {position}."
        );
        return position;
    }

    private bool TryProjectDownToNavMesh(Vector3 position, out Vector3 projectedPosition)
    {
        float projectionDistance = Mathf.Max(0.1f, navMeshProjectionDistance);
        Vector3 rayStart = position + Vector3.up * 0.25f;

        if (NavMesh.Raycast(rayStart, rayStart + Vector3.down * projectionDistance, out NavMeshHit rayHit, NavMesh.AllAreas))
        {
            projectedPosition = rayHit.position;
            return true;
        }

        float sampleRadius = 0.2f;
        int sampleSteps = Mathf.CeilToInt(projectionDistance / sampleRadius);
        for (int i = 0; i <= sampleSteps; i++)
        {
            Vector3 samplePosition = position + Vector3.down * (i * sampleRadius);
            if (NavMesh.SamplePosition(samplePosition, out NavMeshHit sampleHit, sampleRadius, NavMesh.AllAreas))
            {
                Vector3 hitPosition = sampleHit.position;
                if (Mathf.Abs(hitPosition.x - position.x) <= sampleRadius * 1.5f
                    && Mathf.Abs(hitPosition.z - position.z) <= sampleRadius * 1.5f)
                {
                    projectedPosition = new Vector3(position.x, hitPosition.y, position.z);
                    return true;
                }
            }
        }

        projectedPosition = position;
        return false;
    }

    private NetworkRunner FindActiveNetworkRunner()
    {
        NetworkRunner[] runners = FindObjectsByType<NetworkRunner>(FindObjectsSortMode.None);

        foreach (NetworkRunner runner in runners)
        {
            if (runner != null && runner.IsRunning)
            {
                return runner;
            }
        }

        return null;
    }

    private void OnSpawnedCharacterDied(Character spawnedCharacter)
    {
        spawnedCharacter.Died -= OnSpawnedCharacterDied;
        aliveEnemyCount = Mathf.Max(aliveEnemyCount - 1, 0);
        Debug.Log($"Spawner[{NetworkId}] '{name}': enemy died. aliveEnemyCount={aliveEnemyCount}.");

        if (aliveEnemyCount <= 0)
        {
            ClearSpawner();
        }
    }

    private void ClearSpawner()
    {
        if (hasCleared)
        {
            Debug.Log($"Spawner[{NetworkId}] '{name}': ClearSpawner ignored because already cleared.");
            return;
        }

        hasCleared = true;
        Debug.Log($"Spawner[{NetworkId}] '{name}': cleared locally. Opening gates and broadcasting.");
        OpenGate();
        BroadcastClearToNetwork();
        Cleared?.Invoke(this);
    }

    private void ForceClearFromNetwork()
    {
        if (hasCleared)
        {
            return;
        }

        hasSpawned = true;
        hasCleared = true;
        aliveEnemyCount = 0;
        Debug.Log($"Spawner[{NetworkId}] '{name}': received network clear. Opening gates.");
        OpenGate();
        Cleared?.Invoke(this);
    }

    private void OpenGate()
    {
        if (gatesToOpen == null)
        {
            Debug.Log($"Spawner[{NetworkId}] '{name}': no gatesToOpen array.");
            return;
        }

        foreach (Gate gate in gatesToOpen)
        {
            if (gate != null)
            {
                Debug.Log($"Spawner[{NetworkId}] '{name}': opening gate '{gate.name}'.");
                gate.OpenGate();
            }
            else
            {
                Debug.LogWarning($"Spawner[{NetworkId}] '{name}': gatesToOpen contains null gate.");
            }
        }
    }

    private void BroadcastClearToNetwork()
    {
        NetworkRunner networkRunner = FindActiveNetworkRunner();
        if (networkRunner == null || !networkRunner.IsRunning)
        {
            Debug.Log($"Spawner[{NetworkId}] '{name}': no active NetworkRunner for clear broadcast.");
            return;
        }

        FusionPlayerAvatar[] avatars = FindObjectsByType<FusionPlayerAvatar>(FindObjectsSortMode.None);
        foreach (FusionPlayerAvatar avatar in avatars)
        {
            if (avatar != null && avatar.BroadcastSpawnerCleared(NetworkId))
            {
                Debug.Log($"Spawner[{NetworkId}] '{name}': broadcasted clear through '{avatar.name}'.");
                return;
            }
        }

        Debug.LogWarning($"Spawner[{NetworkId}] '{name}': no FusionPlayerAvatar available to broadcast clear.");
    }

    private void BroadcastSpawnRequestToNetwork()
    {
        if (Time.time < nextSpawnRequestTime)
        {
            Debug.Log($"Spawner[{NetworkId}] '{name}': spawn request cooldown active, waiting before retry.");
            return;
        }

        nextSpawnRequestTime = Time.time + 1f;

        FusionPlayerAvatar requestAvatar = FindSpawnRequestAvatar();
        if (requestAvatar != null && requestAvatar.BroadcastSpawnerSpawnRequested(NetworkId))
        {
            Debug.Log(
                $"Spawner[{NetworkId}] '{name}': broadcasted spawned-state through " +
                $"'{requestAvatar.name}' PlayerRef={requestAvatar.NetworkPlayerRef}."
            );
            return;
        }

        Debug.LogWarning($"Spawner[{NetworkId}] '{name}': no FusionPlayerAvatar available to broadcast spawn request.");
    }

    private FusionPlayerAvatar FindSpawnRequestAvatar()
    {
        FusionPlayerAvatar[] avatars = FindObjectsByType<FusionPlayerAvatar>(FindObjectsSortMode.None);
        FusionPlayerAvatar fallbackAvatar = null;

        foreach (FusionPlayerAvatar avatar in avatars)
        {
            if (avatar == null || !avatar.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (fallbackAvatar == null)
            {
                fallbackAvatar = avatar;
            }

            if (avatar.IsLocalPlayerAvatar)
            {
                return avatar;
            }
        }

        return fallbackAvatar;
    }

    public static void OpenGatesForNetworkId(int spawnerNetworkId)
    {
        Spawner[] spawners = FindObjectsByType<Spawner>(FindObjectsSortMode.None);
        foreach (Spawner spawner in spawners)
        {
            if (spawner != null && spawner.NetworkId == spawnerNetworkId)
            {
                Debug.Log($"Spawner[{spawnerNetworkId}]: matched network clear on '{spawner.name}'.");
                spawner.ForceClearFromNetwork();
            }
        }
    }

    public static void SpawnForNetworkId(int spawnerNetworkId)
    {
        SpawnForNetworkId(spawnerNetworkId, PlayerRef.None);
    }

    public static void SpawnForNetworkId(int spawnerNetworkId, PlayerRef activatingPlayer)
    {
        Spawner[] spawners = FindObjectsByType<Spawner>(FindObjectsSortMode.None);
        foreach (Spawner spawner in spawners)
        {
            if (spawner != null && spawner.NetworkId == spawnerNetworkId)
            {
                Debug.Log($"Spawner[{spawnerNetworkId}]: matched network spawn request on '{spawner.name}' from {activatingPlayer}.");
                spawner.SpawnFromNetworkRequest(activatingPlayer);
            }
        }
    }

    private int ComputeStableNetworkId()
    {
        unchecked
        {
            const int offsetBasis = (int)2166136261;
            const int prime = 16777619;
            int hash = offsetBasis;
            string path = GetHierarchyPath(transform);

            for (int i = 0; i < path.Length; i++)
            {
                hash ^= path[i];
                hash *= prime;
            }

            return hash == 0 ? 1 : hash;
        }
    }

    private string GetHierarchyPath(Transform current)
    {
        if (current == null)
        {
            return string.Empty;
        }

        string path = current.name;
        Transform parent = current.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        return path;
    }

    private void OnTriggerEnter(Collider other)
    {
        FusionPlayerAvatar playerAvatar = other.GetComponentInParent<FusionPlayerAvatar>();
        bool isPlayerTag = other.CompareTag("Player");

        Debug.Log(
            $"Spawner[{NetworkId}] '{GetHierarchyPath(transform)}': OnTriggerEnter by '{other.name}', " +
            $"root='{other.transform.root.name}', tag='{other.tag}', isPlayerTag={isPlayerTag}, " +
            $"hasFusionPlayerAvatar={playerAvatar != null}, hasSpawned={hasSpawned}, hasCleared={hasCleared}."
        );

        NetworkRunner networkRunner = FindActiveNetworkRunner();
        if (networkRunner != null && networkRunner.IsRunning)
        {
            if (playerAvatar != null && playerAvatar.IsLocalPlayerAvatar)
            {
                SpawnCharacters();
            }

            return;
        }

        if (isPlayerTag || playerAvatar != null)
        {
            SpawnCharacters();
        }
    }

    public void OnDrawGizmos()
    {
        if (collider == null)
        {
            return;
        }

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position, collider.bounds.size);
    }
}
