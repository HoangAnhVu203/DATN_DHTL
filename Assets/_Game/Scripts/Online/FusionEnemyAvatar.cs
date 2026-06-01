using Fusion;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class FusionEnemyAvatar : NetworkBehaviour
{
    private static readonly int SpeedParameter = Animator.StringToHash("Speed");
    private static readonly int IsGroundedParameter = Animator.StringToHash("IsGrounded");

    [SerializeField] private Enemy enemy;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Animator animator;
    [SerializeField] private NavMeshAgent navMeshAgent;
    [SerializeField] private DamageCaster damageCaster;
    [SerializeField] private Enemy_02_shoot rangedAttack;
    [SerializeField] private Health health;
    [SerializeField] private FusionNetworkHealth networkHealth;
    [SerializeField] private float targetRefreshInterval = 0.25f;
    [SerializeField] private float proxyAnimationMoveSpeed = 2.5f;
    [SerializeField] private float proxyAnimatorDampTime = 0.08f;
    [SerializeField] private float proxyStopAnimatorDampTime = 0.02f;
    [SerializeField] private float targetPathSampleDistance = 2f;
    [SerializeField] private bool playSpawnDissolveOnAuthority = true;

    private bool? lastStateAuthority;
    private double nextTargetRefreshTime;
    private bool spawnDissolvePlayed;
    private bool hasAppliedNetworkDeath;
    private bool hasLastRenderPosition;
    private bool agentSyncedToSpawn;
    private Vector3 lastRenderPosition;
    private NavMeshPath reusablePath;
    private int lastDamageSourceId;
    private double lastDamageTime = -999d;
    private float lastAppliedDifficultyMultiplier = -1f;

    private const double DuplicateDamageLockSeconds = 0.3d;

    [Networked] public int WaveIndex { get; private set; }
    [Networked] public float DifficultyMultiplier { get; private set; }

    public bool CanReceiveDamageLocally => Object != null && Object.IsValid && Object.HasStateAuthority;
    public bool HasStateAuthorityLocally => Object != null && Object.IsValid && Object.HasStateAuthority;

    private void Awake()
    {
        ResolveReferences();
    }

    public override void Spawned()
    {
        ResolveReferences();
        SubscribeHealth();
        ResetProxyAnimationTracking();
        reusablePath ??= new NavMeshPath();
        ApplyAuthorityState();
        ApplyNetworkDifficultyToLocal();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeHealth();

        if (Object != null && Object.IsValid)
        {
            ApplyAuthorityState();
        }
    }

    private void OnDisable()
    {
        UnsubscribeHealth();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        UnsubscribeHealth();
    }

    private void Update()
    {
        if (Object == null || !Object.IsValid)
        {
            return;
        }

        ApplyAuthorityState();
        ApplyNetworkDifficultyToLocal();
    }

    public override void FixedUpdateNetwork()
    {
        if (Object == null || !Object.IsValid || !Object.HasStateAuthority || enemy == null)
        {
            return;
        }

        SyncAgentToSpawnPosition();
        RefreshClosestTarget();
        enemy.TickMovement(Runner.DeltaTime);
    }

    public override void Render()
    {
        if (Object == null || !Object.IsValid || Object.HasStateAuthority || animator == null)
        {
            ResetProxyAnimationTracking();
            return;
        }

        float deltaTime = Time.deltaTime;
        if (!hasLastRenderPosition || deltaTime <= 0f)
        {
            ResetProxyAnimationTracking();
            return;
        }

        Vector3 movement = transform.position - lastRenderPosition;
        movement.y = 0f;
        lastRenderPosition = transform.position;

        float moveSpeed = Mathf.Max(0.01f, proxyAnimationMoveSpeed);
        float normalizedSpeed = Mathf.Clamp01(movement.magnitude / (deltaTime * moveSpeed));
        float dampTime = normalizedSpeed > 0.001f ? proxyAnimatorDampTime : proxyStopAnimatorDampTime;

        animator.SetFloat(SpeedParameter, normalizedSpeed, dampTime, deltaTime);
        animator.SetBool(IsGroundedParameter, true);
    }

    public bool RequestDamage(int damage, Vector3 attackPosition, int damageSourceId = 0, PlayerRef attacker = default)
    {
        if (damage <= 0 || Object == null || !Object.IsValid)
        {
            return false;
        }

        RPC_ApplyDamage(damage, attackPosition, damageSourceId, attacker);
        return true;
    }

    public void ApplyDifficulty(int waveIndex, float multiplier)
    {
        if (Object == null || !Object.IsValid || !Object.HasStateAuthority)
        {
            return;
        }

        WaveIndex = Mathf.Max(0, waveIndex);
        DifficultyMultiplier = Mathf.Max(1f, multiplier);
        ApplyNetworkDifficultyToLocal();
        networkHealth?.ForceSyncNow();

        Debug.Log(
            $"FusionEnemyAvatar: applied difficulty. wave={WaveIndex}, " +
            $"multiplier={DifficultyMultiplier:0.00}, enemy='{name}'."
        );
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ApplyDamage(int damage, Vector3 attackPosition, int damageSourceId, PlayerRef attacker)
    {
        if (enemy == null || !Object.HasStateAuthority)
        {
            return;
        }

        double now = Runner != null ? Runner.SimulationTime : Time.timeAsDouble;
        if (damageSourceId != 0
            && damageSourceId == lastDamageSourceId
            && now - lastDamageTime < DuplicateDamageLockSeconds)
        {
            Debug.Log(
                $"FusionEnemyAvatar: ignored duplicate damage from source {damageSourceId} " +
                $"on '{name}' within {DuplicateDamageLockSeconds:0.00}s."
            );
            return;
        }

        lastDamageSourceId = damageSourceId;
        lastDamageTime = now;

        int hpBefore = health != null ? health.currentHealth : 0;
        enemy.ApplyDamage(damage, attackPosition);
        int hpAfter = health != null ? health.currentHealth : 0;
        int actualDamage = Mathf.Max(0, hpBefore - hpAfter);

        string attackerUserId = FusionPlayerAvatar.GetUserIdForPlayerRef(Runner, attacker);
        if (!string.IsNullOrWhiteSpace(attackerUserId) && actualDamage > 0)
        {
            FusionPlayerAvatar.BroadcastMatchStatEvent(OnlineMatchStats.StatEventType.Damage, attackerUserId, actualDamage);

            if (hpBefore > 0 && hpAfter <= 0)
            {
                FusionPlayerAvatar.BroadcastMatchStatEvent(OnlineMatchStats.StatEventType.Kill, attackerUserId);
            }
        }

        networkHealth?.ForceSyncNow();
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
        ResetProxyAnimationTracking();
        gameObject.name = hasStateAuthority ? "NetworkEnemy_StateAuthority" : "NetworkEnemy_Proxy";
        gameObject.tag = hasStateAuthority ? "Enemy" : "Untagged";

        if (enemy != null)
        {
            enemy.enabled = hasStateAuthority;
            enemy.UseExternalMovementTick = hasStateAuthority;
        }

        if (characterController != null)
        {
            characterController.enabled = true;
        }

        if (navMeshAgent != null)
        {
            navMeshAgent.enabled = hasStateAuthority;

            if (hasStateAuthority)
            {
                agentSyncedToSpawn = false;
                SyncAgentToSpawnPosition();
                ForceRefreshClosestTarget();
            }
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

    private void RefreshClosestTarget()
    {
        if (Runner == null || Runner.SimulationTime < nextTargetRefreshTime)
        {
            return;
        }

        nextTargetRefreshTime = Runner.SimulationTime + Mathf.Max(0.05f, targetRefreshInterval);
        ForceRefreshClosestTarget();
    }

    private void ForceRefreshClosestTarget()
    {
        if (enemy == null)
        {
            return;
        }

        enemy.SetTarget(FindClosestPlayer());
    }

    private void SyncAgentToSpawnPosition()
    {
        if (agentSyncedToSpawn || navMeshAgent == null || !navMeshAgent.enabled)
        {
            return;
        }

        if (!navMeshAgent.isOnNavMesh)
        {
            Debug.LogWarning(
                $"FusionEnemyAvatar: '{name}' spawned at {transform.position} but NavMeshAgent is not on NavMesh. " +
                "Move this SpawnPoint onto the baked NavMesh."
            );
            return;
        }

        navMeshAgent.Warp(transform.position);
        navMeshAgent.ResetPath();
        navMeshAgent.nextPosition = transform.position;
        enemy?.SyncAgentToTransform();
        agentSyncedToSpawn = true;

        Debug.Log($"FusionEnemyAvatar: synced '{name}' NavMeshAgent to spawn position {transform.position}.");
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

        if (animator == null)
        {
            animator = GetComponent<Animator>();
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

        if (networkHealth == null)
        {
            networkHealth = GetComponent<FusionNetworkHealth>();
        }

        if (health == null)
        {
            health = GetComponent<Health>();
        }
    }

    private void ApplyNetworkDifficultyToLocal()
    {
        if (Object != null && Object.IsValid && !Object.HasStateAuthority)
        {
            return;
        }

        float multiplier = Mathf.Max(1f, DifficultyMultiplier);
        if (Mathf.Approximately(lastAppliedDifficultyMultiplier, multiplier))
        {
            return;
        }

        lastAppliedDifficultyMultiplier = multiplier;

        if (health != null)
        {
            health.ApplyMaxHealthMultiplier(multiplier, true);
        }

        if (damageCaster != null)
        {
            damageCaster.ApplyDamageMultiplier(multiplier);
        }

        if (rangedAttack != null)
        {
            rangedAttack.ApplyDamageMultiplier(multiplier);
        }

        if (enemy != null)
        {
            float speedMultiplier = Mathf.Lerp(1f, multiplier, 0.35f);
            enemy.ApplyRuntimeMoveSpeedMultiplier(speedMultiplier);
        }
    }

    private void SubscribeHealth()
    {
        if (health == null)
        {
            return;
        }

        health.HealthChanged -= OnHealthChanged;
        health.HealthChanged += OnHealthChanged;
    }

    private void UnsubscribeHealth()
    {
        if (health != null)
        {
            health.HealthChanged -= OnHealthChanged;
        }
    }

    private void OnHealthChanged(int current, int max)
    {
        if (current > 0 || hasAppliedNetworkDeath || enemy == null)
        {
            return;
        }

        hasAppliedNetworkDeath = true;

        if (enemy.CurrentState != CharacterState.Dead)
        {
            enemy.SwitchToState(CharacterState.Dead, true);
        }
    }

    private void ResetProxyAnimationTracking()
    {
        hasLastRenderPosition = true;
        lastRenderPosition = transform.position;
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

            Health playerHealth = playerAvatar.GetComponent<Health>();
            if (playerHealth != null && playerHealth.IsDead)
            {
                continue;
            }

            if (!CanReachPlayer(playerAvatar.transform))
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

    private bool CanReachPlayer(Transform playerTransform)
    {
        if (playerTransform == null || navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
        {
            return true;
        }

        Vector3 targetPosition = playerTransform.position;
        float sampleDistance = Mathf.Max(0.1f, targetPathSampleDistance);
        if (NavMesh.SamplePosition(targetPosition, out NavMeshHit targetHit, sampleDistance, NavMesh.AllAreas))
        {
            targetPosition = targetHit.position;
        }

        reusablePath ??= new NavMeshPath();
        if (!navMeshAgent.CalculatePath(targetPosition, reusablePath))
        {
            return false;
        }

        return reusablePath.status == NavMeshPathStatus.PathComplete;
    }
}
