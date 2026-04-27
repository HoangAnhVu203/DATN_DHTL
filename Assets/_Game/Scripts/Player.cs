using UnityEngine;
#if UNITY_EDITOR && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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
#if UNITY_EDITOR && ENABLE_INPUT_SYSTEM
        Vector3 editorDirection = GetEditorMoveDirection();

        if (editorDirection.sqrMagnitude > 0.001f)
        {
            return editorDirection;
        }
#endif

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

    protected override void OnUpdateIdle(float deltaTime)
    {
        CheckEditorAttackInput();
    }

    protected override void OnUpdateRun(float deltaTime)
    {
        CheckEditorAttackInput();
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

    private void CheckEditorAttackInput()
    {
#if UNITY_EDITOR && ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
        {
            Attack();
        }
#endif
    }

#if UNITY_EDITOR && ENABLE_INPUT_SYSTEM
    private Vector3 GetEditorMoveDirection()
    {
        if (Keyboard.current == null)
        {
            return Vector3.zero;
        }

        Vector3 direction = Vector3.zero;

        if (Keyboard.current.wKey.isPressed)
        {
            direction.z += 1f;
        }

        if (Keyboard.current.sKey.isPressed)
        {
            direction.z -= 1f;
        }

        if (Keyboard.current.dKey.isPressed)
        {
            direction.x += 1f;
        }

        if (Keyboard.current.aKey.isPressed)
        {
            direction.x -= 1f;
        }

        return Vector3.ClampMagnitude(direction, 1f);
    }
#endif
}
