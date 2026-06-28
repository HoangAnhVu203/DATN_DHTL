using Fusion;
using UnityEngine;

public class Enemy_02_shoot : MonoBehaviour
{
    public Transform ShootingPoint;
    public GameObject DamageOrb;
    private Character cc;
    private FusionEnemyAvatar fusionEnemyAvatar;
    private float damageMultiplier = 1f;

    // Sets up this component before gameplay starts.
    private void Awake()
    {
        cc = GetComponent<Character>();
        fusionEnemyAvatar = GetComponent<FusionEnemyAvatar>();
    }

    // Fires a damage orb from the enemy ranged attack.
    public void ShootTheDamageOrb()
    {
        if (DamageOrb == null || ShootingPoint == null || !CanShootLocally())
        {
            return;
        }

        Quaternion rotation = Quaternion.LookRotation(ShootingPoint.forward);
        NetworkRunner runner = FindActiveNetworkRunner();
        NetworkObject damageOrbNetworkObject = DamageOrb.GetComponent<NetworkObject>();

        if (runner != null && runner.IsRunning && damageOrbNetworkObject != null)
        {
            NetworkObject spawnedOrb = runner.Spawn(
                damageOrbNetworkObject,
                ShootingPoint.position,
                rotation,
                PlayerRef.None,
                null,
                NetworkSpawnFlags.SharedModeStateAuthMasterClient
            );

            ApplyDamageToSpawnedOrb(spawnedOrb != null ? spawnedOrb.gameObject : null);
            return;
        }

        GameObject spawnedLocalOrb = Instantiate(DamageOrb, ShootingPoint.position, rotation);
        ApplyDamageToSpawnedOrb(spawnedLocalOrb);
    }

    // Applies the damage multiplier.
    public void ApplyDamageMultiplier(float multiplier)
    {
        damageMultiplier = Mathf.Max(0.1f, multiplier);
    }

    // Runs the per-frame work for this behaviour.
    private void Update()
    {
        if (CanShootLocally() && cc != null)
        {
            cc.RotateToTarget();
        }
    }

    // Checks whether this enemy should shoot on this client.
    private bool CanShootLocally()
    {
        return fusionEnemyAvatar == null || fusionEnemyAvatar.HasStateAuthorityLocally;
    }

    // Applies the damage to spawned orb.
    private void ApplyDamageToSpawnedOrb(GameObject spawnedOrb)
    {
        if (spawnedOrb == null)
        {
            return;
        }

        DamageOrb damageOrb = spawnedOrb.GetComponent<DamageOrb>();
        if (damageOrb != null)
        {
            damageOrb.ApplyDamageMultiplier(damageMultiplier);
        }

        FusionDamageOrb fusionDamageOrb = spawnedOrb.GetComponent<FusionDamageOrb>();
        if (fusionDamageOrb != null && damageOrb != null)
        {
            fusionDamageOrb.SetNetworkDamage(damageOrb.damage);
        }
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
}
