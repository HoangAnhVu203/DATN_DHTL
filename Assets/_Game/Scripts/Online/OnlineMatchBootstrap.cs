using System.Collections.Generic;
using UnityEngine;

public class OnlineMatchBootstrap : MonoBehaviour
{
    [SerializeField] private Player playerTemplate;
    [SerializeField] private Transform spawnPointRoot;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private bool spawnOfflineSinglePlayer = true;

    // Runs the first scene-time setup for this object.
    private void Start()
    {
        ResolveReferences();
        SpawnPlayers();
    }

    private void ResolveReferences()
    {
        if (playerTemplate == null)
        {
            playerTemplate = FindFirstObjectByType<Player>();
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
    }

    // Spawns the players.
    private void SpawnPlayers()
    {
        if (playerTemplate == null)
        {
            Debug.LogError("OnlineMatchBootstrap could not find a Player in GameScene.");
            return;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("OnlineMatchBootstrap could not find SpawnPoint_0..SpawnPoint_3 under PlayerSpawnPoints.");
            return;
        }

        List<RoomService.RoomPlayerData> players = OnlineRoomSession.Players;
        if (players == null || players.Count == 0)
        {
            if (spawnOfflineSinglePlayer)
            {
                PlacePlayer(playerTemplate, spawnPoints[0], true, SupabaseSession.DisplayName);
            }

            return;
        }

        int localPlayerIndex = players.FindIndex(player => player.user_id == SupabaseSession.UserId);
        if (localPlayerIndex < 0)
        {
            localPlayerIndex = 0;
        }

        int spawnCount = Mathf.Min(players.Count, spawnPoints.Length);
        for (int i = 0; i < spawnCount; i++)
        {
            RoomService.RoomPlayerData roomPlayer = players[i];
            bool isLocalPlayer = i == localPlayerIndex;
            Player playerInstance = isLocalPlayer
                ? playerTemplate
                : Instantiate(playerTemplate, spawnPoints[i].position, spawnPoints[i].rotation);

            string displayName = !string.IsNullOrWhiteSpace(roomPlayer.display_name)
                ? roomPlayer.display_name
                : roomPlayer.user_id;

            PlacePlayer(playerInstance, spawnPoints[i], isLocalPlayer, displayName);
        }
    }

    // Places the local player at a match spawn point.
    private void PlacePlayer(Player player, Transform spawnPoint, bool isLocalPlayer, string displayName)
    {
        if (player == null || spawnPoint == null)
        {
            return;
        }

        player.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
        player.gameObject.name = isLocalPlayer ? $"Player_Local_{displayName}" : $"Player_Remote_{displayName}";
        player.gameObject.tag = isLocalPlayer ? "Player" : "Untagged";
        player.enabled = isLocalPlayer;

        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller != null)
        {
            controller.enabled = isLocalPlayer;
        }
    }

    // Returns the spawn index.
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
