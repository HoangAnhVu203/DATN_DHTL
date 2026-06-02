using System;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.UI;

public class PanelGamePlay : UICanvas
{
    private const string PlayerTag = "Player";
    private const float PlayerSearchInterval = 0.25f;

    [SerializeField] private Button attackButton;
    [SerializeField] private Button slideButton;
    [SerializeField] private Button reviveButton;
    [SerializeField] private Image reviveProgressImage;
    [SerializeField] private Text reviveButtonText;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TMP_Text coinText;
    [SerializeField] private Text matchTimerText;
    [SerializeField] private float reviveHoldDuration = 3f;

    private Player player;
    private FusionPlayerAvatar fusionPlayerAvatar;
    private FusionPlayerAvatar currentReviveTarget;
    private Health playerHealth;
    private Player subscribedPlayer;
    private Health subscribedPlayerHealth;
    private float nextPlayerSearchTime;
    private float reviveHoldTimer;
    private bool reviveButtonHeld;
    private bool reviveRequestSent;
    private bool hasObservedGameplayCoin;
    private int lastObservedGameplayCoin;
    private bool isSavingCoin;
    private int requestedCoinSaveTotal = -1;

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

        if (reviveButton == null)
        {
            reviveButton = FindButtonByName("ReviveBtn") ?? FindButtonByName("ReviveButton");
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

        BindReviveButtonEvents();
        SetReviveButtonVisible(false);
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

        reviveButtonHeld = false;
        reviveRequestSent = false;
    }

    private void Update()
    {
        UpdateMatchTimer();
        UpdateReviveButton(Time.unscaledDeltaTime);

        if (Time.unscaledTime >= nextPlayerSearchTime)
        {
            nextPlayerSearchTime = Time.unscaledTime + PlayerSearchInterval;

            if (playerHealth == null || subscribedPlayerHealth == null || fusionPlayerAvatar == null)
            {
                CachePlayer();
                SubscribePlayerStats();
                RefreshPlayerStats();
            }
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

        if (matchTimerText == null)
        {
            GameObject timerTextObject = FindChildByName("MatchTimerText");
            matchTimerText = timerTextObject != null
                ? timerTextObject.GetComponent<Text>()
                : null;
        }

        if (reviveButton == null)
        {
            reviveButton = FindButtonByName("ReviveBtn") ?? FindButtonByName("ReviveButton");
        }

        if (reviveButton != null)
        {
            if (reviveProgressImage == null)
            {
                Image[] images = reviveButton.GetComponentsInChildren<Image>(true);
                foreach (Image image in images)
                {
                    if (image != null && image.type == Image.Type.Filled)
                    {
                        reviveProgressImage = image;
                        break;
                    }
                }
            }

            if (reviveButtonText == null)
            {
                reviveButtonText = reviveButton.GetComponentInChildren<Text>(true);
            }
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
            lastObservedGameplayCoin = player.CoinAmount;
            hasObservedGameplayCoin = true;
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
        UpdateMatchTimer();

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

    private void UpdateMatchTimer()
    {
        if (matchTimerText == null)
        {
            return;
        }

        int elapsedSeconds = OnlineMatchStats.GetMatchElapsedSeconds();
        int minutes = elapsedSeconds / 60;
        int seconds = elapsedSeconds % 60;
        matchTimerText.text = $"{minutes:00}:{seconds:00}";
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

        if (!hasObservedGameplayCoin)
        {
            lastObservedGameplayCoin = coin;
            hasObservedGameplayCoin = true;
            return;
        }

        int collectedCoin = coin - lastObservedGameplayCoin;
        lastObservedGameplayCoin = coin;

        if (collectedCoin <= 0 || !CanSaveCollectedCoin())
        {
            return;
        }

        SupabaseSession.AddCoin(collectedCoin);
        QueueCoinSave(SupabaseSession.Coin);
    }

    private bool CanSaveCollectedCoin()
    {
        if (!SupabaseSession.IsLoggedIn || string.IsNullOrWhiteSpace(SupabaseSession.UserId))
        {
            return false;
        }

        return fusionPlayerAvatar == null || fusionPlayerAvatar.IsLocalPlayerAvatar;
    }

    private void QueueCoinSave(int totalCoin)
    {
        requestedCoinSaveTotal = Mathf.Max(0, totalCoin);

        if (!isSavingCoin)
        {
            StartCoroutine(SaveCoinRoutine());
        }
    }

    private IEnumerator SaveCoinRoutine()
    {
        isSavingCoin = true;

        while (requestedCoinSaveTotal >= 0)
        {
            int totalToSave = requestedCoinSaveTotal;
            requestedCoinSaveTotal = -1;

            yield return SaveCoinTotal(totalToSave);
        }

        isSavingCoin = false;
    }

    private IEnumerator SaveCoinTotal(int totalCoin)
    {
        AuthService authService = FindFirstObjectByType<AuthService>();
        SupabaseConfig config = authService != null ? authService.Config : SupabaseSession.Config;

        if (config == null
            || string.IsNullOrWhiteSpace(config.SupabaseUrl)
            || string.IsNullOrWhiteSpace(config.AnonKey)
            || string.IsNullOrWhiteSpace(SupabaseSession.AccessToken)
            || string.IsNullOrWhiteSpace(SupabaseSession.UserId))
        {
            Debug.LogWarning("PanelGamePlay: cannot save coin because Supabase config/session is missing.");
            yield break;
        }

        string userId = Uri.EscapeDataString(SupabaseSession.UserId);
        string url = $"{config.SupabaseUrl}/rest/v1/users?id=eq.{userId}";
        string jsonBody = JsonUtility.ToJson(new UpdateCoinRequest
        {
            coin = totalCoin,
            updated_at = DateTime.UtcNow.ToString("O")
        });

        using UnityWebRequest request = new UnityWebRequest(url, "PATCH");
        byte[] body = Encoding.UTF8.GetBytes(jsonBody);
        request.uploadHandler = new UploadHandlerRaw(body);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("apikey", config.AnonKey);
        request.SetRequestHeader("Authorization", $"Bearer {SupabaseSession.AccessToken}");
        request.SetRequestHeader("Accept", "application/json");

        yield return request.SendWebRequest();

        string responseText = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
        if (request.responseCode < 200 || request.responseCode >= 300 || request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"PanelGamePlay: save coin failed. HTTP {request.responseCode}: {(string.IsNullOrWhiteSpace(responseText) ? request.error : responseText)}");
            requestedCoinSaveTotal = Mathf.Max(requestedCoinSaveTotal, totalCoin);
            yield return new WaitForSecondsRealtime(2f);
            yield break;
        }

        Debug.Log($"PanelGamePlay: saved total coin {totalCoin} for user {SupabaseSession.UserId}.");
    }

    [Serializable]
    private class UpdateCoinRequest
    {
        public int coin;
        public string updated_at;
    }

    private void BindReviveButtonEvents()
    {
        if (reviveButton == null)
        {
            return;
        }

        EventTrigger trigger = reviveButton.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = reviveButton.gameObject.AddComponent<EventTrigger>();
        }

        trigger.triggers.Clear();

        EventTrigger.Entry pointerDown = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerDown
        };
        pointerDown.callback.AddListener(_ => OnRevivePointerDown());
        trigger.triggers.Add(pointerDown);

        EventTrigger.Entry pointerUp = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerUp
        };
        pointerUp.callback.AddListener(_ => OnRevivePointerUp());
        trigger.triggers.Add(pointerUp);

        EventTrigger.Entry pointerExit = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerExit
        };
        pointerExit.callback.AddListener(_ => OnRevivePointerUp());
        trigger.triggers.Add(pointerExit);
    }

    private void OnRevivePointerDown()
    {
        reviveButtonHeld = true;
    }

    private void OnRevivePointerUp()
    {
        reviveButtonHeld = false;
    }

    private void UpdateReviveButton(float deltaTime)
    {
        if (fusionPlayerAvatar == null)
        {
            CachePlayer();
        }

        FusionPlayerAvatar reviveTarget = FindNearestReviveTarget();
        bool canShowRevive = reviveTarget != null;
        SetReviveButtonVisible(canShowRevive);

        if (!canShowRevive)
        {
            currentReviveTarget = null;
            ResetReviveHold();
            return;
        }

        if (currentReviveTarget != reviveTarget)
        {
            currentReviveTarget = reviveTarget;
            ResetReviveHold();
        }

        bool isHolding = reviveButtonHeld || IsReviveKeyboardHeld();
        if (!isHolding)
        {
            reviveRequestSent = false;
            reviveHoldTimer = 0f;
            SetReviveProgress(0f);
            return;
        }

        if (reviveRequestSent)
        {
            SetReviveProgress(1f);
            return;
        }

        float safeDuration = Mathf.Max(0.1f, reviveHoldDuration);
        reviveHoldTimer = Mathf.Min(reviveHoldTimer + deltaTime, safeDuration);
        float progress = Mathf.Clamp01(reviveHoldTimer / safeDuration);
        SetReviveProgress(progress);

        if (progress < 1f)
        {
            return;
        }

        reviveRequestSent = true;
        fusionPlayerAvatar.RequestReviveTarget(currentReviveTarget);
    }

    private FusionPlayerAvatar FindNearestReviveTarget()
    {
        if (fusionPlayerAvatar == null || !fusionPlayerAvatar.CanReviveOthers)
        {
            return null;
        }

        FusionPlayerAvatar[] avatars = FindObjectsByType<FusionPlayerAvatar>(FindObjectsSortMode.None);
        FusionPlayerAvatar closest = null;
        float reviveDistance = Mathf.Max(0.1f, fusionPlayerAvatar.ReviveDistance);
        float closestSqrDistance = reviveDistance * reviveDistance;

        foreach (FusionPlayerAvatar avatar in avatars)
        {
            if (avatar == null || avatar == fusionPlayerAvatar || !avatar.CanBeRevived)
            {
                continue;
            }

            float sqrDistance = (avatar.transform.position - fusionPlayerAvatar.transform.position).sqrMagnitude;
            if (sqrDistance > closestSqrDistance)
            {
                continue;
            }

            closestSqrDistance = sqrDistance;
            closest = avatar;
        }

        return closest;
    }

    private bool IsReviveKeyboardHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.eKey.isPressed;
#else
        return Input.GetKey(KeyCode.E);
#endif
    }

    private void ResetReviveHold()
    {
        reviveHoldTimer = 0f;
        reviveRequestSent = false;
        SetReviveProgress(0f);
    }

    private void SetReviveButtonVisible(bool visible)
    {
        if (reviveButton != null && reviveButton.gameObject.activeSelf != visible)
        {
            reviveButton.gameObject.SetActive(visible);
        }

        if (!visible)
        {
            reviveButtonHeld = false;
        }
    }

    private void SetReviveProgress(float progress)
    {
        progress = Mathf.Clamp01(progress);

        if (reviveProgressImage != null)
        {
            reviveProgressImage.fillAmount = progress;
        }

        if (reviveButtonText != null)
        {
            reviveButtonText.text = progress > 0f
                ? $"Revive {Mathf.RoundToInt(progress * 100f)}%"
                : "Revive";
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
