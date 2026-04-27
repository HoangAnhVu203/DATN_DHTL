using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class Enemy : Character
{
    private const string PlayerTag = "Player";

    [SerializeField] private Transform target;
    [SerializeField] private float stoppingDistance = 1.5f;
    [SerializeField] private float detectionRange = 12f;
    [SerializeField] private float repathInterval = 0.2f;

    private NavMeshAgent agent;
    private float nextRepathTime;

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

            return Vector3.zero;
        }

        if (Time.time >= nextRepathTime)
        {
            nextRepathTime = Time.time + repathInterval;
            agent.SetDestination(target.position);
        }

        if (!agent.hasPath || agent.pathPending || agent.remainingDistance <= stoppingDistance)
        {
            return Vector3.zero;
        }

        Vector3 direction = agent.steeringTarget - transform.position;
        direction.y = 0f;

        return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.zero;
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
    }
}
