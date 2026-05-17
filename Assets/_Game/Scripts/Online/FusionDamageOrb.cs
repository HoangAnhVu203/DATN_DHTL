using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
public class FusionDamageOrb : NetworkBehaviour
{
    [SerializeField] private DamageOrb damageOrb;
    [SerializeField] private Rigidbody orbRigidbody;

    public bool CanSimulateLocally => Object == null || !Object.IsValid || Object.HasStateAuthority;
    public bool IsNetworkSpawned => Object != null && Object.IsValid && Runner != null;

    private void Awake()
    {
        ResolveReferences();
    }

    public override void Spawned()
    {
        ResolveReferences();

        if (orbRigidbody != null && !Object.HasStateAuthority)
        {
            orbRigidbody.isKinematic = true;
        }
    }

    public void DestroyNetworkOrb(Vector3 hitPosition)
    {
        if (!IsNetworkSpawned || !Object.HasStateAuthority)
        {
            return;
        }

        RPC_PlayHitVFX(hitPosition);
        Runner.Despawn(Object);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayHitVFX(Vector3 hitPosition)
    {
        if (damageOrb != null)
        {
            damageOrb.PlayHitVFX(hitPosition);
        }
    }

    private void ResolveReferences()
    {
        if (damageOrb == null)
        {
            damageOrb = GetComponent<DamageOrb>();
        }

        if (orbRigidbody == null)
        {
            orbRigidbody = GetComponent<Rigidbody>();
        }
    }
}
