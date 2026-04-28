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
    [SerializeField] private float detectionRange = 12f;
    [SerializeField] private float repathInterval = 0.2f;

    private NavMeshAgent agent;
    private float nextRepathTime;
    private float attackTimer;
    private Vector3 lastMoveDirection;
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

        if (target == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag(PlayerTag);
            if (playerObject != null)
            {
                target = playerObject.transform;
            }
        }
    }

    protected override Vector3 GetMoveDirection()
    {
        if (agent == null || target == null || !agent.isOnNavMesh)
        {
            return Vector3.zero;
        }

        float sqrDistanceToTarget = (target.position - transform.position).sqrMagnitude;
        if (sqrDistanceToTarget > detectionRange * detectionRange)
        {
            if (agent.hasPath)
            {
                agent.ResetPath();
            }

            lastMoveDirection = Vector3.zero;
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
            agent.SetDestination(target.position);
        }

        if (agent.pathPending)
        {
            return lastMoveDirection;
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
        target = newTarget;

        if (agent != null && agent.isOnNavMesh)
        {
            agent.ResetPath();
        }

        lastMoveDirection = Vector3.zero;
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
}
