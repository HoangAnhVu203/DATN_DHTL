using Fusion;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class FusionEnemyAvatar : NetworkBehaviour
{
    [SerializeField] private Enemy enemy;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private NavMeshAgent navMeshAgent;
    [SerializeField] private DamageCaster damageCaster;
    [SerializeField] private Enemy_02_shoot rangedAttack;
    [SerializeField] private float targetRefreshInterval = 0.25f;
    [SerializeField] private bool playSpawnDissolveOnAuthority = true;

    private bool? lastStateAuthority;
    private float nextTargetRefreshTime;
    private bool spawnDissolvePlayed;

    public bool CanReceiveDamageLocally => Object != null && Object.IsValid && Object.HasStateAuthority;
    public bool HasStateAuthorityLocally => Object != null && Object.IsValid && Object.HasStateAuthority;

    private void Awake()
    {
        ResolveReferences();
    }

    public override void Spawned()
    {
        ApplyAuthorityState();
    }

    private void OnEnable()
    {
        if (Object != null && Object.IsValid)
        {
            ApplyAuthorityState();
        }
    }

    private void Update()
    {
        if (Object == null || !Object.IsValid)
        {
            return;
        }

        ApplyAuthorityState();

        if (!Object.HasStateAuthority || enemy == null || Time.time < nextTargetRefreshTime)
        {
            return;
        }

        nextTargetRefreshTime = Time.time + targetRefreshInterval;
        enemy.SetTarget(FindClosestPlayer());
    }

    public bool RequestDamage(int damage, Vector3 attackPosition)
    {
        if (damage <= 0 || Object == null || !Object.IsValid)
        {
            return false;
        }

        RPC_ApplyDamage(damage, attackPosition);
        return true;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ApplyDamage(int damage, Vector3 attackPosition)
    {
        if (enemy == null || !Object.HasStateAuthority)
        {
            return;
        }

        enemy.ApplyDamage(damage, attackPosition);
    }

    private void ApplyAuthorityState()
    {
        ResolveReferences();

        bool hasStateAuthority = Object != null && Object.IsValid && Object.HasStateAuthority;
        if (lastStateAuthority.HasValue && lastStateAuthority.Value == hasStateAuthority)
        {
            return;
        }

        lastStateAuthority = hasStateAuthority;
        gameObject.name = hasStateAuthority ? "NetworkEnemy_StateAuthority" : "NetworkEnemy_Proxy";
        gameObject.tag = hasStateAuthority ? "Enemy" : "Untagged";

        if (enemy != null)
        {
            enemy.enabled = hasStateAuthority;
        }

        if (characterController != null)
        {
            characterController.enabled = true;
        }

        if (navMeshAgent != null)
        {
            navMeshAgent.enabled = hasStateAuthority;
        }

        if (damageCaster != null)
        {
            damageCaster.DisableDamageCaster();
            damageCaster.enabled = hasStateAuthority;
        }

        if (rangedAttack != null)
        {
            rangedAttack.enabled = hasStateAuthority;
        }

        if (hasStateAuthority && playSpawnDissolveOnAuthority && !spawnDissolvePlayed && enemy != null)
        {
            spawnDissolvePlayed = true;
            enemy.PlaySpawnDissolve();
        }
    }

    private void ResolveReferences()
    {
        if (enemy == null)
        {
            enemy = GetComponent<Enemy>();
        }

        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }

        if (navMeshAgent == null)
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
        }

        if (damageCaster == null)
        {
            damageCaster = GetComponentInChildren<DamageCaster>(true);
        }

        if (rangedAttack == null)
        {
            rangedAttack = GetComponent<Enemy_02_shoot>();
        }
    }

    private Transform FindClosestPlayer()
    {
        FusionPlayerAvatar[] players = FindObjectsByType<FusionPlayerAvatar>(FindObjectsSortMode.None);
        Transform closest = null;
        float closestSqrDistance = float.MaxValue;

        foreach (FusionPlayerAvatar playerAvatar in players)
        {
            if (playerAvatar == null || !playerAvatar.gameObject.activeInHierarchy)
            {
                continue;
            }

            float sqrDistance = (playerAvatar.transform.position - transform.position).sqrMagnitude;
            if (sqrDistance >= closestSqrDistance)
            {
                continue;
            }

            closestSqrDistance = sqrDistance;
            closest = playerAvatar.transform;
        }

        return closest;
    }
}
