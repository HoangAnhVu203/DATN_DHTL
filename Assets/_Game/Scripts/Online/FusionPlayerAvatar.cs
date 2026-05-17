using Fusion;
using Unity.Cinemachine;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class FusionPlayerAvatar : NetworkBehaviour
{
    private static readonly int SpeedParameter = Animator.StringToHash("Speed");
    private static readonly int IsGroundedParameter = Animator.StringToHash("IsGrounded");
    private static readonly int AttackParameter = Animator.StringToHash("Attack");
    private static readonly int SlideParameter = Animator.StringToHash("Slide");
    private const float MoveInputThreshold = 0.001f;

    [SerializeField] private Player player;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Animator animator;
    [SerializeField] private DamageCaster damageCaster;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 12f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedGravity = -2f;
    [SerializeField] private float animatorDampTime = 0.12f;
    [SerializeField] private float attackDuration = 0.65f;
    [SerializeField] private float attackDamageDelay = 0.15f;
    [SerializeField] private float attackDamageDuration = 0.25f;
    [SerializeField] private float slideDuration = 0.5f;
    [SerializeField] private float slideDistance = 3f;
    [SerializeField] private bool setCameraFollowTarget = true;

    [Networked] private float NetworkedSpeed { get; set; }
    [Networked] private NetworkBool NetworkedGrounded { get; set; }

    private bool? lastLocalControlState;
    private bool cameraBound;
    private float verticalVelocity;
    private float attackTimer;
    private float attackDamageDelayTimer;
    private float attackDamageTimer;
    private float slideTimer;
    private Vector3 slideDirection;
    private bool attackQueued;
    private bool slideQueued;

    public bool CanApplyDamageLocally => HasLocalControl();

    private void Awake()
    {
        ResolveReferences();
    }

    public override void Spawned()
    {
        ApplyAuthorityState();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (HasLocalControl())
        {
            gameObject.tag = "Untagged";
        }
    }

    private void OnEnable()
    {
        if (Object != null && Object.IsValid)
        {
            ApplyAuthorityState();
        }
    }

    private void ApplyAuthorityState()
    {
        ResolveReferences();

        bool isLocalPlayer = HasLocalControl();
        if (lastLocalControlState.HasValue && lastLocalControlState.Value == isLocalPlayer)
        {
            if (isLocalPlayer && setCameraFollowTarget && !cameraBound)
            {
                BindCinemachineCamera();
            }

            return;
        }

        lastLocalControlState = isLocalPlayer;
        gameObject.name = isLocalPlayer ? "NetworkPlayer_Local" : "NetworkPlayer_Remote";
        gameObject.tag = isLocalPlayer ? "Player" : "Untagged";

        // Networked movement is handled here. The old Player script still owns attack logic,
        // but its Update movement must stay off or it can fight with NetworkTransform.
        if (player != null)
        {
            player.enabled = false;
        }

        if (characterController != null)
        {
            characterController.enabled = true;
        }

        if (!isLocalPlayer && damageCaster != null)
        {
            damageCaster.DisableDamageCaster();
        }

        if (isLocalPlayer && setCameraFollowTarget)
        {
            BindCinemachineCamera();
        }

        Debug.Log(
            $"FusionPlayerAvatar: {(isLocalPlayer ? "local" : "remote")} player. " +
            $"InputAuthority={Object.InputAuthority}, StateAuthority={Object.StateAuthority}, LocalPlayer={Runner.LocalPlayer}"
        );
    }

    private bool HasLocalControl()
    {
        if (Object == null || !Object.IsValid || Runner == null)
        {
            return false;
        }

        PlayerRef localPlayer = Runner.LocalPlayer;
        return Object.HasInputAuthority
               || Object.HasStateAuthority
               || Object.InputAuthority == localPlayer
               || Object.StateAuthority == localPlayer;
    }

    private void ResolveReferences()
    {
        if (player == null)
        {
            player = GetComponent<Player>();
        }

        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (damageCaster == null)
        {
            damageCaster = GetComponentInChildren<DamageCaster>(true);
        }
    }

    private void BindCinemachineCamera()
    {
        CinemachineCamera cinemachineCamera = FindFirstObjectByType<CinemachineCamera>();
        if (cinemachineCamera == null)
        {
            return;
        }

        CameraTarget target = cinemachineCamera.Target;
        target.TrackingTarget = transform;
        cinemachineCamera.Target = target;
        cameraBound = true;
    }

    private void LateUpdate()
    {
        if (Object != null && Object.IsValid)
        {
            ApplyAuthorityState();
        }
    }

    private void Update()
    {
        QueueKeyboardActions();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasLocalControl() || characterController == null || !characterController.enabled)
        {
            return;
        }

        ConsumeQueuedActions();
        UpdateAttackDamageWindow(Runner.DeltaTime);
        MoveLocalPlayer(Runner.DeltaTime);
    }

    public override void Render()
    {
        if (animator == null || HasLocalControl())
        {
            return;
        }

        animator.SetFloat(SpeedParameter, NetworkedSpeed, animatorDampTime, Time.deltaTime);
        animator.SetBool(IsGroundedParameter, NetworkedGrounded);
    }

    public void RequestAttack()
    {
        if (!HasLocalControl())
        {
            return;
        }

        attackQueued = true;
    }

    public void RequestSlide()
    {
        if (!HasLocalControl())
        {
            return;
        }

        slideQueued = true;
    }

    public bool RequestDamage(int damage, Vector3 attackPosition)
    {
        if (damage <= 0 || Object == null || !Object.IsValid)
        {
            return false;
        }

        RPC_ApplyDamage(damage, attackPosition);
        return true;
    }

    private void MoveLocalPlayer(float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            return;
        }

        Vector3 inputDirection = slideTimer > 0f ? slideDirection : GetMoveInput();
        inputDirection.y = 0f;
        inputDirection = Vector3.ClampMagnitude(inputDirection, 1f);

        bool hasMoveInput = inputDirection.sqrMagnitude > MoveInputThreshold;

        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = groundedGravity;
        }

        verticalVelocity += gravity * deltaTime;

        float currentMoveSpeed = moveSpeed;
        if (attackTimer > 0f)
        {
            inputDirection = Vector3.zero;
            hasMoveInput = false;
        }
        else if (slideTimer > 0f)
        {
            float duration = Mathf.Max(slideDuration, 0.01f);
            currentMoveSpeed = slideDistance / duration;
            slideTimer = Mathf.Max(slideTimer - deltaTime, 0f);
        }

        Vector3 movement = inputDirection * currentMoveSpeed;
        movement.y = verticalVelocity;
        characterController.Move(movement * deltaTime);

        if (hasMoveInput)
        {
            Quaternion targetRotation = Quaternion.LookRotation(inputDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * deltaTime);
        }

        if (animator != null)
        {
            float speed = hasMoveInput ? inputDirection.magnitude : 0f;
            animator.SetFloat(SpeedParameter, speed, animatorDampTime, deltaTime);
            animator.SetBool(IsGroundedParameter, characterController.isGrounded);
            NetworkedSpeed = speed;
            NetworkedGrounded = characterController.isGrounded;
        }

        if (attackTimer > 0f)
        {
            attackTimer = Mathf.Max(attackTimer - deltaTime, 0f);
        }
    }

    private Vector3 GetMoveInput()
    {
        Vector3 direction = Vector3.zero;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
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

        if (direction.sqrMagnitude <= MoveInputThreshold)
        {
            direction = JoyStickController.direct;
        }

        return Vector3.ClampMagnitude(direction, 1f);
    }

    private void QueueKeyboardActions()
    {
        if (!HasLocalControl())
        {
            return;
        }

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)
        {
            attackQueued = true;
        }

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            slideQueued = true;
        }
#else
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            attackQueued = true;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            slideQueued = true;
        }
#endif
    }

    private void ConsumeQueuedActions()
    {
        if (attackQueued)
        {
            attackQueued = false;
            TryStartAttack();
        }

        if (slideQueued)
        {
            slideQueued = false;
            TryStartSlide();
        }
    }

    private void TryStartAttack()
    {
        if (attackTimer > 0f || slideTimer > 0f)
        {
            return;
        }

        attackTimer = Mathf.Max(attackDuration, 0f);
        attackDamageDelayTimer = Mathf.Max(attackDamageDelay, 0f);
        attackDamageTimer = Mathf.Max(attackDamageDuration, 0f);
        damageCaster?.DisableDamageCaster();
        RPC_PlayAttack();
    }

    private void TryStartSlide()
    {
        if (attackTimer > 0f || slideTimer > 0f)
        {
            return;
        }

        Vector3 direction = GetMoveInput();
        direction.y = 0f;

        if (direction.sqrMagnitude <= MoveInputThreshold)
        {
            direction = transform.forward;
        }

        slideDirection = direction.normalized;
        slideTimer = Mathf.Max(slideDuration, 0f);
        RPC_PlaySlide();
    }

    private void UpdateAttackDamageWindow(float deltaTime)
    {
        if (damageCaster == null || attackTimer <= 0f)
        {
            damageCaster?.DisableDamageCaster();
            return;
        }

        if (attackDamageDelayTimer > 0f)
        {
            attackDamageDelayTimer = Mathf.Max(attackDamageDelayTimer - deltaTime, 0f);

            if (attackDamageDelayTimer > 0f)
            {
                damageCaster.DisableDamageCaster();
                return;
            }
        }

        if (attackDamageTimer > 0f)
        {
            damageCaster.EnableDamageCaster();
            attackDamageTimer = Mathf.Max(attackDamageTimer - deltaTime, 0f);
            return;
        }

        damageCaster.DisableDamageCaster();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayAttack()
    {
        if (animator != null)
        {
            animator.SetFloat(SpeedParameter, 0f);
            animator.SetTrigger(AttackParameter);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlaySlide()
    {
        if (animator != null)
        {
            animator.SetTrigger(SlideParameter);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ApplyDamage(int damage, Vector3 attackPosition)
    {
        ResolveReferences();

        if (player != null)
        {
            player.ApplyDamage(damage, attackPosition);
            return;
        }

        Character character = GetComponent<Character>();
        if (character != null)
        {
            character.ApplyDamage(damage, attackPosition);
        }
    }
}
