using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.UI;

public class PanelGamePlay : UICanvas
{
    private const string PlayerTag = "Player";
    private const float PlayerSearchInterval = 0.25f;

    [SerializeField] private Button attackButton;
    [SerializeField] private Button slideButton;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TMP_Text coinText;

    private Player player;
    private FusionPlayerAvatar fusionPlayerAvatar;
    private Health playerHealth;
    private Player subscribedPlayer;
    private Health subscribedPlayerHealth;
    private float nextPlayerSearchTime;

    private void OnEnable()
    {
        EnsureEventSystem();
        BindUIReferences();

        if (attackButton == null)
        {
            attackButton = FindButtonByName("Skill1") ?? GetComponentInChildren<Button>(true);
        }

        if (slideButton == null)
        {
            slideButton = FindButtonByName("Skill2 - Slide");
        }

        CachePlayer();
        SubscribePlayerStats();
        RefreshPlayerStats();

        if (attackButton != null)
        {
            attackButton.onClick.RemoveListener(OnAttackButtonClicked);
            attackButton.onClick.AddListener(OnAttackButtonClicked);
        }

        if (slideButton != null)
        {
            slideButton.onClick.RemoveListener(OnSlideButtonClicked);
            slideButton.onClick.AddListener(OnSlideButtonClicked);
        }
    }

    private void OnDisable()
    {
        UnsubscribePlayerStats();

        if (attackButton != null)
        {
            attackButton.onClick.RemoveListener(OnAttackButtonClicked);
        }

        if (slideButton != null)
        {
            slideButton.onClick.RemoveListener(OnSlideButtonClicked);
        }
    }

    private void Update()
    {
        if (Time.unscaledTime < nextPlayerSearchTime)
        {
            return;
        }

        nextPlayerSearchTime = Time.unscaledTime + PlayerSearchInterval;

        if (playerHealth == null || subscribedPlayerHealth == null || fusionPlayerAvatar == null)
        {
            CachePlayer();
            SubscribePlayerStats();
            RefreshPlayerStats();
        }
    }

    public override void SetUp()
    {
        base.SetUp();
        BindUIReferences();
        CachePlayer();
        SubscribePlayerStats();
        RefreshPlayerStats();
    }

    public void OnAttackButtonClicked()
    {
        if (player == null)
        {
            CachePlayer();
        }

        if (fusionPlayerAvatar != null)
        {
            fusionPlayerAvatar.RequestAttack();
        }
        else if (player != null)
        {
            player.Attack();
        }
    }

    public void OnSlideButtonClicked()
    {
        if (player == null)
        {
            CachePlayer();
        }

        if (fusionPlayerAvatar != null)
        {
            fusionPlayerAvatar.RequestSlide();
        }
        else if (player != null)
        {
            player.Slide();
        }
    }

    private Button FindButtonByName(string buttonName)
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);

        foreach (Button button in buttons)
        {
            if (button != null && button.gameObject.name == buttonName)
            {
                return button;
            }
        }

        return null;
    }

    private void BindUIReferences()
    {
        if (healthSlider == null)
        {
            GameObject healthSliderObject = FindChildByName("Health Slider");
            healthSlider = healthSliderObject != null
                ? healthSliderObject.GetComponent<Slider>()
                : GetComponentInChildren<Slider>(true);
        }

        if (healthSlider != null)
        {
            healthSlider.minValue = 0f;
            healthSlider.maxValue = 100f;
            healthSlider.wholeNumbers = true;
            EnsureHealthFillVisible();
        }

        if (coinText == null)
        {
            GameObject coinTextObject = FindChildByName("CoinText");
            coinText = coinTextObject != null
                ? coinTextObject.GetComponent<TMP_Text>()
                : GetComponentInChildren<TMP_Text>(true);
        }
    }

    private GameObject FindChildByName(string childName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);

        foreach (Transform child in children)
        {
            if (child != null && child.gameObject.name == childName)
            {
                return child.gameObject;
            }
        }

        return null;
    }

    private void CachePlayer()
    {
        FusionPlayerAvatar localAvatar = FindLocalFusionPlayerAvatar();
        if (localAvatar != null)
        {
            fusionPlayerAvatar = localAvatar;
            player = fusionPlayerAvatar.GetComponent<Player>();
            playerHealth = fusionPlayerAvatar.GetComponent<Health>();
            return;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag(PlayerTag);

        if (playerObject != null)
        {
            fusionPlayerAvatar = playerObject.GetComponent<FusionPlayerAvatar>();

            if (fusionPlayerAvatar == null)
            {
                fusionPlayerAvatar = playerObject.GetComponentInParent<FusionPlayerAvatar>();
            }

            player = playerObject.GetComponent<Player>();

            if (player == null)
            {
                player = playerObject.GetComponentInParent<Player>();
            }
        }

        if (player == null)
        {
            player = FindFirstObjectByType<Player>();
        }

        if (fusionPlayerAvatar == null)
        {
            fusionPlayerAvatar = FindLocalFusionPlayerAvatar();
        }

        if (fusionPlayerAvatar != null)
        {
            playerHealth = fusionPlayerAvatar.GetComponent<Health>();
        }
        else
        {
            playerHealth = player != null ? player.GetComponent<Health>() : null;
        }
    }

    private FusionPlayerAvatar FindLocalFusionPlayerAvatar()
    {
        FusionPlayerAvatar[] avatars = FindObjectsByType<FusionPlayerAvatar>(FindObjectsSortMode.None);

        foreach (FusionPlayerAvatar avatar in avatars)
        {
            if (avatar != null && avatar.IsLocalPlayerAvatar)
            {
                return avatar;
            }
        }

        return null;
    }

    private void SubscribePlayerStats()
    {
        UnsubscribePlayerStats();

        if (playerHealth != null)
        {
            playerHealth.HealthChanged += OnPlayerHealthChanged;
            subscribedPlayerHealth = playerHealth;
        }

        if (player != null)
        {
            player.CoinChanged += OnPlayerCoinChanged;
            subscribedPlayer = player;
        }
    }

    private void UnsubscribePlayerStats()
    {
        if (subscribedPlayerHealth != null)
        {
            subscribedPlayerHealth.HealthChanged -= OnPlayerHealthChanged;
            subscribedPlayerHealth = null;
        }

        if (subscribedPlayer != null)
        {
            subscribedPlayer.CoinChanged -= OnPlayerCoinChanged;
            subscribedPlayer = null;
        }
    }

    private void RefreshPlayerStats()
    {
        if (playerHealth != null)
        {
            OnPlayerHealthChanged(playerHealth.currentHealth, playerHealth.maxHealth);
        }

        if (player != null)
        {
            OnPlayerCoinChanged(player.CoinAmount);
        }
    }

    private void OnPlayerHealthChanged(int currentHealth, int maxHealth)
    {
        if (healthSlider == null)
        {
            return;
        }

        int safeMaxHealth = Mathf.Max(1, maxHealth);
        healthSlider.minValue = 0f;
        healthSlider.maxValue = safeMaxHealth;
        healthSlider.SetValueWithoutNotify(Mathf.Clamp(currentHealth, 0, safeMaxHealth));
        EnsureHealthFillVisible();
    }

    private void EnsureHealthFillVisible()
    {
        if (healthSlider == null || healthSlider.fillRect == null)
        {
            return;
        }

        healthSlider.fillRect.gameObject.SetActive(true);
        Image fillImage = healthSlider.fillRect.GetComponent<Image>();

        if (fillImage == null)
        {
            return;
        }

        Color fillColor = fillImage.color;
        if (fillColor.a <= 0.01f)
        {
            fillColor.a = 1f;
            fillImage.color = fillColor;
        }
    }

    private void OnPlayerCoinChanged(int coin)
    {
        if (coinText != null)
        {
            coinText.text = coin.ToString();
        }
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();

#if ENABLE_INPUT_SYSTEM
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
#else
        eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
    }
}
