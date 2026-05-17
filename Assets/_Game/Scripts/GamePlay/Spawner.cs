using System.Collections.Generic;
using System;
using Fusion;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    [SerializeField] private Transform spawnPointRoot;
    [SerializeField] private Gate[] gatesToOpen;

    private List<SpawnPoint> spawnPointList;

    private bool hasSpawned;
    private bool hasCleared;
    private int aliveEnemyCount;

    public new Collider collider;
    public bool IsCleared => hasCleared;
    public event Action<Spawner> Cleared;

    private void Awake()
    {
        Transform root = spawnPointRoot != null ? spawnPointRoot : transform.parent;
        if (root == null)
        {
            root = transform;
        }

        var spawnPointArray = root.GetComponentsInChildren<SpawnPoint>();
        spawnPointList = new List<SpawnPoint>(spawnPointArray);
    }

    public void SpawnCharacters()
    {
        if (hasSpawned) { return; }

        hasSpawned = true;
        aliveEnemyCount = 0;

        NetworkRunner networkRunner = FindActiveNetworkRunner();
        if (networkRunner != null && networkRunner.IsRunning)
        {
            SpawnNetworkCharacters(networkRunner);
            return;
        }

        SpawnOfflineCharacters();
    }

    private void SpawnOfflineCharacters()
    {
        foreach(SpawnPoint point in spawnPointList)
        {
            if(point.EnemyToSpawn != null)
            {
                GameObject spawnedGameobject = Instantiate(point.EnemyToSpawn, point.transform.position, Quaternion.identity);
                Character spawnedCharacter = spawnedGameobject.GetComponent<Character>();

                if (spawnedCharacter != null)
                {
                    aliveEnemyCount++;
                    spawnedCharacter.Died += OnSpawnedCharacterDied;
                    spawnedCharacter.PlaySpawnDissolve();
                }
            }
        }

        if (aliveEnemyCount <= 0)
        {
            ClearSpawner();
        }
    }

    private void SpawnNetworkCharacters(NetworkRunner networkRunner)
    {
        if (!networkRunner.IsSharedModeMasterClient)
        {
            Debug.Log("Spawner: waiting for shared-mode master client to spawn enemies.");
            return;
        }

        foreach (SpawnPoint point in spawnPointList)
        {
            if (point.EnemyToSpawn == null)
            {
                continue;
            }

            NetworkObject enemyNetworkObject = point.EnemyToSpawn.GetComponent<NetworkObject>();
            if (enemyNetworkObject == null)
            {
                Debug.LogError($"Spawner: enemy prefab '{point.EnemyToSpawn.name}' is missing NetworkObject. Falling back to local spawn.");
                SpawnOfflineCharacter(point);
                continue;
            }

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
                continue;
            }

            Character spawnedCharacter = spawnedObject.GetComponent<Character>();
            if (spawnedCharacter == null)
            {
                continue;
            }

            aliveEnemyCount++;
            spawnedCharacter.Died += OnSpawnedCharacterDied;
        }

        if (aliveEnemyCount <= 0)
        {
            ClearSpawner();
        }
    }

    private void SpawnOfflineCharacter(SpawnPoint point)
    {
        GameObject spawnedGameobject = Instantiate(point.EnemyToSpawn, point.transform.position, Quaternion.identity);
        Character spawnedCharacter = spawnedGameobject.GetComponent<Character>();

        if (spawnedCharacter == null)
        {
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

        if (aliveEnemyCount <= 0)
        {
            ClearSpawner();
        }
    }

    private void ClearSpawner()
    {
        if (hasCleared)
        {
            return;
        }

        hasCleared = true;
        OpenGate();
        Cleared?.Invoke(this);
    }

    private void OpenGate()
    {
        if (gatesToOpen == null)
        {
            return;
        }

        foreach (Gate gate in gatesToOpen)
        {
            if (gate != null)
            {
                gate.OpenGate();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponentInParent<FusionPlayerAvatar>() != null)
        {
            SpawnCharacters();
        }
    }

    public void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position, collider.bounds.size);
    }
}
