using UnityEngine;

public abstract class Character : MonoBehaviour
{
    private const string SpeedParameter = "Speed";
    private const string IsGroundedParameter = "IsGrounded";
    private const float MoveInputThreshold = 0.001f;

    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 12f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedGravity = -2f;
    [SerializeField] private float moveAcceleration = 12f;
    [SerializeField] private float moveDeceleration = 18f;
    [SerializeField] private float animatorDampTime = 0.12f;

    private CharacterController characterController;
    private Rigidbody rb;
    private Animator animator;
    private Vector3 smoothedMoveDirection;
    private float verticalVelocity;

    public CharacterState CurrentState { get; private set; } = CharacterState.Idle;
    protected bool IsGrounded { get; private set; } = true;
    protected float MoveSpeed => moveSpeed;

    protected virtual void Awake()
    {
        characterController = GetComponent<CharacterController>();
        Rigidbody attachedRigidbody = GetComponent<Rigidbody>();

        if (characterController != null && attachedRigidbody != null)
        {
            attachedRigidbody.isKinematic = true;
            attachedRigidbody.useGravity = false;
        }

        rb = characterController == null ? attachedRigidbody : null;
        animator = GetComponent<Animator>();
    }

    protected virtual void Start()
    {
        EnterState(CurrentState);
    }

    private void Update()
    {
        if (rb != null)
        {
            return;
        }

        MoveCharacter(Time.deltaTime);
    }

    private void FixedUpdate()
    {
        if (rb == null)
        {
            return;
        }

        MoveCharacter(Time.fixedDeltaTime);
    }

    private void MoveCharacter(float deltaTime)
    {
        UpdateState(CurrentState, deltaTime);

        bool canMove = CanMoveInCurrentState();
        Vector3 targetMoveDirection = canMove ? GetMoveDirection() : Vector3.zero;
        targetMoveDirection.y = 0f;
        targetMoveDirection = Vector3.ClampMagnitude(targetMoveDirection, 1f);

        bool hasMoveInput = targetMoveDirection.sqrMagnitude > MoveInputThreshold;
        float acceleration = hasMoveInput ? moveAcceleration : moveDeceleration;
        smoothedMoveDirection = Vector3.MoveTowards(
            smoothedMoveDirection,
            targetMoveDirection,
            acceleration * deltaTime
        );

        float speed = smoothedMoveDirection.magnitude;
        float animatorSpeed = hasMoveInput ? speed : 0f;
        SetAnimatorFloat(SpeedParameter, animatorSpeed, hasMoveInput ? animatorDampTime : 0f, deltaTime);
        UpdateMovementState(animatorSpeed);

        if (characterController != null)
        {
            MoveWithCharacterController(smoothedMoveDirection, speed, canMove, hasMoveInput, deltaTime);
            AfterMove();
            return;
        }

        UpdateMoveEffects(canMove && hasMoveInput && speed > MoveInputThreshold);

        if (speed <= MoveInputThreshold)
        {
            AfterMove();
            return;
        }

        Move(smoothedMoveDirection, deltaTime);
        Rotate(smoothedMoveDirection, deltaTime);
        AfterMove();
    }

    private void MoveWithCharacterController(Vector3 direction, float speed, bool canMove, bool hasMoveInput, float deltaTime)
    {
        IsGrounded = characterController.isGrounded;
        SetAnimatorBool(IsGroundedParameter, IsGrounded);

        if (IsGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = groundedGravity;
        }

        verticalVelocity += gravity * deltaTime;

        Vector3 movement = direction * moveSpeed;
        movement.y = verticalVelocity;
        characterController.Move(movement * deltaTime);

        IsGrounded = characterController.isGrounded;
        SetAnimatorBool(IsGroundedParameter, IsGrounded);
        UpdateMoveEffects(canMove && hasMoveInput && speed > MoveInputThreshold && IsGrounded);

        if (speed > MoveInputThreshold)
        {
            Rotate(direction, deltaTime);
        }
    }

    private void Move(Vector3 direction, float deltaTime)
    {
        Vector3 movement = direction * moveSpeed * deltaTime;

        if (rb != null)
        {
            rb.MovePosition(rb.position + movement);
            return;
        }

        transform.position += movement;
    }

    private void Rotate(Vector3 direction, float deltaTime)
    {
        Quaternion targetRotation = Quaternion.LookRotation(direction);

        if (rb != null)
        {
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * deltaTime));
            return;
        }

        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * deltaTime);
    }

    private void SetAnimatorFloat(string parameterName, float value, float dampTime, float deltaTime)
    {
        if (animator != null)
        {
            if (dampTime <= 0f)
            {
                animator.SetFloat(parameterName, value);
                return;
            }

            animator.SetFloat(parameterName, value, dampTime, deltaTime);
        }
    }

    private void SetAnimatorBool(string parameterName, bool value)
    {
        if (animator != null)
        {
            animator.SetBool(parameterName, value);
        }
    }

    protected void SetAnimatorTrigger(string parameterName)
    {
        if (animator != null)
        {
            animator.SetTrigger(parameterName);
        }
    }

    public void SwitchToState(CharacterState newState)
    {
        if (CurrentState == newState)
        {
            return;
        }

        CharacterState previousState = CurrentState;
        ExitState(previousState);
        CurrentState = newState;
        EnterState(CurrentState);
    }

    private void UpdateMovementState(float speed)
    {
        if (!CanAutoSwitchMovementState())
        {
            return;
        }

        SwitchToState(speed > MoveInputThreshold ? CharacterState.Run : CharacterState.Idle);
    }

    private bool CanAutoSwitchMovementState()
    {
        return CurrentState == CharacterState.Idle || CurrentState == CharacterState.Run;
    }

    private bool CanMoveInCurrentState()
    {
        return CurrentState != CharacterState.Attack
            && CurrentState != CharacterState.Hurt
            && CurrentState != CharacterState.Dead;
    }

    protected abstract Vector3 GetMoveDirection();

    private void EnterState(CharacterState state)
    {
        switch (state)
        {
            case CharacterState.Idle:
                OnEnterIdle();
                break;

            case CharacterState.Run:
                OnEnterRun();
                break;

            case CharacterState.Attack:
                OnEnterAttack();
                break;

            case CharacterState.Hurt:
                OnEnterHurt();
                break;

            case CharacterState.Dead:
                OnEnterDead();
                break;
        }
    }

    private void UpdateState(CharacterState state, float deltaTime)
    {
        switch (state)
        {
            case CharacterState.Idle:
                OnUpdateIdle(deltaTime);
                break;

            case CharacterState.Run:
                OnUpdateRun(deltaTime);
                break;

            case CharacterState.Attack:
                OnUpdateAttack(deltaTime);
                break;

            case CharacterState.Hurt:
                OnUpdateHurt(deltaTime);
                break;

            case CharacterState.Dead:
                OnUpdateDead(deltaTime);
                break;
        }
    }

    private void ExitState(CharacterState state)
    {
        switch (state)
        {
            case CharacterState.Idle:
                OnExitIdle();
                break;

            case CharacterState.Run:
                OnExitRun();
                break;

            case CharacterState.Attack:
                OnExitAttack();
                break;

            case CharacterState.Hurt:
                OnExitHurt();
                break;

            case CharacterState.Dead:
                OnExitDead();
                break;
        }
    }

    protected virtual void OnEnterIdle() { }
    protected virtual void OnUpdateIdle(float deltaTime) { }
    protected virtual void OnExitIdle() { }
    protected virtual void OnEnterRun() { }
    protected virtual void OnUpdateRun(float deltaTime) { }
    protected virtual void OnExitRun() { }
    protected virtual void OnEnterAttack() { }
    protected virtual void OnUpdateAttack(float deltaTime) { }
    protected virtual void OnExitAttack() { }
    protected virtual void OnEnterHurt() { }
    protected virtual void OnUpdateHurt(float deltaTime) { }
    protected virtual void OnExitHurt() { }
    protected virtual void OnEnterDead() { }
    protected virtual void OnUpdateDead(float deltaTime) { }
    protected virtual void OnExitDead() { }

    protected virtual void UpdateMoveEffects(bool isMoving)
    {
    }

    protected virtual void AfterMove()
    {
    }
}
