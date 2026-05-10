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
    private const float MaxPlayerHealth = 100f;

    [SerializeField] private Button attackButton;
    [SerializeField] private Button slideButton;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TMP_Text coinText;

    private Player player;
    private Health playerHealth;
    private Player subscribedPlayer;
    private Health subscribedPlayerHealth;

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

        if (player != null)
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

        if (player != null)
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
            healthSlider.maxValue = MaxPlayerHealth;
            healthSlider.wholeNumbers = true;
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
        GameObject playerObject = GameObject.FindGameObjectWithTag(PlayerTag);

        if (playerObject != null)
        {
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

        playerHealth = player != null ? player.GetComponent<Health>() : null;
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

        healthSlider.maxValue = MaxPlayerHealth;
        healthSlider.value = Mathf.Clamp(currentHealth, 0, (int)MaxPlayerHealth);
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
