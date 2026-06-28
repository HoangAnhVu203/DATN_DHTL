using Fusion;
using UnityEngine;

public class PickUp : NetworkBehaviour
{
    [SerializeField] private int networkId;
    public PickUpType type;
    public int value = 20;
    public ParticleSystem collectedVFX;

    private bool collected;
    private int NetworkId => networkId != 0 ? networkId : ComputeStableNetworkId();

    // Handles the first contact with another collider.
    private void OnTriggerEnter(Collider other)
    {
        if (collected)
        {
            return;
        }

        FusionPlayerAvatar networkPlayer = other.GetComponentInParent<FusionPlayerAvatar>();
        if (networkPlayer != null)
        {
            CollectNetworkPlayer(networkPlayer);
            return;
        }

        if (!other.CompareTag("Player"))
        {
            return;
        }

        Character character = other.GetComponentInParent<Character>();
        if (character == null)
        {
            return;
        }

        collected = true;
        character.ApplyPickupValue(type, value);
        PlayCollectedVFX(transform.position);
        Destroy(gameObject);
    }

    // Collects the network player.
    private void CollectNetworkPlayer(FusionPlayerAvatar networkPlayer)
    {
        if (Object != null && Object.IsValid && Runner != null && Runner.IsRunning)
        {
            RPC_RequestCollect(networkPlayer.NetworkPlayerRef);
            return;
        }

        collected = true;
        networkPlayer.RequestPickup(type, value);
        networkPlayer.BroadcastPickupCollected(NetworkId, transform.position);
        PlayCollectedVFX(transform.position);
        Destroy(gameObject);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    // Runs the request collect RPC.
    private void RPC_RequestCollect(PlayerRef collector)
    {
        if (collected)
        {
            return;
        }

        collected = true;

        if (Runner != null && Runner.TryGetPlayerObject(collector, out NetworkObject playerObject) && playerObject != null)
        {
            FusionPlayerAvatar playerAvatar = playerObject.GetComponent<FusionPlayerAvatar>();
            if (playerAvatar != null)
            {
                playerAvatar.RequestPickup(type, value);
            }
        }

        RPC_PlayCollectedVFX(transform.position);

        if (Runner != null && Object != null && Object.IsValid)
        {
            Runner.Despawn(Object);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    // Runs the play collected vfx RPC.
    private void RPC_PlayCollectedVFX(Vector3 position)
    {
        PlayCollectedVFX(position);
    }

    // Plays the collected vfx.
    private void PlayCollectedVFX(Vector3 position)
    {
        if (collectedVFX != null)
        {
            Instantiate(collectedVFX, position, Quaternion.identity);
        }
    }

    // Collects the local pickup for network id.
    public static void CollectLocalPickupForNetworkId(int pickupNetworkId, Vector3 collectPosition)
    {
        PickUp[] pickups = FindObjectsByType<PickUp>(FindObjectsSortMode.None);
        foreach (PickUp pickup in pickups)
        {
            if (pickup == null || pickup.NetworkId != pickupNetworkId || pickup.collected)
            {
                continue;
            }

            pickup.collected = true;
            pickup.PlayCollectedVFX(collectPosition);
            Destroy(pickup.gameObject);
        }
    }

    // Computes the stable network id.
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

    // Returns the hierarchy path.
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
}
