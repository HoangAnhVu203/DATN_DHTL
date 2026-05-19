using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    [SerializeField] private int networkId;
    [SerializeField] private Transform spawnPointRoot;
    [SerializeField] private Gate[] gatesToOpen;

    private List<SpawnPoint> spawnPointList;

    private bool hasSpawned;
    private bool hasCleared;
    private float nextSpawnRequestTime;
    private int aliveEnemyCount;

    public new Collider collider;
    public bool IsCleared => hasCleared;
    public event Action<Spawner> Cleared;
    private int NetworkId => networkId != 0 ? networkId : ComputeStableNetworkId();

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

            if (!networkRunner.IsSharedModeMasterClient)
            {
                BroadcastSpawnRequestToNetwork();
                return;
            }

            hasSpawned = true;
            aliveEnemyCount = 0;
            SpawnNetworkCharacters(networkRunner);
            return;
        }

        hasSpawned = true;
        aliveEnemyCount = 0;
        Debug.Log($"Spawner[{NetworkId}] '{name}': no active NetworkRunner, spawning offline enemies.");
        SpawnOfflineCharacters();
    }

    private void SpawnFromNetworkRequest()
    {
        Debug.Log(
            $"Spawner[{NetworkId}] '{GetHierarchyPath(transform)}': received spawn request. " +
            $"hasSpawned={hasSpawned}, hasCleared={hasCleared}."
        );

        if (hasSpawned || hasCleared)
        {
            Debug.Log($"Spawner[{NetworkId}] '{name}': spawn request ignored because hasSpawned={hasSpawned}, hasCleared={hasCleared}.");
            return;
        }

        NetworkRunner networkRunner = FindActiveNetworkRunner();
        if (networkRunner == null || !networkRunner.IsRunning)
        {
            Debug.LogWarning($"Spawner[{NetworkId}] '{name}': spawn request ignored because there is no active NetworkRunner.");
            return;
        }

        if (!networkRunner.IsSharedModeMasterClient)
        {
            Debug.Log($"Spawner[{NetworkId}] '{name}': spawn request received on non-master client, waiting for master.");
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

            Debug.Log(
                $"Spawner[{NetworkId}] '{name}': offline spawn '{point.EnemyToSpawn.name}' " +
                $"at {point.transform.position} from point '{point.name}'."
            );

            GameObject spawnedGameobject = Instantiate(point.EnemyToSpawn, point.transform.position, Quaternion.identity);
            Character spawnedCharacter = spawnedGameobject.GetComponent<Character>();

            if (spawnedCharacter == null)
            {
                Debug.LogWarning($"Spawner[{NetworkId}] '{name}': offline spawned object has no Character component.");
                continue;
            }

            aliveEnemyCount++;
            spawnedCharacter.Died += OnSpawnedCharacterDied;
            spawnedCharacter.PlaySpawnDissolve();
        }

        Debug.Log($"Spawner[{NetworkId}] '{name}': offline spawn finished. aliveEnemyCount={aliveEnemyCount}.");

        if (aliveEnemyCount <= 0)
        {
            ClearSpawner();
        }
    }

    private void SpawnNetworkCharacters(NetworkRunner networkRunner)
    {
        if (!networkRunner.IsSharedModeMasterClient)
        {
            Debug.Log($"Spawner[{NetworkId}] '{name}': not shared-mode master client, waiting for master to spawn enemies.");
            return;
        }

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

            Debug.Log(
                $"Spawner[{NetworkId}] '{name}': network spawning '{point.EnemyToSpawn.name}' " +
                $"at {point.transform.position} from point '{point.name}'."
            );

            NetworkObject spawnedObject = networkRunner.Spawn(
                enemyNetworkObject,
                point.transform.position,
                Quaternion.identity,
                PlayerRef.None,
                null,
                NetworkSpawnFlags.SharedModeStateAuthMasterClient
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
                Debug.LogWarning($"Spawner[{NetworkId}] '{name}': spawned object '{spawnedObject.name}' has no Character component.");
                continue;
            }

            aliveEnemyCount++;
            spawnedCharacter.Died += OnSpawnedCharacterDied;
            Debug.Log($"Spawner[{NetworkId}] '{name}': spawned '{spawnedObject.name}'. aliveEnemyCount={aliveEnemyCount}.");
        }

        Debug.Log($"Spawner[{NetworkId}] '{name}': network spawn finished. aliveEnemyCount={aliveEnemyCount}.");

        if (aliveEnemyCount <= 0)
        {
            ClearSpawner();
        }
    }

    private void SpawnOfflineCharacter(SpawnPoint point)
    {
        Debug.Log(
            $"Spawner[{NetworkId}] '{name}': offline fallback spawn '{point.EnemyToSpawn.name}' " +
            $"at {point.transform.position} from point '{point.name}'."
        );

        GameObject spawnedGameobject = Instantiate(point.EnemyToSpawn, point.transform.position, Quaternion.identity);
        Character spawnedCharacter = spawnedGameobject.GetComponent<Character>();

        if (spawnedCharacter == null)
        {
            Debug.LogWarning($"Spawner[{NetworkId}] '{name}': offline fallback object has no Character component.");
            return;
        }

        aliveEnemyCount++;
        spawnedCharacter.Died += OnSpawnedCharacterDied;
        spawnedCharacter.PlaySpawnDissolve();
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

        FusionPlayerAvatar spawnAuthorityAvatar = FindSpawnAuthorityAvatar();
        if (spawnAuthorityAvatar != null && spawnAuthorityAvatar.RequestSpawnerSpawnOnStateAuthority(NetworkId))
        {
            Debug.Log(
                $"Spawner[{NetworkId}] '{name}': sent spawn request to state authority of " +
                $"'{spawnAuthorityAvatar.name}' PlayerRef={spawnAuthorityAvatar.NetworkPlayerRef}."
            );
            return;
        }

        Debug.LogWarning($"Spawner[{NetworkId}] '{name}': no FusionPlayerAvatar available to request spawn authority.");
    }

    private FusionPlayerAvatar FindSpawnAuthorityAvatar()
    {
        FusionPlayerAvatar[] avatars = FindObjectsByType<FusionPlayerAvatar>(FindObjectsSortMode.None);
        FusionPlayerAvatar bestAvatar = null;
        int bestPlayerId = int.MaxValue;

        foreach (FusionPlayerAvatar avatar in avatars)
        {
            if (avatar == null || !avatar.gameObject.activeInHierarchy)
            {
                continue;
            }

            PlayerRef playerRef = avatar.NetworkPlayerRef;
            int playerId = playerRef.PlayerId > 0 ? playerRef.PlayerId : playerRef.AsIndex;
            if (playerId <= 0)
            {
                continue;
            }

            if (playerId >= bestPlayerId)
            {
                continue;
            }

            bestPlayerId = playerId;
            bestAvatar = avatar;
        }

        return bestAvatar;
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
        Spawner[] spawners = FindObjectsByType<Spawner>(FindObjectsSortMode.None);
        foreach (Spawner spawner in spawners)
        {
            if (spawner != null && spawner.NetworkId == spawnerNetworkId)
            {
                Debug.Log($"Spawner[{spawnerNetworkId}]: matched network spawn request on '{spawner.name}'.");
                spawner.SpawnFromNetworkRequest();
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
