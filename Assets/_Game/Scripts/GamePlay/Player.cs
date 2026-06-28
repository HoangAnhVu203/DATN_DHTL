using UnityEngine;
#if ENABLE_INPUT_SYSTEM
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
    [SerializeField] private float keyboardInputAcceleration = 10f;
    [SerializeField] private float keyboardInputDeceleration = 14f;

    private PlayerVFXManager vfxManager;
    private float attackTimer;
    private float slideTimer;
    private int currentComboIndex;
    private int requestedComboCount;
    private bool cancelQueuedCombosForMove;
    private Vector3 slideDirection;
    private Vector3 smoothedKeyboardMoveInput;
    protected override float HurtImpactForce => hurtImpactForce;
    protected override bool CanBecomeInvincible => true;

    // Sets up this component before gameplay starts.
    protected override void Awake()
    {
        base.Awake();
        vfxManager = GetComponent<PlayerVFXManager>();
    }

    // Returns movement from keyboard or joystick input.
    protected override Vector3 GetMoveDirection()
    {
        Vector3 keyboardDirection = ReadKeyboardMoveInput();
        Vector3 joystickDirection = JoyStickController.direct;
        joystickDirection.y = 0f;

        bool hasKeyboardInput = keyboardDirection.sqrMagnitude > MoveInputThreshold;
        bool hasJoystickInput = joystickDirection.sqrMagnitude > MoveInputThreshold;

        if (hasJoystickInput && !hasKeyboardInput)
        {
            smoothedKeyboardMoveInput = Vector3.zero;
            return Vector3.ClampMagnitude(joystickDirection, 1f);
        }

        float inputSmoothingSpeed = hasKeyboardInput ? keyboardInputAcceleration : keyboardInputDeceleration;
        smoothedKeyboardMoveInput = Vector3.MoveTowards(
            smoothedKeyboardMoveInput,
            hasKeyboardInput ? keyboardDirection : Vector3.zero,
            Mathf.Max(0f, inputSmoothingSpeed) * Time.deltaTime
        );

        if (smoothedKeyboardMoveInput.sqrMagnitude > MoveInputThreshold)
        {
            return Vector3.ClampMagnitude(smoothedKeyboardMoveInput, 1f);
        }

        return hasJoystickInput ? Vector3.ClampMagnitude(joystickDirection, 1f) : Vector3.zero;
    }

    // Starts the attack action when the character can act.
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

    // Finishes the current attack step from an animation event.
    public void AttackAnimationEnds()
    {
        if (CurrentState == CharacterState.Attack)
        {
            CompleteCurrentCombo();
        }
    }

    // Starts the slide action when the character can move.
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

    // Ends the slide from an animation event.
    public void SlideAnimationEnds()
    {
        if (CurrentState == CharacterState.Slide)
        {
            FinishSlide();
        }
    }

    // Sets up the attack state.
    protected override void OnEnterAttack()
    {
        BeginAttackCombo(1);
    }

    // Sets up the slide state.
    protected override void OnEnterSlide()
    {
        slideTimer = Mathf.Max(slideDuration, 0f);
        DisableDamageCaster();
        SetAnimatorTrigger(SlideParameter);
        UpdateMoveEffects(false);
    }

    // Updates the idle state while it is active.
    protected override void OnUpdateIdle(float deltaTime)
    {
        CheckEditorAttackInput();
        CheckEditorSlideInput();
    }

    // Updates the run state while it is active.
    protected override void OnUpdateRun(float deltaTime)
    {
        CheckEditorAttackInput();
        CheckEditorSlideInput();
    }

    // Updates the attack state while it is active.
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

    // Cleans up the attack state.
    protected override void OnExitAttack()
    {
        attackTimer = 0f;
        currentComboIndex = 0;
        requestedComboCount = 0;
        cancelQueuedCombosForMove = false;
        DisableDamageCaster();
        UpdateMoveEffects(false);
    }

    // Updates the slide state while it is active.
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

    // Cleans up the slide state.
    protected override void OnExitSlide()
    {
        slideTimer = 0f;
        slideDirection = Vector3.zero;
        UpdateMoveEffects(false);
    }

    // Updates the move effects.
    protected override void UpdateMoveEffects(bool isMoving)
    {
        if (vfxManager != null)
        {
            vfxManager.Update_FootStep(isMoving);
        }
    }

    // Begins the attack step.
    private void BeginAttack()
    {
        attackTimer = Mathf.Max(attackDuration, 0f);
        SetAnimatorTrigger(AttackParameter);
        UpdateMoveEffects(false);
    }

    // Begins the attack combo step.
    private void BeginAttackCombo(int comboIndex)
    {
        currentComboIndex = Mathf.Clamp(comboIndex, 1, MaxComboCount);
        BeginAttack();
    }

    // Queues the next combo.
    private void QueueNextCombo()
    {
        if (cancelQueuedCombosForMove || currentComboIndex >= MaxComboCount)
        {
            return;
        }

        requestedComboCount = Mathf.Clamp(requestedComboCount + 1, currentComboIndex, MaxComboCount);
    }

    // Completes the current combo step.
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

    // Finishes the attack step.
    private void FinishAttack()
    {
        requestedComboCount = 0;
        currentComboIndex = 0;
        cancelQueuedCombosForMove = false;

        SwitchToState(HasMoveInput() ? CharacterState.Run : CharacterState.Idle);
    }

    // Checks whether move input is available.
    private bool HasMoveInput()
    {
        return GetMoveDirection().sqrMagnitude > MoveInputThreshold;
    }

    // Returns the slide direction.
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

    // Finishes the slide step.
    private void FinishSlide()
    {
        if (CurrentState == CharacterState.Slide)
        {
            SwitchToState(HasMoveInput() ? CharacterState.Run : CharacterState.Idle);
        }
    }

    // Checks editor attack input.
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

    // Checks editor slide input.
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

    // Reads WASD movement from the active input system.
    private Vector3 ReadKeyboardMoveInput()
    {
        Vector3 direction = Vector3.zero;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null)
        {
            return Vector3.zero;
        }

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
#else
        if (Input.GetKey(KeyCode.W))
        {
            direction.z += 1f;
        }

        if (Input.GetKey(KeyCode.S))
        {
            direction.z -= 1f;
        }

        if (Input.GetKey(KeyCode.D))
        {
            direction.x += 1f;
        }

        if (Input.GetKey(KeyCode.A))
        {
            direction.x -= 1f;
        }
#endif

        return Vector3.ClampMagnitude(direction, 1f);
    }
}
