using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class Enemy : Character
{
    private const string PlayerTag = "Player";
    private const string AttackParameter = "Attack";

    [SerializeField] private Transform target;
    [SerializeField] private float stoppingDistance = 1.5f;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackDuration = 0.65f;
    [SerializeField] private float attackTurnSpeed = 12f;
    [SerializeField] private float repathInterval = 0.2f;
    [SerializeField] private float targetRefreshInterval = 0.25f;
    [SerializeField] private float targetPathSampleDistance = 2f;

    private NavMeshAgent agent;
    private float nextRepathTime;
    private float nextTargetRefreshTime;
    private float attackTimer;
    private Vector3 lastMoveDirection;
    private NavMeshPath reusablePath;
    private float EffectiveAttackRange => Mathf.Max(attackRange, stoppingDistance);

    protected override void Awake()
    {
        base.Awake();

        agent = GetComponent<NavMeshAgent>();
        agent.updatePosition = false;
        agent.updateRotation = false;
        agent.speed = MoveSpeed;
        agent.acceleration = MoveSpeed * 4f;
        agent.angularSpeed = 720f;
        agent.stoppingDistance = stoppingDistance;
        reusablePath = new NavMeshPath();

        RefreshClosestPlayerTarget();
    }

    public override void ApplyRuntimeMoveSpeedMultiplier(float multiplier)
    {
        base.ApplyRuntimeMoveSpeedMultiplier(multiplier);

        if (agent == null)
        {
            return;
        }

        agent.speed = MoveSpeed;
        agent.acceleration = MoveSpeed * 4f;
    }

    protected override Vector3 GetMoveDirection()
    {
        if (!UseExternalMovementTick && (target == null || Time.time >= nextTargetRefreshTime))
        {
            RefreshClosestPlayerTarget();
        }

        if (agent == null || target == null || !agent.isOnNavMesh)
        {
            return Vector3.zero;
        }

        if (IsTargetInAttackRange())
        {
            StopAgentPath();
            lastMoveDirection = Vector3.zero;
            return Vector3.zero;
        }

        if (Time.time >= nextRepathTime)
        {
            nextRepathTime = Time.time + repathInterval;
            if (!agent.SetDestination(target.position))
            {
                StopAgentPath();
                return Vector3.zero;
            }
        }

        if (agent.pathPending)
        {
            return lastMoveDirection;
        }

        if (agent.pathStatus != NavMeshPathStatus.PathComplete)
        {
            StopAgentPath();
            return Vector3.zero;
        }

        if (!agent.hasPath || agent.remainingDistance <= stoppingDistance)
        {
            lastMoveDirection = Vector3.zero;
            return Vector3.zero;
        }

        Vector3 direction = agent.steeringTarget - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
        {
            return lastMoveDirection;
        }

        lastMoveDirection = direction.normalized;
        return lastMoveDirection;
    }

    protected override void AfterMove()
    {
        if (agent != null && agent.isOnNavMesh)
        {
            agent.nextPosition = transform.position;
        }
    }

    public void SetTarget(Transform newTarget)
    {
        if (target == newTarget)
        {
            return;
        }

        target = newTarget;

        if (agent != null && agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.nextPosition = transform.position;
        }

        lastMoveDirection = Vector3.zero;
        nextRepathTime = 0f;
    }

    public void SyncAgentToTransform()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }

        agent.Warp(transform.position);
        agent.ResetPath();
        agent.nextPosition = transform.position;
        lastMoveDirection = Vector3.zero;
        nextRepathTime = 0f;
    }

    public void RefreshClosestPlayerTarget()
    {
        nextTargetRefreshTime = Time.time + Mathf.Max(0.05f, targetRefreshInterval);

        Player[] players = FindObjectsByType<Player>(FindObjectsSortMode.None);
        Transform closestTarget = null;
        float closestSqrDistance = float.MaxValue;

        foreach (Player player in players)
        {
            if (player == null || !player.gameObject.activeInHierarchy)
            {
                continue;
            }

            Health playerHealth = player.GetComponent<Health>();
            if (playerHealth != null && playerHealth.IsDead)
            {
                continue;
            }

            if (!CanReachTarget(player.transform))
            {
                continue;
            }

            float sqrDistance = (player.transform.position - transform.position).sqrMagnitude;
            if (sqrDistance >= closestSqrDistance)
            {
                continue;
            }

            closestSqrDistance = sqrDistance;
            closestTarget = player.transform;
        }

        if (closestTarget == null && (agent == null || !agent.isOnNavMesh))
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag(PlayerTag);
            closestTarget = playerObject != null ? playerObject.transform : null;
        }

        SetTarget(closestTarget);
    }

    private bool CanReachTarget(Transform candidateTarget)
    {
        if (candidateTarget == null || agent == null || !agent.isOnNavMesh)
        {
            return true;
        }

        Vector3 targetPosition = candidateTarget.position;
        float sampleDistance = Mathf.Max(0.1f, targetPathSampleDistance);
        if (NavMesh.SamplePosition(targetPosition, out NavMeshHit targetHit, sampleDistance, NavMesh.AllAreas))
        {
            targetPosition = targetHit.position;
        }

        reusablePath ??= new NavMeshPath();
        if (!agent.CalculatePath(targetPosition, reusablePath))
        {
            return false;
        }

        return reusablePath.status == NavMeshPathStatus.PathComplete;
    }

    public void AttackAnimationEnds()
    {
        if (CurrentState == CharacterState.Attack)
        {
            SwitchToState(CharacterState.Idle);
        }
    }

    protected override void OnUpdateIdle(float deltaTime)
    {
        TryEnterAttackState();
    }

    protected override void OnUpdateRun(float deltaTime)
    {
        TryEnterAttackState();
    }

    protected override void OnEnterDead()
    {
        base.OnEnterDead();
        StopAgentPath();
        DisableColliders();

        if (agent != null)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;

            if (agent.isOnNavMesh)
            {
                agent.nextPosition = transform.position;
            }
        }

        target = null;
        lastMoveDirection = Vector3.zero;
    }

    protected override void OnEnterHurt()
    {
        base.OnEnterHurt();
        StopAgentPath();

        if (agent != null)
        {
            agent.velocity = Vector3.zero;

            if (agent.isOnNavMesh)
            {
                agent.nextPosition = transform.position;
            }
        }
    }

    protected override void OnEnterAttack()
    {
        attackTimer = Mathf.Max(attackDuration, 0f);
        StopAgentPath();
        FaceTarget(1f);
        SetAnimatorTrigger(AttackParameter);
    }

    protected override void OnUpdateAttack(float deltaTime)
    {
        if (HasAnimator())
        {
            return;
        }

        attackTimer -= deltaTime;

        if (attackTimer <= 0f)
        {
            SwitchToState(CharacterState.Idle);
        }
    }

    private void TryEnterAttackState()
    {
        if (IsTargetInAttackRange())
        {
            SwitchToState(CharacterState.Attack);
        }
    }

    private bool IsTargetInAttackRange()
    {
        if (target == null)
        {
            return false;
        }

        Vector3 offset = target.position - transform.position;
        offset.y = 0f;

        float effectiveAttackRange = EffectiveAttackRange;
        return offset.sqrMagnitude <= effectiveAttackRange * effectiveAttackRange;
    }

    private void FaceTarget(float deltaTime)
    {
        if (target == null)
        {
            return;
        }

        Vector3 direction = target.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, attackTurnSpeed * deltaTime);
    }

    private void StopAgentPath()
    {
        if (agent != null && agent.isOnNavMesh && agent.hasPath)
        {
            agent.ResetPath();
        }

        lastMoveDirection = Vector3.zero;
    }

    private void DisableColliders()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>();

        foreach (Collider enemyCollider in colliders)
        {
            enemyCollider.enabled = false;
        }
    }
}
