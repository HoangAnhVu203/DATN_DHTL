using UnityEngine;

public class Player : Character
{
    private const string AttackParameter = "Attack";

    [SerializeField] private float attackDuration = 0.65f;

    private PlayerVFXManager vfxManager;
    private float attackTimer;

    protected override void Awake()
    {
        base.Awake();
        vfxManager = GetComponent<PlayerVFXManager>();
    }

    protected override Vector3 GetMoveDirection()
    {
        Vector3 direction = JoyStickController.direct;
        direction.y = 0f;

        return direction;
    }

    public void Attack()
    {
        if (CurrentState == CharacterState.Attack)
        {
            BeginAttack();
            return;
        }

        SwitchToState(CharacterState.Attack);
    }

    public void AttackAnimationEnds()
    {
        if (CurrentState == CharacterState.Attack)
        {
            SwitchToState(CharacterState.Idle);
        }
    }

    protected override void OnEnterAttack()
    {
        BeginAttack();
    }

    protected override void OnUpdateAttack(float deltaTime)
    {
        attackTimer -= deltaTime;

        if (attackTimer <= 0f)
        {
            SwitchToState(CharacterState.Idle);
        }
    }

    protected override void UpdateMoveEffects(bool isMoving)
    {
        if (vfxManager != null)
        {
            vfxManager.Update_FootStep(isMoving);
        }
    }

    private void BeginAttack()
    {
        attackTimer = Mathf.Max(attackDuration, 0f);
        SetAnimatorTrigger(AttackParameter);
        UpdateMoveEffects(false);
    }
}
