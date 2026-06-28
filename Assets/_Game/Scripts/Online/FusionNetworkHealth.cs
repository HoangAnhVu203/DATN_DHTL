using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
public class FusionNetworkHealth : NetworkBehaviour
{
    [SerializeField] private Health health;

    [Networked] public int CurrentHealth { get; private set; }
    [Networked] public int MaxHealth { get; private set; }

    private int lastAppliedCurrent = -1;
    private int lastAppliedMax = -1;

    // Sets up this component before gameplay starts.
    private void Awake()
    {
        ResolveReferences();
    }

    // Initializes this object after Fusion spawns it.
    public override void Spawned()
    {
        ResolveReferences();

        if (Object.HasStateAuthority)
        {
            MirrorLocalHealthToNetwork();
        }

        ApplyNetworkHealthToLocal();
    }

    // Runs this object on Fusion network ticks.
    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        MirrorLocalHealthToNetwork();
    }

    // Copies network health changes into the local Health component.
    public override void Render()
    {
        ApplyNetworkHealthToLocal();
    }

    // Forces the sync now.
    public void ForceSyncNow()
    {
        if (Object != null && Object.IsValid && Object.HasStateAuthority)
        {
            MirrorLocalHealthToNetwork();
        }
    }

    private void ResolveReferences()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }
    }

    // Pushes local health values into Fusion state authority.
    private void MirrorLocalHealthToNetwork()
    {
        if (health == null)
        {
            return;
        }

        int max = Mathf.Max(1, health.maxHealth);
        int current = Mathf.Clamp(health.currentHealth, 0, max);

        if (MaxHealth != max)
        {
            MaxHealth = max;
        }

        if (CurrentHealth != current)
        {
            CurrentHealth = current;
        }
    }

    // Applies the network health to local.
    private void ApplyNetworkHealthToLocal()
    {
        if (health == null || MaxHealth <= 0)
        {
            return;
        }

        if (lastAppliedCurrent == CurrentHealth && lastAppliedMax == MaxHealth)
        {
            return;
        }

        lastAppliedCurrent = CurrentHealth;
        lastAppliedMax = MaxHealth;
        health.SetHealthFromNetwork(CurrentHealth, MaxHealth);
    }
}
