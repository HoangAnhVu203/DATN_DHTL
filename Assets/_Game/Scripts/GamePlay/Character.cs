using System;
using System.Collections;
using Fusion;
using UnityEngine;

public abstract class Character : MonoBehaviour
{
    private const string SpeedParameter = "Speed";
    private const string IsGroundedParameter = "IsGrounded";
    private const string DeadParameter = "Dead";
    private const string BeingHitParameter = "BeingHit";
    private const string PlayerTag = "Player";
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
    [SerializeField] private float hurtDuration = 0.5f;
    [SerializeField] private float impactDeceleration = 25f;
    [SerializeField] private float dissolveDelay = 2f;
    [SerializeField] private float dissolveDuration = 2f;
    [SerializeField] private float dissolveStartHeight = 20f;
    [SerializeField] private float dissolveEndHeight = -18f;
    [SerializeField] private bool playSpawnDissolveOnStart;
    [SerializeField] private float spawnDissolveDelay;
    [SerializeField] private GameObject itemDrop;
    [SerializeField] private float itemDropHeightOffset = 0.3f;
    [SerializeField] private bool isInvincible;
    [SerializeField] private float invincibleDuration = 2f;
    [SerializeField] private int Coin;


    private CharacterController characterController;
    private Health Health;
    private DamageCaster damageCaster;
    private DropWeapons dropWeapons;
    private Rigidbody rb;
    private Animator animator;
    private Vector3 smoothedMoveDirection;
    private float verticalVelocity;
    private MaterialPropertyBlock materialPropertyBlock;
    private SkinnedMeshRenderer[] skinnedMeshRenderers;
    private Coroutine blinkCoroutine;
    private Coroutine dissolveCoroutine;
    private Coroutine invincibleCoroutine;
    private float hurtTimer;
    private bool hasEnteredHurtAnimation;
    private Vector3 impactOnCharacter;
    private Transform targetPlayer;
    private bool isSpawnDissolving;
    private bool hasNotifiedDeath;
    private bool hasDeathDropPosition;
    private bool suppressNextDeathDissolve;
    private Vector3 deathDropPosition;
    private float baseMoveSpeed;


    public CharacterState CurrentState { get; private set; } = CharacterState.Idle;
    public bool IsSpawnDissolving => isSpawnDissolving;
    public int CoinAmount => Coin;
    public event Action<Character> Died;
    public event Action<int> CoinChanged;
    protected bool IsGrounded { get; private set; } = true;
    protected float MoveSpeed => moveSpeed;
    protected virtual float HurtImpactForce => 0f;
    protected virtual bool CanBecomeInvincible => false;
    public bool UseExternalMovementTick { get; set; }

    // Sets up this component before gameplay starts.
    protected virtual void Awake()
    {
        characterController = GetComponent<CharacterController>();
        Health = GetComponent<Health>();
        damageCaster = GetComponentInChildren<DamageCaster>();
        dropWeapons = GetComponent<DropWeapons>();
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
        baseMoveSpeed = moveSpeed;
    }

    // Runs the first scene-time setup for this object.
    protected virtual void Start()
    {
        EnterState(CurrentState);

        if (playSpawnDissolveOnStart)
        {
            PlaySpawnDissolve();
        }
    }

    // Runs the per-frame work for this behaviour.
    private void Update()
    {
        if (UseExternalMovementTick || rb != null)
        {
            return;
        }

        MoveCharacter(Time.deltaTime);
    }

    // Runs the physics-timed update for this behaviour.
    private void FixedUpdate()
    {
        if (UseExternalMovementTick || rb == null)
        {
            return;
        }

        MoveCharacter(Time.fixedDeltaTime);
    }

    // Runs one character movement tick.
    public void TickMovement(float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            return;
        }

        MoveCharacter(deltaTime);
    }

    // Moves the character.
    private void MoveCharacter(float deltaTime)
    {
        if (CurrentState == CharacterState.Dead)
        {
            smoothedMoveDirection = Vector3.zero;
            impactOnCharacter = Vector3.zero;
            SetAnimatorFloat(SpeedParameter, 0f, 0f, deltaTime);
            UpdateMoveEffects(false);
            return;
        }

        if (isSpawnDissolving)
        {
            smoothedMoveDirection = Vector3.zero;
            impactOnCharacter = Vector3.zero;
            SetAnimatorFloat(SpeedParameter, 0f, 0f, deltaTime);
            UpdateMoveEffects(false);
            AfterMove();
            return;
        }

        UpdateState(CurrentState, deltaTime);

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
            ApplyImpact(deltaTime);
            AfterMove();
            return;
        }

        UpdateMoveEffects(canMove && hasMoveInput && speed > MoveInputThreshold);

        ApplyImpact(deltaTime);

        if (speed <= MoveInputThreshold)
        {
            AfterMove();
            return;
        }

        Move(smoothedMoveDirection, deltaTime);
        Rotate(smoothedMoveDirection, deltaTime);
        AfterMove();
    }

    // Moves with CharacterController while preserving gravity and impact.
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

    // Moves the character.
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

    // Rotates toward the target direction.
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

    // Updates the animator float.
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

    // Updates the animator bool.
    private void SetAnimatorBool(string parameterName, bool value)
    {
        if (animator != null)
        {
            animator.SetBool(parameterName, value);
        }
    }

    // Updates the animator trigger.
    protected void SetAnimatorTrigger(string parameterName)
    {
        if (animator != null)
        {
            animator.SetTrigger(parameterName);
        }
    }

    // Checks whether animator is available.
    protected bool HasAnimator()
    {
        return animator != null;
    }

    // Switches the character to a new state.
    public void SwitchToState(CharacterState newState, bool forceRestart = false)
    {
        if (isSpawnDissolving
            && newState != CharacterState.Idle
            && newState != CharacterState.Hurt
            && newState != CharacterState.Dead)
        {
            return;
        }

        if (CurrentState == newState && !forceRestart)
        {
            return;
        }

        CharacterState previousState = CurrentState;
        ExitState(previousState);
        CurrentState = newState;
        EnterState(CurrentState);
    }

    // Updates the movement state.
    private void UpdateMovementState(float speed)
    {
        if (!CanAutoSwitchMovementState())
        {
            return;
        }

        SwitchToState(speed > MoveInputThreshold ? CharacterState.Run : CharacterState.Idle);
    }

    // Checks whether the character can auto-switch movement state.
    private bool CanAutoSwitchMovementState()
    {
        return CurrentState == CharacterState.Idle || CurrentState == CharacterState.Run;
    }

    // Checks whether the current state allows movement.
    private bool CanMoveInCurrentState()
    {
        return !isSpawnDissolving
            && CurrentState != CharacterState.Attack
            && CurrentState != CharacterState.Slide
            && CurrentState != CharacterState.Hurt
            && CurrentState != CharacterState.Dead;
    }

    protected abstract Vector3 GetMoveDirection();

    // Enters the requested character state.
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

            case CharacterState.Slide:
                OnEnterSlide();
                break;

            case CharacterState.Hurt:
                OnEnterHurt();
                break;

            case CharacterState.Dead:
                OnEnterDead();
                break;
        }
    }

    // Updates the state.
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

            case CharacterState.Slide:
                OnUpdateSlide(deltaTime);
                break;

            case CharacterState.Hurt:
                OnUpdateHurt(deltaTime);
                break;

            case CharacterState.Dead:
                OnUpdateDead(deltaTime);
                break;
        }
    }

    // Exits the requested character state.
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

            case CharacterState.Slide:
                OnExitSlide();
                break;

            case CharacterState.Hurt:
                OnExitHurt();
                break;

            case CharacterState.Dead:
                OnExitDead();
                break;
        }
    }

    // Sets up the idle state.
    protected virtual void OnEnterIdle() { }
    // Updates the idle state while it is active.
    protected virtual void OnUpdateIdle(float deltaTime) { }
    // Cleans up the idle state.
    protected virtual void OnExitIdle() { }
    // Sets up the run state.
    protected virtual void OnEnterRun() { }
    // Updates the run state while it is active.
    protected virtual void OnUpdateRun(float deltaTime) { }
    // Cleans up the run state.
    protected virtual void OnExitRun() { }
    // Sets up the attack state.
    protected virtual void OnEnterAttack() { }
    // Updates the attack state while it is active.
    protected virtual void OnUpdateAttack(float deltaTime) { }
    // Cleans up the attack state.
    protected virtual void OnExitAttack() { }
    // Sets up the slide state.
    protected virtual void OnEnterSlide() { }
    // Updates the slide state while it is active.
    protected virtual void OnUpdateSlide(float deltaTime) { }
    // Cleans up the slide state.
    protected virtual void OnExitSlide() { }
    // Sets up the hurt state.
    protected virtual void OnEnterHurt()
    {
        hurtTimer = Mathf.Max(hurtDuration, 0f);
        hasEnteredHurtAnimation = false;
        smoothedMoveDirection = Vector3.zero;
        verticalVelocity = 0f;
        SetAnimatorFloat(SpeedParameter, 0f, 0f, Time.deltaTime);
        SetAnimatorTrigger(BeingHitParameter);
        DisableDamageCaster();
        UpdateMoveEffects(false);

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    // Updates the hurt state while it is active.
    protected virtual void OnUpdateHurt(float deltaTime)
    {
        if (HasAnimator())
        {
            if (IsHurtAnimationFinished(deltaTime))
            {
                FinishHurt();
            }

            return;
        }

        hurtTimer -= deltaTime;

        if (hurtTimer <= 0f && !HasAnimator())
        {
            FinishHurt();
        }
    }
    // Cleans up the hurt state.
    protected virtual void OnExitHurt()
    {
        impactOnCharacter = Vector3.zero;
    }

    // Sets up the dead state.
    protected virtual void OnEnterDead()
    {
        deathDropPosition = transform.position;
        hasDeathDropPosition = true;
        smoothedMoveDirection = Vector3.zero;
        verticalVelocity = 0f;
        impactOnCharacter = Vector3.zero;
        NotifyDied();
        CancelInvincible();
        SetAnimatorFloat(SpeedParameter, 0f, 0f, Time.deltaTime);
        SetAnimatorTrigger(DeadParameter);
        DisableDamageCaster();

        if (suppressNextDeathDissolve)
        {
            suppressNextDeathDissolve = false;
            StopMaterialDissolve();
        }
        else
        {
            StartMaterialDissolve();
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
    // Updates the dead state while it is active.
    protected virtual void OnUpdateDead(float deltaTime) { }
    // Cleans up the dead state.
    protected virtual void OnExitDead() { }

    // Notifies listeners that died occurred.
    private void NotifyDied()
    {
        if (hasNotifiedDeath)
        {
            return;
        }

        hasNotifiedDeath = true;
        Died?.Invoke(this);
    }

    // Updates the move effects.
    protected virtual void UpdateMoveEffects(bool isMoving)
    {
    }

    // Runs post-move hooks after movement finishes.
    protected virtual void AfterMove()
    {
    }

    // Moves the by.
    protected void MoveBy(Vector3 movement)
    {
        if (characterController != null)
        {
            characterController.Move(movement);
            return;
        }

        if (rb != null)
        {
            rb.MovePosition(rb.position + movement);
            return;
        }

        transform.position += movement;
    }

    // Rotates toward the towards.
    protected void RotateTowards(Vector3 direction, float deltaTime)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude <= MoveInputThreshold)
        {
            return;
        }

        Rotate(direction.normalized, deltaTime);
    }

    // Applies the damage.
    public void ApplyDamage(int damage, Vector3 attackPos = new Vector3())
    {
        if (CanBecomeInvincible && isInvincible)
        {
            return;
        }

        if (Health == null || Health.IsDead)
        {
            return;
        }

        Health.ApplyDamage(damage);
        StartInvincible();

        EnemyVFXManager enemyVFXManager = GetComponent<EnemyVFXManager>();
        if (enemyVFXManager != null)
        {
            enemyVFXManager.PlayBeingHitVFX(attackPos);
        }

        if (Health.IsDead)
        {
            SwitchToState(CharacterState.Dead);
            return;
        }

        PlayMaterialsBlink();
        SwitchToState(CharacterState.Hurt, true);
        AddImpact(attackPos, HurtImpactForce);
    }

    // Starts the short invincibility window after taking damage.
    private void StartInvincible()
    {
        if (!CanBecomeInvincible || invincibleDuration <= 0f)
        {
            return;
        }

        isInvincible = true;

        if (invincibleCoroutine != null)
        {
            StopCoroutine(invincibleCoroutine);
        }

        invincibleCoroutine = StartCoroutine(DelayCancelInvincible());
    }

    // Runs the delay cancel invincible step.
    private IEnumerator DelayCancelInvincible()
    {
        yield return new WaitForSeconds(invincibleDuration);
        isInvincible = false;
        invincibleCoroutine = null;
    }

    // Checks whether temporary invincibility has expired.
    private void CancelInvincible()
    {
        if (invincibleCoroutine != null)
        {
            StopCoroutine(invincibleCoroutine);
            invincibleCoroutine = null;
        }

        isInvincible = false;
    }

    // Enables the damage caster.
    public void EnableDamageCaster()
    {
        if (damageCaster != null)
        {
            damageCaster.EnableDamageCaster();
        }
    }

    // Disables the damage caster.
    public void DisableDamageCaster()
    {
        if (damageCaster != null)
        {
            damageCaster.DisableDamageCaster();
        }
    }

    // Applies the runtime move speed multiplier.
    public virtual void ApplyRuntimeMoveSpeedMultiplier(float multiplier)
    {
        float safeMultiplier = Mathf.Max(0.1f, multiplier);
        if (baseMoveSpeed <= 0f)
        {
            baseMoveSpeed = moveSpeed;
        }

        moveSpeed = baseMoveSpeed * safeMultiplier;
    }

    // Skips the next death dissolve effect.
    public void SuppressNextDeathDissolve()
    {
        suppressNextDeathDissolve = true;
    }

    // Revives the character with the supplied health.
    public void Revive(int reviveHealth)
    {
        StopMaterialDissolve();
        CancelInvincible();
        hasNotifiedDeath = false;
        hasDeathDropPosition = false;
        smoothedMoveDirection = Vector3.zero;
        verticalVelocity = 0f;
        impactOnCharacter = Vector3.zero;

        if (Health != null)
        {
            int maxHealth = Mathf.Max(1, Health.maxHealth);
            Health.SetHealthFromNetwork(Mathf.Clamp(reviveHealth, 1, maxHealth), maxHealth);
        }

        if (animator != null)
        {
            animator.Rebind();
            animator.Update(0f);
        }

        dropWeapons?.PickUpWeapons();
        SwitchToState(CharacterState.Idle, true);
    }

    // Starts the material blink feedback after a hit.
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

    // Animates the hit blink on character materials.
    private IEnumerator MaterialsBlink()
    {
        SetMaterialsBlink(BlinkAmount);

        yield return new WaitForSeconds(BlinkDuration);

        SetMaterialsBlink(0f);
        blinkCoroutine = null;
    }

    // Updates the materials blink.
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

    // Starts the material dissolve process.
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

        isSpawnDissolving = false;
        dissolveCoroutine = StartCoroutine(MaterialDissolve());
    }

    // Stops the material dissolve process.
    private void StopMaterialDissolve()
    {
        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;
            SetMaterialsBlink(0f);
        }

        if (dissolveCoroutine != null)
        {
            StopCoroutine(dissolveCoroutine);
            dissolveCoroutine = null;
        }

        isSpawnDissolving = false;
        SetMaterialsFloat(EnableDissolvePropertyId, 0f);
        SetMaterialsFloat(DissolveHeightPropertyId, dissolveStartHeight);
    }

    // Plays the spawn dissolve.
    public void PlaySpawnDissolve()
    {
        if (skinnedMeshRenderers == null || skinnedMeshRenderers.Length == 0)
        {
            return;
        }

        if (CurrentState == CharacterState.Dead)
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

        dissolveCoroutine = StartCoroutine(MaterialSpawnDissolve());
    }

    // Animates the spawn dissolve on character materials.
    private IEnumerator MaterialSpawnDissolve()
    {
        isSpawnDissolving = true;
        DisableDamageCaster();
        SetMaterialsFloat(EnableDissolvePropertyId, 1f);
        SetMaterialsFloat(DissolveHeightPropertyId, dissolveEndHeight);

        if (spawnDissolveDelay > 0f)
        {
            yield return new WaitForSeconds(spawnDissolveDelay);
        }

        float currentDissolveTime = 0f;
        float duration = Mathf.Max(0.01f, dissolveDuration);

        while (currentDissolveTime < duration)
        {
            currentDissolveTime += Time.deltaTime;
            float dissolveHeight = Mathf.Lerp(dissolveEndHeight, dissolveStartHeight, currentDissolveTime / duration);
            SetMaterialsFloat(DissolveHeightPropertyId, dissolveHeight);
            yield return null;
        }

        SetMaterialsFloat(DissolveHeightPropertyId, dissolveStartHeight);
        SetMaterialsFloat(EnableDissolvePropertyId, 0f);
        isSpawnDissolving = false;
        dissolveCoroutine = null;
    }

    // Animates the death dissolve on character materials.
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

    // Updates the materials float.
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

    // Drops the item.
    public void DropItem()
    {
        if (itemDrop == null)
        {
            return;
        }

        Vector3 dropBasePosition = hasDeathDropPosition ? deathDropPosition : transform.position;
        Vector3 dropPosition = dropBasePosition + Vector3.up * itemDropHeightOffset;
        FusionEnemyAvatar networkEnemy = GetComponent<FusionEnemyAvatar>();
        if (networkEnemy != null && networkEnemy.Object != null && networkEnemy.Object.IsValid)
        {
            if (!networkEnemy.Object.HasStateAuthority)
            {
                return;
            }

            NetworkObject itemNetworkObject = itemDrop.GetComponent<NetworkObject>();
            if (itemNetworkObject != null && networkEnemy.Runner != null && networkEnemy.Runner.IsRunning)
            {
                networkEnemy.Runner.Spawn(
                    itemNetworkObject,
                    dropPosition,
                    Quaternion.identity,
                    PlayerRef.None,
                    null,
                    NetworkSpawnFlags.SharedModeStateAuthMasterClient
                );
                return;
            }
        }

        Instantiate(itemDrop, dropPosition, Quaternion.identity);
    }

    // Applies the pickup value.
    public void ApplyPickupValue(PickUpType pickupType, int value)
    {
        switch (pickupType)
        {
            case PickUpType.Health:
                AddHealth(value);
                break;

            case PickUpType.Coin:
                AddCoin(value);
                break;
        }
    }

    // Checks whether the hurt animation has finished.
    private bool IsHurtAnimationFinished(float deltaTime)
    {
        AnimatorStateInfo currentStateInfo = animator.GetCurrentAnimatorStateInfo(0);
        AnimatorStateInfo nextStateInfo = animator.GetNextAnimatorStateInfo(0);
        bool isCurrentHurt = currentStateInfo.IsName(BeingHitParameter);
        bool isNextHurt = animator.IsInTransition(0) && nextStateInfo.IsName(BeingHitParameter);

        if (isCurrentHurt || isNextHurt)
        {
            hasEnteredHurtAnimation = true;
            return isCurrentHurt && !animator.IsInTransition(0) && currentStateInfo.normalizedTime >= 1f;
        }

        if (hasEnteredHurtAnimation)
        {
            return true;
        }

        hurtTimer -= deltaTime;
        return hurtTimer <= 0f;
    }

    // Finishes the hurt step.
    private void FinishHurt()
    {
        if (CurrentState == CharacterState.Hurt)
        {
            SwitchToState(CharacterState.Idle);
        }
    }

    // Adds the impact.
    private void AddImpact(Vector3 attackerPos, float force)
    {
        if (force <= 0f)
        {
            return;
        }

        Vector3 impactDir = transform.position - attackerPos;
        impactDir.y = 0f;

        if (impactDir.sqrMagnitude <= 0.001f)
        {
            impactDir = -transform.forward;
        }
        else
        {
            impactDir.Normalize();
        }

        impactOnCharacter = impactDir * force;
    }

    // Applies the impact.
    private void ApplyImpact(float deltaTime)
    {
        if (impactOnCharacter.sqrMagnitude <= 0.001f)
        {
            impactOnCharacter = Vector3.zero;
            return;
        }

        Vector3 impactMovement = impactOnCharacter * deltaTime;

        if (characterController != null)
        {
            characterController.Move(impactMovement);
        }
        else if (rb != null)
        {
            rb.MovePosition(rb.position + impactMovement);
        }
        else
        {
            transform.position += impactMovement;
        }

        impactOnCharacter = Vector3.MoveTowards(
            impactOnCharacter,
            Vector3.zero,
            impactDeceleration * deltaTime
        );
    }

    // Picks up the item.
    public void PickUpItem(PickUp item)
    {
        switch (item.type)
        {
            case PickUpType.Health:
                AddHealth(item.value);
                break;

            case PickUpType.Coin:
                AddCoin(item.value);
                break;
        }
    }

    // Adds the health.
    private void AddHealth(int health)
    {
        Health.AddHealth(health);

        PlayerVFXManager playerVFXManager = GetComponent<PlayerVFXManager>();
        if (playerVFXManager != null)
        {
            playerVFXManager.PlayerHealthVFX();
        }
    }

    // Adds the coin.
    private void AddCoin(int coin)
    {
        Coin += coin;
        CoinChanged?.Invoke(Coin);
    }

    // Turns the character toward the current target.
    public void RotateToTarget()
    {
        if (CurrentState == CharacterState.Dead || isSpawnDissolving)
        {
            return;
        }

        if (targetPlayer == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag(PlayerTag);
            if (playerObject == null)
            {
                return;
            }

            targetPlayer = playerObject.transform;
        }

        Vector3 direction = targetPlayer.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= MoveInputThreshold)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(direction.normalized);
    }
}
