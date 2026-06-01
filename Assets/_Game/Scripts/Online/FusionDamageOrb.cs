using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
public class FusionDamageOrb : NetworkBehaviour
{
    [SerializeField] private DamageOrb damageOrb;
    [SerializeField] private Rigidbody orbRigidbody;

    [Networked] public int NetworkDamage { get; private set; }

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

        ApplyNetworkDamageToLocal();
    }

    public override void Render()
    {
        ApplyNetworkDamageToLocal();
    }

    public void SetNetworkDamage(int damage)
    {
        if (Object == null || !Object.IsValid || !Object.HasStateAuthority)
        {
            return;
        }

        NetworkDamage = Mathf.Max(1, damage);
        ApplyNetworkDamageToLocal();
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

    private void ApplyNetworkDamageToLocal()
    {
        if (damageOrb != null && NetworkDamage > 0)
        {
            damageOrb.SetDamageFromNetwork(NetworkDamage);
        }
    }
}
