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

    private void Awake()
    {
        ResolveReferences();
    }

    public override void Spawned()
    {
        ResolveReferences();

        if (Object.HasStateAuthority)
        {
            MirrorLocalHealthToNetwork();
        }

        ApplyNetworkHealthToLocal();
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        MirrorLocalHealthToNetwork();
    }

    public override void Render()
    {
        ApplyNetworkHealthToLocal();
    }

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
