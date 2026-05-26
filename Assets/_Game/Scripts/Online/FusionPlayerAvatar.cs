using Fusion;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;
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
    [SerializeField] private Health health;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Animator animator;
    [SerializeField] private DamageCaster damageCaster;
    [SerializeField] private FusionNetworkHealth networkHealth;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 12f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedGravity = -2f;
    [SerializeField] private float moveAcceleration = 14f;
    [SerializeField] private float moveDeceleration = 100f;
    [SerializeField] private float animatorDampTime = 0.06f;
    [SerializeField] private float stopAnimatorDampTime = 0.01f;
    [SerializeField] private float attackDuration = 0.65f;
    [SerializeField] private float attackDamageDelay = 0.15f;
    [SerializeField] private float attackDamageDuration = 0.25f;
    [SerializeField] private float slideDuration = 0.5f;
    [SerializeField] private float slideDistance = 3f;
    [SerializeField] private bool setCameraFollowTarget = true;
    [Header("Revive")]
    [SerializeField] private int initialRevivesRemaining = 1;
    [SerializeField] private float reviveDistance = 2.4f;
    [SerializeField] private float reviveHealthPercent = 0.5f;
    [Header("Nameplate")]
    [SerializeField] private bool showDisplayName = true;
    [SerializeField] private float displayNameHeight = 2.25f;
    [SerializeField] private float displayNameFontSize = 3f;
    [SerializeField] private Vector3 displayNameScale = new Vector3(0.08f, 0.08f, 0.08f);
    [SerializeField] private Color displayNameColor = Color.white;
    [Header("Nameplate Health")]
    [SerializeField] private bool showNameplateHealthBar = true;
    [SerializeField] private bool showLocalNameplateHealthBar;
    [SerializeField] private bool useGameplayHealthBarStyle = true;
    [SerializeField] private float healthBarHeightOffset = -0.26f;
    [SerializeField] private Vector2 healthBarSize = new Vector2(140f, 18f);
    [SerializeField] private float healthBarWorldScale = 0.01f;
    [SerializeField] private float healthBarFillSpeed = 4f;
    [SerializeField] private Color healthBarBackgroundColor = new Color(0f, 0f, 0f, 0.65f);
    [SerializeField] private Color healthBarFillColor = new Color(0.2f, 0.95f, 0.35f, 0.95f);
    [SerializeField] private Color healthBarLowFillColor = new Color(0.95f, 0.22f, 0.16f, 0.95f);
    [SerializeField] private Color healthBarTextColor = Color.white;

    [Networked] private float NetworkedSpeed { get; set; }
    [Networked] private NetworkBool NetworkedGrounded { get; set; }
    [Networked, Capacity(32)] private NetworkString<_32> NetworkedDisplayName { get; set; }
    [Networked] public int RevivesRemaining { get; private set; }
    [Networked] public NetworkBool IsDowned { get; private set; }
    [Networked] public NetworkBool IsEliminated { get; private set; }
    [Networked] private NetworkBool ReviveStateInitialized { get; set; }

    private bool? lastLocalControlState;
    private bool cameraBound;
    private float verticalVelocity;
    private float attackTimer;
    private float attackDamageDelayTimer;
    private float attackDamageTimer;
    private float slideTimer;
    private Vector3 slideDirection;
    private Vector3 smoothedMoveDirection;
    private Vector3 initialSpawnPosition;
    private Quaternion initialSpawnRotation;
    private int initialSpawnIndex = -1;
    private int initialSpawnCorrectionTicks;
    private bool attackQueued;
    private bool slideQueued;
    private bool hasAppliedNetworkDeath;
    private int lastDamageSourceId;
    private double lastDamageTime = -999d;
    private TextMeshPro displayNameText;
    private string lastRenderedDisplayName;
    private Canvas healthBarCanvas;
    private RectTransform healthBarFillRect;
    private Image healthBarFrameImage;
    private Image healthBarBackgroundImage;
    private Image healthBarFillImage;
    private TMP_Text healthBarText;
    private float displayedHealthRatio = -1f;
    private int lastRenderedCurrentHealth = -1;
    private int lastRenderedMaxHealth = -1;

    private const double DuplicateDamageLockSeconds = 0.3d;
    private static Sprite whiteSprite;
    private static HealthBarStyle gameplayHealthBarStyle;
    private static bool gameplayHealthBarStyleLoaded;

    public bool CanApplyDamageLocally => HasLocalControl();
    public bool IsLocalPlayerAvatar => HasLocalControl();
    public float ReviveDistance => reviveDistance;
    public bool CanBeRevived => IsDowned && !IsEliminated && RevivesRemaining > 0;
    public bool CanReviveOthers => HasLocalControl() && !IsDowned && !IsEliminated && !IsDead();
    public bool IsUnableToContinueMatch
    {
        get
        {
            if (IsDowned || IsEliminated || IsDead())
            {
                return true;
            }

            return networkHealth != null && networkHealth.MaxHealth > 0 && networkHealth.CurrentHealth <= 0;
        }
    }
    public PlayerRef NetworkPlayerRef
    {
        get
        {
            if (Object == null || !Object.IsValid)
            {
                return PlayerRef.None;
            }

            return Object.InputAuthority != PlayerRef.None ? Object.InputAuthority : Object.StateAuthority;
        }
    }

    public void SetInitialSpawn(Vector3 position, Quaternion rotation, int spawnIndex)
    {
        initialSpawnPosition = position;
        initialSpawnRotation = rotation;
        initialSpawnIndex = spawnIndex;
        initialSpawnCorrectionTicks = 5;
        ApplyInitialSpawnPosition();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    public override void Spawned()
    {
        ResolveReferences();
        SubscribeHealth();
        InitializeReviveState();
        ApplyAuthorityState();
        SetLocalDisplayNameIfNeeded();
        RefreshDisplayNameView();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        UnsubscribeHealth();

        if (HasLocalControl())
        {
            gameObject.tag = "Untagged";
        }
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeHealth();

        if (Object != null && Object.IsValid)
        {
            InitializeReviveState();
            ApplyAuthorityState();
        }
    }

    private void OnDisable()
    {
        UnsubscribeHealth();
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

    private void InitializeReviveState()
    {
        if (Object == null || !Object.IsValid || !Object.HasStateAuthority || ReviveStateInitialized)
        {
            return;
        }

        ReviveStateInitialized = true;
        RevivesRemaining = Mathf.Max(0, initialRevivesRemaining);
        IsDowned = false;
        IsEliminated = false;
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

        if (health == null)
        {
            health = GetComponent<Health>();
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (damageCaster == null)
        {
            damageCaster = GetComponentInChildren<DamageCaster>(true);
        }

        if (networkHealth == null)
        {
            networkHealth = GetComponent<FusionNetworkHealth>();
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
            RefreshDisplayNameView();
        }
    }

    private void SetLocalDisplayNameIfNeeded()
    {
        if (Object == null || !Object.IsValid || !Object.HasStateAuthority)
        {
            return;
        }

        string displayName = GetLocalDisplayName();
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            NetworkedDisplayName = displayName;
        }
    }

    private string GetLocalDisplayName()
    {
        if (!string.IsNullOrWhiteSpace(SupabaseSession.DisplayName))
        {
            return SupabaseSession.DisplayName.Trim();
        }

        if (OnlineRoomSession.Players != null && !string.IsNullOrWhiteSpace(SupabaseSession.UserId))
        {
            RoomService.RoomPlayerData roomPlayer = OnlineRoomSession.Players.Find(
                playerData => playerData != null && playerData.user_id == SupabaseSession.UserId
            );

            if (roomPlayer != null && !string.IsNullOrWhiteSpace(roomPlayer.display_name))
            {
                return roomPlayer.display_name.Trim();
            }
        }

        if (!string.IsNullOrWhiteSpace(SupabaseSession.Email))
        {
            int atIndex = SupabaseSession.Email.IndexOf('@');
            return atIndex > 0 ? SupabaseSession.Email.Substring(0, atIndex) : SupabaseSession.Email;
        }

        return "Player";
    }

    private void RefreshDisplayNameView()
    {
        if (!showDisplayName)
        {
            if (displayNameText != null)
            {
                displayNameText.gameObject.SetActive(false);
            }

            return;
        }

        EnsureDisplayNameText();

        if (displayNameText == null)
        {
            return;
        }

        string displayName = NetworkedDisplayName.ToString();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = HasLocalControl() ? GetLocalDisplayName() : "Player";
        }

        if (lastRenderedDisplayName != displayName)
        {
            lastRenderedDisplayName = displayName;
            displayNameText.text = displayName;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            Transform textTransform = displayNameText.transform;
            textTransform.position = transform.position + Vector3.up * displayNameHeight;
            textTransform.rotation = Quaternion.LookRotation(textTransform.position - mainCamera.transform.position);
        }

        RefreshNameplateHealthBar(mainCamera);
    }

    private void EnsureDisplayNameText()
    {
        if (displayNameText != null)
        {
            return;
        }

        Transform existingNameplate = transform.Find("DisplayNameText");
        if (existingNameplate != null)
        {
            displayNameText = existingNameplate.GetComponent<TextMeshPro>();
        }

        if (displayNameText == null)
        {
            GameObject nameObject = new GameObject("DisplayNameText");
            nameObject.transform.SetParent(transform);
            nameObject.transform.localPosition = Vector3.up * displayNameHeight;
            nameObject.transform.localRotation = Quaternion.identity;
            nameObject.transform.localScale = displayNameScale;
            displayNameText = nameObject.AddComponent<TextMeshPro>();
        }

        displayNameText.alignment = TextAlignmentOptions.Center;
        displayNameText.fontSize = displayNameFontSize;
        displayNameText.color = displayNameColor;
        displayNameText.enableWordWrapping = false;
        displayNameText.raycastTarget = false;
        displayNameText.gameObject.SetActive(true);
    }

    private void RefreshNameplateHealthBar(Camera mainCamera)
    {
        bool shouldShow = showNameplateHealthBar && (!HasLocalControl() || showLocalNameplateHealthBar);
        if (!shouldShow)
        {
            if (healthBarCanvas != null)
            {
                healthBarCanvas.gameObject.SetActive(false);
            }

            return;
        }

        EnsureHealthBarCanvas();

        if (healthBarCanvas == null)
        {
            return;
        }

        healthBarCanvas.gameObject.SetActive(true);

        int maxHealth = Mathf.Max(1, GetNameplateMaxHealth());
        int currentHealth = Mathf.Clamp(GetNameplateCurrentHealth(maxHealth), 0, maxHealth);
        float targetRatio = Mathf.Clamp01((float)currentHealth / maxHealth);

        if (displayedHealthRatio < 0f)
        {
            displayedHealthRatio = targetRatio;
        }
        else
        {
            displayedHealthRatio = Mathf.MoveTowards(
                displayedHealthRatio,
                targetRatio,
                Mathf.Max(0.1f, healthBarFillSpeed) * Time.deltaTime
            );
        }

        if (healthBarFillRect != null)
        {
            healthBarFillRect.anchorMax = new Vector2(displayedHealthRatio, 1f);
        }

        if (healthBarFillImage != null)
        {
            if (!useGameplayHealthBarStyle)
            {
                healthBarFillImage.color = Color.Lerp(healthBarLowFillColor, healthBarFillColor, displayedHealthRatio);
            }
        }

        if (healthBarText != null
            && (lastRenderedCurrentHealth != currentHealth || lastRenderedMaxHealth != maxHealth))
        {
            lastRenderedCurrentHealth = currentHealth;
            lastRenderedMaxHealth = maxHealth;
            healthBarText.text = $"{currentHealth}/{maxHealth}";
        }

        if (mainCamera != null)
        {
            Transform canvasTransform = healthBarCanvas.transform;
            canvasTransform.position = transform.position + Vector3.up * (displayNameHeight + healthBarHeightOffset);
            canvasTransform.rotation = Quaternion.LookRotation(canvasTransform.position - mainCamera.transform.position);
        }
    }

    private int GetNameplateMaxHealth()
    {
        if (networkHealth != null && networkHealth.MaxHealth > 0)
        {
            return networkHealth.MaxHealth;
        }

        return health != null ? health.maxHealth : 100;
    }

    private int GetNameplateCurrentHealth(int maxHealth)
    {
        if (networkHealth != null && networkHealth.MaxHealth > 0)
        {
            return networkHealth.CurrentHealth;
        }

        return health != null ? health.currentHealth : maxHealth;
    }

    private void EnsureHealthBarCanvas()
    {
        if (healthBarCanvas != null)
        {
            ApplyGameplayHealthBarStyle();
            return;
        }

        Transform existingHealthBar = transform.Find("NameplateHealthBar");
        if (existingHealthBar != null)
        {
            healthBarCanvas = existingHealthBar.GetComponent<Canvas>();
            healthBarFillRect = FindChildRect(existingHealthBar, "Fill");
            healthBarFrameImage = FindChildImage(existingHealthBar, "Frame");
            healthBarBackgroundImage = FindChildImage(existingHealthBar, "Background");
            healthBarFillImage = healthBarFillRect != null ? healthBarFillRect.GetComponent<Image>() : null;
            healthBarText = existingHealthBar.GetComponentInChildren<TMP_Text>(true);
        }

        if (healthBarCanvas != null)
        {
            ApplyGameplayHealthBarStyle();
            return;
        }

        GameObject canvasObject = new GameObject("NameplateHealthBar");
        canvasObject.transform.SetParent(transform);
        canvasObject.transform.localPosition = Vector3.up * (displayNameHeight + healthBarHeightOffset);
        canvasObject.transform.localRotation = Quaternion.identity;
        canvasObject.transform.localScale = Vector3.one * healthBarWorldScale;

        healthBarCanvas = canvasObject.AddComponent<Canvas>();
        healthBarCanvas.renderMode = RenderMode.WorldSpace;
        healthBarCanvas.sortingOrder = short.MaxValue - 1;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = healthBarSize;

        GameObject backgroundObject = new GameObject("Background");
        backgroundObject.transform.SetParent(canvasObject.transform, false);
        RectTransform backgroundRect = backgroundObject.AddComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        healthBarBackgroundImage = backgroundObject.AddComponent<Image>();
        healthBarBackgroundImage.sprite = GetWhiteSprite();
        healthBarBackgroundImage.color = healthBarBackgroundColor;
        healthBarBackgroundImage.raycastTarget = false;

        GameObject fillObject = new GameObject("Fill");
        fillObject.transform.SetParent(backgroundObject.transform, false);
        healthBarFillRect = fillObject.AddComponent<RectTransform>();
        healthBarFillRect.anchorMin = Vector2.zero;
        healthBarFillRect.anchorMax = Vector2.one;
        healthBarFillRect.offsetMin = Vector2.zero;
        healthBarFillRect.offsetMax = Vector2.zero;
        healthBarFillImage = fillObject.AddComponent<Image>();
        healthBarFillImage.sprite = GetWhiteSprite();
        healthBarFillImage.color = healthBarFillColor;
        healthBarFillImage.raycastTarget = false;

        GameObject frameObject = new GameObject("Frame");
        frameObject.transform.SetParent(canvasObject.transform, false);
        RectTransform frameRect = frameObject.AddComponent<RectTransform>();
        frameRect.anchorMin = Vector2.zero;
        frameRect.anchorMax = Vector2.one;
        frameRect.offsetMin = Vector2.zero;
        frameRect.offsetMax = Vector2.zero;
        healthBarFrameImage = frameObject.AddComponent<Image>();
        healthBarFrameImage.sprite = GetWhiteSprite();
        healthBarFrameImage.color = Color.clear;
        healthBarFrameImage.raycastTarget = false;

        GameObject textObject = new GameObject("HealthText");
        textObject.transform.SetParent(canvasObject.transform, false);
        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        healthBarText = textObject.AddComponent<TextMeshProUGUI>();
        healthBarText.alignment = TextAlignmentOptions.Center;
        healthBarText.fontSize = 11f;
        healthBarText.color = healthBarTextColor;
        healthBarText.enableWordWrapping = false;
        healthBarText.raycastTarget = false;

        ApplyGameplayHealthBarStyle();
    }

    private static RectTransform FindChildRect(Transform root, string childName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
            {
                return child.GetComponent<RectTransform>();
            }
        }

        return null;
    }

    private static Image FindChildImage(Transform root, string childName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
            {
                return child.GetComponent<Image>();
            }
        }

        return null;
    }

    private void ApplyGameplayHealthBarStyle()
    {
        if (!useGameplayHealthBarStyle)
        {
            return;
        }

        HealthBarStyle style = GetGameplayHealthBarStyle();
        if (!style.IsValid)
        {
            return;
        }

        ApplyImageStyle(healthBarFrameImage, style.frame);
        ApplyImageStyle(healthBarBackgroundImage, style.background);
        ApplyImageStyle(healthBarFillImage, style.fill);
    }

    private static HealthBarStyle GetGameplayHealthBarStyle()
    {
        if (gameplayHealthBarStyleLoaded)
        {
            return gameplayHealthBarStyle;
        }

        gameplayHealthBarStyleLoaded = true;
        PanelGamePlay gameplayPanelPrefab = Resources.Load<PanelGamePlay>("UI/Panel - GamePlay");
        if (gameplayPanelPrefab == null)
        {
            return gameplayHealthBarStyle;
        }

        Transform healthSliderTransform = FindChildTransform(gameplayPanelPrefab.transform, "Health Slider");
        if (healthSliderTransform == null)
        {
            return gameplayHealthBarStyle;
        }

        Slider slider = healthSliderTransform.GetComponent<Slider>();
        Image frameImage = healthSliderTransform.GetComponent<Image>();
        Image backgroundImage = FindChildImage(healthSliderTransform, "Background");
        Image fillImage = slider != null && slider.fillRect != null
            ? slider.fillRect.GetComponent<Image>()
            : FindChildImage(healthSliderTransform, "Fill");

        gameplayHealthBarStyle = new HealthBarStyle
        {
            frame = ImageStyle.FromImage(frameImage),
            background = ImageStyle.FromImage(backgroundImage),
            fill = ImageStyle.FromImage(fillImage)
        };

        return gameplayHealthBarStyle;
    }

    private static Transform FindChildTransform(Transform root, string childName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
            {
                return child;
            }
        }

        return null;
    }

    private static void ApplyImageStyle(Image target, ImageStyle style)
    {
        if (target == null || !style.IsValid)
        {
            return;
        }

        target.sprite = style.sprite;
        target.color = style.color;
        target.type = style.type;
        target.preserveAspect = style.preserveAspect;
        target.fillCenter = style.fillCenter;
        target.pixelsPerUnitMultiplier = style.pixelsPerUnitMultiplier;
    }

    private struct HealthBarStyle
    {
        public ImageStyle frame;
        public ImageStyle background;
        public ImageStyle fill;

        public bool IsValid => frame.IsValid || background.IsValid || fill.IsValid;
    }

    private struct ImageStyle
    {
        public Sprite sprite;
        public Color color;
        public Image.Type type;
        public bool preserveAspect;
        public bool fillCenter;
        public float pixelsPerUnitMultiplier;

        public bool IsValid => sprite != null;

        public static ImageStyle FromImage(Image image)
        {
            if (image == null)
            {
                return default;
            }

            return new ImageStyle
            {
                sprite = image.sprite,
                color = image.color,
                type = image.type,
                preserveAspect = image.preserveAspect,
                fillCenter = image.fillCenter,
                pixelsPerUnitMultiplier = image.pixelsPerUnitMultiplier
            };
        }
    }

    private static Sprite GetWhiteSprite()
    {
        if (whiteSprite != null)
        {
            return whiteSprite;
        }

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
        whiteSprite.name = "Runtime White Sprite";
        return whiteSprite;
    }

    private void Update()
    {
        if (IsDead())
        {
            ClearLocalActions();
            return;
        }

        QueueKeyboardActions();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasLocalControl() || characterController == null || !characterController.enabled)
        {
            return;
        }

        if (IsDead())
        {
            StopLocalControlAfterDeath();
            return;
        }

        if (initialSpawnCorrectionTicks > 0)
        {
            initialSpawnCorrectionTicks--;
            ApplyInitialSpawnPosition();

            if (initialSpawnCorrectionTicks > 0)
            {
                return;
            }
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

        float dampTime = NetworkedSpeed > MoveInputThreshold ? animatorDampTime : stopAnimatorDampTime;
        animator.SetFloat(SpeedParameter, NetworkedSpeed, dampTime, Time.deltaTime);
        animator.SetBool(IsGroundedParameter, NetworkedGrounded);
    }

    public void RequestAttack()
    {
        if (!HasLocalControl() || IsDead())
        {
            return;
        }

        attackQueued = true;
    }

    public void RequestSlide()
    {
        if (!HasLocalControl() || IsDead())
        {
            return;
        }

        slideQueued = true;
    }

    public bool RequestDamage(int damage, Vector3 attackPosition, int damageSourceId = 0)
    {
        if (damage <= 0 || Object == null || !Object.IsValid)
        {
            return false;
        }

        RPC_ApplyDamage(damage, attackPosition, damageSourceId);
        return true;
    }

    public void RequestPickup(PickUpType pickupType, int value)
    {
        if (value <= 0 || Object == null || !Object.IsValid)
        {
            return;
        }

        RPC_ApplyPickup((int)pickupType, value);
    }

    public void RequestReviveTarget(FusionPlayerAvatar target)
    {
        if (target == null || !CanReviveOthers)
        {
            return;
        }

        target.RPC_RequestRevive(NetworkPlayerRef);
    }

    public bool BroadcastMatchResult(GameState resultState)
    {
        if (resultState != GameState.Victory && resultState != GameState.Lose)
        {
            return false;
        }

        if (Object == null || !Object.IsValid)
        {
            return false;
        }

        RPC_ApplyMatchResult((int)resultState);
        return true;
    }

    private void MoveLocalPlayer(float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            return;
        }

        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = groundedGravity;
        }

        verticalVelocity += gravity * deltaTime;

        Vector3 targetMoveDirection = slideTimer > 0f ? slideDirection : GetMoveInput();
        targetMoveDirection.y = 0f;
        targetMoveDirection = Vector3.ClampMagnitude(targetMoveDirection, 1f);

        float currentMoveSpeed = moveSpeed;
        if (attackTimer > 0f)
        {
            targetMoveDirection = Vector3.zero;
        }
        else if (slideTimer > 0f)
        {
            float duration = Mathf.Max(slideDuration, 0.01f);
            currentMoveSpeed = slideDistance / duration;
            slideTimer = Mathf.Max(slideTimer - deltaTime, 0f);
        }

        float acceleration = targetMoveDirection.sqrMagnitude > MoveInputThreshold
            ? moveAcceleration
            : moveDeceleration;

        if (slideTimer > 0f)
        {
            smoothedMoveDirection = targetMoveDirection;
        }
        else if (targetMoveDirection.sqrMagnitude <= MoveInputThreshold)
        {
            smoothedMoveDirection = Vector3.zero;
        }
        else
        {
            smoothedMoveDirection = Vector3.MoveTowards(
                smoothedMoveDirection,
                targetMoveDirection,
                acceleration * deltaTime
            );
        }

        bool hasMoveInput = smoothedMoveDirection.sqrMagnitude > MoveInputThreshold;
        Vector3 movement = smoothedMoveDirection * currentMoveSpeed;
        movement.y = verticalVelocity;
        characterController.Move(movement * deltaTime);

        if (hasMoveInput)
        {
            Quaternion targetRotation = Quaternion.LookRotation(smoothedMoveDirection.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * deltaTime);
        }

        float speed = hasMoveInput ? Mathf.Clamp01(smoothedMoveDirection.magnitude) : 0f;
        NetworkedSpeed = speed;
        NetworkedGrounded = characterController.isGrounded;

        if (animator != null)
        {
            float dampTime = speed > MoveInputThreshold ? animatorDampTime : stopAnimatorDampTime;
            animator.SetFloat(SpeedParameter, speed, dampTime, deltaTime);
            animator.SetBool(IsGroundedParameter, characterController.isGrounded);
        }

        if (attackTimer > 0f)
        {
            attackTimer = Mathf.Max(attackTimer - deltaTime, 0f);
        }
    }

    private void ApplyInitialSpawnPosition()
    {
        if (initialSpawnIndex < 0 || !HasLocalControl())
        {
            return;
        }

        bool controllerWasEnabled = characterController != null && characterController.enabled;
        if (characterController != null)
        {
            characterController.enabled = false;
        }

        transform.SetPositionAndRotation(initialSpawnPosition, initialSpawnRotation);
        verticalVelocity = 0f;
        smoothedMoveDirection = Vector3.zero;

        if (characterController != null)
        {
            characterController.enabled = controllerWasEnabled;
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
        if (!HasLocalControl() || IsDead())
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
        if (IsDead() || attackTimer > 0f || slideTimer > 0f)
        {
            return;
        }

        attackTimer = Mathf.Max(attackDuration, 0f);
        attackDamageDelayTimer = Mathf.Max(attackDamageDelay, 0f);
        attackDamageTimer = Mathf.Max(attackDamageDuration, 0f);
        damageCaster?.EndControlledDamageWindow();
        RPC_PlayAttack();
    }

    private void TryStartSlide()
    {
        if (IsDead() || attackTimer > 0f || slideTimer > 0f)
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
        if (damageCaster == null || attackTimer <= 0f || IsDead())
        {
            damageCaster?.EndControlledDamageWindow();
            return;
        }

        if (attackDamageDelayTimer > 0f)
        {
            attackDamageDelayTimer = Mathf.Max(attackDamageDelayTimer - deltaTime, 0f);

            if (attackDamageDelayTimer > 0f)
            {
                damageCaster.EndControlledDamageWindow();
                return;
            }
        }

        if (attackDamageTimer > 0f)
        {
            damageCaster.BeginControlledDamageWindow();
            attackDamageTimer = Mathf.Max(attackDamageTimer - deltaTime, 0f);
            return;
        }

        damageCaster.EndControlledDamageWindow();
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
    private void RPC_ApplyDamage(int damage, Vector3 attackPosition, int damageSourceId)
    {
        ResolveReferences();

        double now = Runner != null ? Runner.SimulationTime : Time.timeAsDouble;
        if (damageSourceId != 0
            && damageSourceId == lastDamageSourceId
            && now - lastDamageTime < DuplicateDamageLockSeconds)
        {
            Debug.Log(
                $"FusionPlayerAvatar: ignored duplicate damage from source {damageSourceId} " +
                $"on '{name}' within {DuplicateDamageLockSeconds:0.00}s."
            );
            return;
        }

        lastDamageSourceId = damageSourceId;
        lastDamageTime = now;

        if (player != null)
        {
            player.ApplyDamage(damage, attackPosition);
            networkHealth?.ForceSyncNow();
            return;
        }

        Character character = GetComponent<Character>();
        if (character != null)
        {
            character.ApplyDamage(damage, attackPosition);
            networkHealth?.ForceSyncNow();
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ApplyPickup(int pickupType, int value)
    {
        ResolveReferences();

        Character character = player != null ? player : GetComponent<Character>();
        if (character == null)
        {
            return;
        }

        character.ApplyPickupValue((PickUpType)pickupType, value);
        networkHealth?.ForceSyncNow();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestRevive(PlayerRef reviver)
    {
        ResolveReferences();

        if (!CanBeRevived || reviver == PlayerRef.None || Runner == null)
        {
            return;
        }

        if (!Runner.TryGetPlayerObject(reviver, out NetworkObject reviverObject) || reviverObject == null)
        {
            return;
        }

        FusionPlayerAvatar reviverAvatar = reviverObject.GetComponent<FusionPlayerAvatar>();
        if (reviverAvatar == null
            || reviverAvatar == this
            || reviverAvatar.IsDowned
            || reviverAvatar.IsEliminated
            || reviverAvatar.IsDead())
        {
            return;
        }

        float allowedDistance = Mathf.Max(0.1f, reviveDistance) + 0.35f;
        Vector3 offset = reviverAvatar.transform.position - transform.position;
        offset.y = 0f;

        if (offset.sqrMagnitude > allowedDistance * allowedDistance)
        {
            return;
        }

        int maxHealth = health != null ? Mathf.Max(1, health.maxHealth) : 100;
        int reviveHealth = Mathf.Clamp(Mathf.RoundToInt(maxHealth * reviveHealthPercent), 1, maxHealth);

        RevivesRemaining = Mathf.Max(RevivesRemaining - 1, 0);
        IsDowned = false;
        IsEliminated = false;

        if (health != null)
        {
            health.SetHealthFromNetwork(reviveHealth, maxHealth);
        }

        networkHealth?.ForceSyncNow();
        RPC_ApplyRevive(reviveHealth);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ApplyRevive(int reviveHealth)
    {
        ApplyNetworkRevive(reviveHealth);
    }

    public bool BroadcastSpawnerCleared(int spawnerNetworkId)
    {
        if (Object == null || !Object.IsValid)
        {
            return false;
        }

        RPC_OpenSpawnerGates(spawnerNetworkId);
        return true;
    }

    public bool BroadcastSpawnerSpawnRequested(int spawnerNetworkId)
    {
        if (Object == null || !Object.IsValid)
        {
            return false;
        }

        RPC_RequestSpawnerSpawn(spawnerNetworkId, NetworkPlayerRef);
        return true;
    }

    public bool RequestSpawnerSpawnOnStateAuthority(int spawnerNetworkId)
    {
        if (Object == null || !Object.IsValid)
        {
            return false;
        }

        RPC_RequestSpawnerSpawnOnStateAuthority(spawnerNetworkId);
        return true;
    }

    public void BroadcastPickupCollected(int pickupNetworkId, Vector3 collectPosition)
    {
        if (Object == null || !Object.IsValid)
        {
            return;
        }

        RPC_CollectLocalPickup(pickupNetworkId, collectPosition);
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RPC_OpenSpawnerGates(int spawnerNetworkId)
    {
        Spawner.OpenGatesForNetworkId(spawnerNetworkId);
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RPC_RequestSpawnerSpawn(int spawnerNetworkId, PlayerRef activatingPlayer)
    {
        Spawner.SpawnForNetworkId(spawnerNetworkId, activatingPlayer);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestSpawnerSpawnOnStateAuthority(int spawnerNetworkId)
    {
        Spawner.SpawnForNetworkId(spawnerNetworkId, NetworkPlayerRef);
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RPC_CollectLocalPickup(int pickupNetworkId, Vector3 collectPosition)
    {
        PickUp.CollectLocalPickupForNetworkId(pickupNetworkId, collectPosition);
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RPC_ApplyMatchResult(int resultStateValue)
    {
        GameState resultState = (GameState)resultStateValue;
        NetworkMatchManager.Ensure().ApplyNetworkResult(resultState);
    }

    private void SubscribeHealth()
    {
        if (health == null)
        {
            return;
        }

        health.HealthChanged -= OnHealthChanged;
        health.HealthChanged += OnHealthChanged;
    }

    private void UnsubscribeHealth()
    {
        if (health != null)
        {
            health.HealthChanged -= OnHealthChanged;
        }
    }

    private void OnHealthChanged(int current, int max)
    {
        if (current > 0 || hasAppliedNetworkDeath)
        {
            return;
        }

        ApplyNetworkDeath();
    }

    private bool IsDead()
    {
        return health != null && health.IsDead;
    }

    private void ApplyNetworkDeath()
    {
        if (hasAppliedNetworkDeath)
        {
            return;
        }

        hasAppliedNetworkDeath = true;
        StopLocalControlAfterDeath();

        bool canBeRevivedAfterThisDeath = RevivesRemaining > 0 && !IsEliminated;

        if (Object != null && Object.IsValid && Object.HasStateAuthority)
        {
            if (canBeRevivedAfterThisDeath)
            {
                IsDowned = true;
                IsEliminated = false;
            }
            else
            {
                IsDowned = false;
                IsEliminated = true;
            }
        }

        Character character = player != null ? player : GetComponent<Character>();
        if (character != null && character.CurrentState != CharacterState.Dead)
        {
            if (canBeRevivedAfterThisDeath)
            {
                character.SuppressNextDeathDissolve();
            }

            character.SwitchToState(CharacterState.Dead, true);
        }
    }

    private void ApplyNetworkRevive(int reviveHealth)
    {
        ResolveReferences();
        hasAppliedNetworkDeath = false;
        lastDamageSourceId = 0;
        lastDamageTime = -999d;
        ClearLocalActions();
        smoothedMoveDirection = Vector3.zero;
        slideDirection = Vector3.zero;
        verticalVelocity = 0f;
        NetworkedSpeed = 0f;
        NetworkedGrounded = true;

        Character character = player != null ? player : GetComponent<Character>();
        if (character != null)
        {
            character.Revive(reviveHealth);
        }
        else if (health != null)
        {
            int maxHealth = Mathf.Max(1, health.maxHealth);
            health.SetHealthFromNetwork(Mathf.Clamp(reviveHealth, 1, maxHealth), maxHealth);
        }
    }

    private void StopLocalControlAfterDeath()
    {
        ClearLocalActions();
        smoothedMoveDirection = Vector3.zero;
        slideDirection = Vector3.zero;
        verticalVelocity = 0f;
        NetworkedSpeed = 0f;

        damageCaster?.EndControlledDamageWindow();

        if (animator != null)
        {
            animator.SetFloat(SpeedParameter, 0f);
        }
    }

    private void ClearLocalActions()
    {
        attackQueued = false;
        slideQueued = false;
        attackTimer = 0f;
        attackDamageDelayTimer = 0f;
        attackDamageTimer = 0f;
        slideTimer = 0f;
    }
}
