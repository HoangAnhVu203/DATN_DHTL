using System.Collections;
using UnityEngine;

public abstract class Character : MonoBehaviour
{
    private const string SpeedParameter = "Speed";
    private const string IsGroundedParameter = "IsGrounded";
    private const string DeadParameter = "Dead";
    private static readonly int BlinkPropertyId = Shader.PropertyToID("_blink");
    private static readonly int EnableDissolvePropertyId = Shader.PropertyToID("_enableDissolve");
    private static readonly int DissolveHeightPropertyId = Shader.PropertyToID("_dissolve_height");
    private const float MoveInputThreshold = 0.001f;
    private const float BlinkAmount = 0.4f;
    private const float BlinkDuration = 0.2f;

    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 12f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedGravity = -2f;
    [SerializeField] private float moveAcceleration = 12f;
    [SerializeField] private float moveDeceleration = 18f;
    [SerializeField] private float animatorDampTime = 0.12f;
    [SerializeField] private float dissolveDelay = 2f;
    [SerializeField] private float dissolveDuration = 2f;
    [SerializeField] private float dissolveStartHeight = 20f;
    [SerializeField] private float dissolveEndHeight = -18f;
    [SerializeField] private GameObject itemDrop;

    private CharacterController characterController;
    private Health health;
    private DamageCaster damageCaster;
    private Rigidbody rb;
    private Animator animator;
    private Vector3 smoothedMoveDirection;
    private float verticalVelocity;
    private MaterialPropertyBlock materialPropertyBlock;
    private SkinnedMeshRenderer[] skinnedMeshRenderers;
    private Coroutine blinkCoroutine;
    private Coroutine dissolveCoroutine;


    public CharacterState CurrentState { get; private set; } = CharacterState.Idle;
    protected bool IsGrounded { get; private set; } = true;
    protected float MoveSpeed => moveSpeed;

    protected virtual void Awake()
    {
        characterController = GetComponent<CharacterController>();
        health = GetComponent<Health>();
        damageCaster = GetComponentInChildren<DamageCaster>();
        skinnedMeshRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
        materialPropertyBlock = new MaterialPropertyBlock();
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

        if (CurrentState == CharacterState.Dead)
        {
            smoothedMoveDirection = Vector3.zero;
            SetAnimatorFloat(SpeedParameter, 0f, 0f, deltaTime);
            UpdateMoveEffects(false);
            return;
        }

        bool canMove = CanMoveInCurrentState();
        Vector3 targetMoveDirection = canMove ? GetMoveDirection() : Vector3.zero;
        targetMoveDirection.y = 0f;
        targetMoveDirection = Vector3.ClampMagnitude(targetMoveDirection, 1f);

        if (!canMove)
        {
            smoothedMoveDirection = Vector3.zero;
        }

        bool hasMoveInput = targetMoveDirection.sqrMagnitude > MoveInputThreshold;
        float acceleration = hasMoveInput ? moveAcceleration : moveDeceleration;
        smoothedMoveDirection = Vector3.MoveTowards(
            smoothedMoveDirection,
            targetMoveDirection,
            acceleration * deltaTime
        );

        float speed = smoothedMoveDirection.magnitude;
        float animatorSpeed = canMove ? speed : 0f;
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

    protected bool HasAnimator()
    {
        return animator != null;
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
    protected virtual void OnEnterDead()
    {
        smoothedMoveDirection = Vector3.zero;
        verticalVelocity = 0f;
        SetAnimatorFloat(SpeedParameter, 0f, 0f, Time.deltaTime);
        SetAnimatorTrigger(DeadParameter);
        DisableDamageCaster();
        StartMaterialDissolve();

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
    protected virtual void OnUpdateDead(float deltaTime) { }
    protected virtual void OnExitDead() { }

    protected virtual void UpdateMoveEffects(bool isMoving)
    {
    }

    protected virtual void AfterMove()
    {
    }

    public void ApplyDamage(int damage, Vector3 attackPos = new Vector3())
    {
        if (health == null || health.IsDead)
        {
            return;
        }

        health.ApplyDamage(damage);

        EnemyVFXManager enemyVFXManager = GetComponent<EnemyVFXManager>();
        if (enemyVFXManager != null)
        {
            enemyVFXManager.PlayBeingHitVFX(attackPos);
        }

        if (health.IsDead)
        {
            SwitchToState(CharacterState.Dead);
            return;
        }

        PlayMaterialsBlink();
    }

    public void EnableDamageCaster()
    {
        if (damageCaster != null)
        {
            damageCaster.EnableDamageCaster();
        }
    }

    public void DisableDamageCaster()
    {
        if (damageCaster != null)
        {
            damageCaster.DisableDamageCaster();
        }
    }

    private void PlayMaterialsBlink()
    {
        if (skinnedMeshRenderers == null || skinnedMeshRenderers.Length == 0)
        {
            return;
        }

        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
        }

        blinkCoroutine = StartCoroutine(MaterialsBlink());
    }

    private IEnumerator MaterialsBlink()
    {
        SetMaterialsBlink(BlinkAmount);

        yield return new WaitForSeconds(BlinkDuration);

        SetMaterialsBlink(0f);
        blinkCoroutine = null;
    }

    private void SetMaterialsBlink(float value)
    {
        foreach (SkinnedMeshRenderer renderer in skinnedMeshRenderers)
        {
            if (renderer == null)
            {
                continue;
            }

            renderer.GetPropertyBlock(materialPropertyBlock);
            materialPropertyBlock.SetFloat(BlinkPropertyId, value);
            renderer.SetPropertyBlock(materialPropertyBlock);
        }
    }

    private void StartMaterialDissolve()
    {
        if (skinnedMeshRenderers == null || skinnedMeshRenderers.Length == 0)
        {
            return;
        }

        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;
            SetMaterialsBlink(0f);
        }

        if (dissolveCoroutine != null)
        {
            StopCoroutine(dissolveCoroutine);
        }

        dissolveCoroutine = StartCoroutine(MaterialDissolve());
    }

    private IEnumerator MaterialDissolve()
    {
        SetMaterialsFloat(EnableDissolvePropertyId, 1f);
        SetMaterialsFloat(DissolveHeightPropertyId, dissolveStartHeight);

        if (dissolveDelay > 0f)
        {
            yield return new WaitForSeconds(dissolveDelay);
        }

        float currentDissolveTime = 0f;
        float duration = Mathf.Max(0.01f, dissolveDuration);

        while (currentDissolveTime < duration)
        {
            currentDissolveTime += Time.deltaTime;
            float dissolveHeight = Mathf.Lerp(dissolveStartHeight, dissolveEndHeight, currentDissolveTime / duration);
            SetMaterialsFloat(DissolveHeightPropertyId, dissolveHeight);
            yield return null;
        }

        SetMaterialsFloat(DissolveHeightPropertyId, dissolveEndHeight);
        dissolveCoroutine = null;

        DropItem();
    }

    private void SetMaterialsFloat(int propertyId, float value)
    {
        foreach (SkinnedMeshRenderer renderer in skinnedMeshRenderers)
        {
            if (renderer == null)
            {
                continue;
            }

            renderer.GetPropertyBlock(materialPropertyBlock);
            materialPropertyBlock.SetFloat(propertyId, value);
            renderer.SetPropertyBlock(materialPropertyBlock);
        }
    }

    public void DropItem()
    {
        if(itemDrop != null)
        {
            Instantiate(itemDrop, new Vector3(transform.position.x, 0.3f, transform.position.z), Quaternion.identity);
        }
    }
}
