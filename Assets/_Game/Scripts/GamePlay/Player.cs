using UnityEngine;
#if UNITY_EDITOR && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class Player : Character
{
    private const string AttackParameter = "Attack";
    private const int MaxComboCount = 3;
    private const float MoveInputThreshold = 0.001f;

    [SerializeField] private float attackDuration = 0.65f;
    [SerializeField] private float hurtImpactForce = 5f;

    private PlayerVFXManager vfxManager;
    private float attackTimer;
    private int currentComboIndex;
    private int requestedComboCount;
    private bool cancelQueuedCombosForMove;
    protected override float HurtImpactForce => hurtImpactForce;
    protected override bool CanBecomeInvincible => true;

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
        if (CurrentState == CharacterState.Hurt || CurrentState == CharacterState.Dead)
        {
            return;
        }

        if (CurrentState == CharacterState.Attack)
        {
            QueueNextCombo();
            return;
        }

        requestedComboCount = 1;
        cancelQueuedCombosForMove = false;
        SwitchToState(CharacterState.Attack);
    }

    public void AttackAnimationEnds()
    {
        if (CurrentState == CharacterState.Attack)
        {
            CompleteCurrentCombo();
        }
    }

    protected override void OnEnterAttack()
    {
        BeginAttackCombo(1);
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
        if (HasMoveInput())
        {
            cancelQueuedCombosForMove = true;
            requestedComboCount = currentComboIndex;
        }

        if (HasAnimator())
        {
            return;
        }

        attackTimer -= deltaTime;

        if (attackTimer <= 0f)
        {
            CompleteCurrentCombo();
        }
    }

    protected override void OnExitAttack()
    {
        attackTimer = 0f;
        currentComboIndex = 0;
        requestedComboCount = 0;
        cancelQueuedCombosForMove = false;
        DisableDamageCaster();
        UpdateMoveEffects(false);
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

    private void BeginAttackCombo(int comboIndex)
    {
        currentComboIndex = Mathf.Clamp(comboIndex, 1, MaxComboCount);
        BeginAttack();
    }

    private void QueueNextCombo()
    {
        if (cancelQueuedCombosForMove || currentComboIndex >= MaxComboCount)
        {
            return;
        }

        requestedComboCount = Mathf.Clamp(requestedComboCount + 1, currentComboIndex, MaxComboCount);
    }

    private void CompleteCurrentCombo()
    {
        if (CurrentState != CharacterState.Attack)
        {
            return;
        }

        if (!cancelQueuedCombosForMove && currentComboIndex < requestedComboCount && currentComboIndex < MaxComboCount)
        {
            BeginAttackCombo(currentComboIndex + 1);
            return;
        }

        FinishAttack();
    }

    private void FinishAttack()
    {
        requestedComboCount = 0;
        currentComboIndex = 0;
        cancelQueuedCombosForMove = false;

        SwitchToState(HasMoveInput() ? CharacterState.Run : CharacterState.Idle);
    }

    private bool HasMoveInput()
    {
        return GetMoveDirection().sqrMagnitude > MoveInputThreshold;
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
