using UnityEngine;
#if UNITY_EDITOR && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class Player : Character
{
    private const string AttackParameter = "Attack";
    private const string SlideParameter = "Slide";
    private const int MaxComboCount = 3;
    private const float MoveInputThreshold = 0.001f;

    [SerializeField] private float attackDuration = 0.65f;
    [SerializeField] private float slideDuration = 0.5f;
    [SerializeField] private float slideDistance = 3f;
    [SerializeField] private float hurtImpactForce = 5f;

    private PlayerVFXManager vfxManager;
    private float attackTimer;
    private float slideTimer;
    private int currentComboIndex;
    private int requestedComboCount;
    private bool cancelQueuedCombosForMove;
    private Vector3 slideDirection;
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
        if (CurrentState == CharacterState.Slide
            || CurrentState == CharacterState.Hurt
            || CurrentState == CharacterState.Dead)
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

    public void Slide()
    {
        if (CurrentState == CharacterState.Attack
            || CurrentState == CharacterState.Slide
            || CurrentState == CharacterState.Hurt
            || CurrentState == CharacterState.Dead)
        {
            return;
        }

        slideDirection = GetSlideDirection();
        SwitchToState(CharacterState.Slide, true);
    }

    public void SlideAnimationEnds()
    {
        if (CurrentState == CharacterState.Slide)
        {
            FinishSlide();
        }
    }

    protected override void OnEnterAttack()
    {
        BeginAttackCombo(1);
    }

    protected override void OnEnterSlide()
    {
        slideTimer = Mathf.Max(slideDuration, 0f);
        DisableDamageCaster();
        SetAnimatorTrigger(SlideParameter);
        UpdateMoveEffects(false);
    }

    protected override void OnUpdateIdle(float deltaTime)
    {
        CheckEditorAttackInput();
        CheckEditorSlideInput();
    }

    protected override void OnUpdateRun(float deltaTime)
    {
        CheckEditorAttackInput();
        CheckEditorSlideInput();
    }

    protected override void OnUpdateAttack(float deltaTime)
    {
        CheckEditorAttackInput();

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

    protected override void OnUpdateSlide(float deltaTime)
    {
        if (slideTimer > 0f)
        {
            float duration = Mathf.Max(slideDuration, 0.01f);
            float movementDeltaTime = Mathf.Min(deltaTime, slideTimer);
            Vector3 movement = slideDirection * (slideDistance / duration) * movementDeltaTime;
            MoveBy(movement);
            slideTimer -= deltaTime;
        }

        RotateTowards(slideDirection, deltaTime);

        if (HasAnimator())
        {
            return;
        }

        if (slideTimer <= 0f)
        {
            FinishSlide();
        }
    }

    protected override void OnExitSlide()
    {
        slideTimer = 0f;
        slideDirection = Vector3.zero;
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

    private Vector3 GetSlideDirection()
    {
        Vector3 direction = GetMoveDirection();
        direction.y = 0f;

        if (direction.sqrMagnitude <= MoveInputThreshold)
        {
            direction = transform.forward;
            direction.y = 0f;
        }

        if (direction.sqrMagnitude <= MoveInputThreshold)
        {
            return Vector3.forward;
        }

        return direction.normalized;
    }

    private void FinishSlide()
    {
        if (CurrentState == CharacterState.Slide)
        {
            SwitchToState(HasMoveInput() ? CharacterState.Run : CharacterState.Idle);
        }
    }

    private void CheckEditorAttackInput()
    {
#if UNITY_EDITOR
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
        {
            Attack();
        }
#else
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            Attack();
        }
#endif
#endif
    }

    private void CheckEditorSlideInput()
    {
#if UNITY_EDITOR
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            Slide();
        }
#else
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Slide();
        }
#endif
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
