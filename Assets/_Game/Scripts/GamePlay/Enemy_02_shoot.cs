using Fusion;
using UnityEngine;

public class Enemy_02_shoot : MonoBehaviour
{
    public Transform ShootingPoint;
    public GameObject DamageOrb;
    private Character cc;
    private FusionEnemyAvatar fusionEnemyAvatar;

    private void Awake()
    {
        cc = GetComponent<Character>();
        fusionEnemyAvatar = GetComponent<FusionEnemyAvatar>();
    }

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
            runner.Spawn(
                damageOrbNetworkObject,
                ShootingPoint.position,
                rotation,
                PlayerRef.None,
                null,
                NetworkSpawnFlags.SharedModeStateAuthMasterClient
            );
            return;
        }

        Instantiate(DamageOrb, ShootingPoint.position, rotation);
    }

    private void Update()
    {
        if (CanShootLocally() && cc != null)
        {
            cc.RotateToTarget();
        }
    }

    private bool CanShootLocally()
    {
        return fusionEnemyAvatar == null || fusionEnemyAvatar.HasStateAuthorityLocally;
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
