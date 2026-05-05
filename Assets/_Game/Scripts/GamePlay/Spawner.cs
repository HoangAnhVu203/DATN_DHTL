using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    [SerializeField] private Transform spawnPointRoot;
    [SerializeField] private Gate gateToOpen;

    private List<SpawnPoint> spawnPointList;

    private bool hasSpawned;
    private int aliveEnemyCount;

    public new Collider collider;

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
            OpenGate();
        }
    }

    private void OnSpawnedCharacterDied(Character spawnedCharacter)
    {
        spawnedCharacter.Died -= OnSpawnedCharacterDied;
        aliveEnemyCount = Mathf.Max(aliveEnemyCount - 1, 0);

        if (aliveEnemyCount <= 0)
        {
            OpenGate();
        }
    }

    private void OpenGate()
    {
        if (gateToOpen != null)
        {
            gateToOpen.OpenGate();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.tag == "Player")
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
