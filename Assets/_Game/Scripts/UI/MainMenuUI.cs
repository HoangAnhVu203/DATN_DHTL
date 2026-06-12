using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private float minimumLoadingTime = 0.5f;
    [SerializeField] private PanelLoading loadingPanelPrefab;
    [SerializeField] private Button informationPlayerButton;
    [SerializeField] private Button matchHistoryButton;
    [SerializeField] private Button logoutButton;
    [SerializeField] private PanelLogin panelLogin;
    [SerializeField] private TMP_Text displayNameText;
    [SerializeField] private TMP_Text coinText;

    private bool isLoading;
    private bool isLoggingOut;

    private void Awake()
    {
        if (informationPlayerButton == null)
        {
            GameObject informationButtonObject = GameObject.Find("InformationPlayer");
            if (informationButtonObject != null)
            {
                informationPlayerButton = informationButtonObject.GetComponent<Button>();
            }
        }

        if (informationPlayerButton != null)
        {
            informationPlayerButton.onClick.AddListener(OpenInformationPanel);
        }

        if (matchHistoryButton == null)
        {
            matchHistoryButton = FindButtonByNames("MatchHistory", "MatchHistoryBtn", "HistoryBtn", "ButtonMatchHistory");
        }

        if (matchHistoryButton != null)
        {
            matchHistoryButton.onClick.AddListener(OpenMatchHistoryPanel);
        }

        if (logoutButton == null)
        {
            logoutButton = FindButtonByNames("LogoutBtn", "ButtonLogout", "LogoutButton", "Logout");
        }

        if (logoutButton != null)
        {
            logoutButton.onClick.AddListener(Logout);
        }

        if (panelLogin == null)
        {
            panelLogin = FindFirstObjectByType<PanelLogin>(FindObjectsInactive.Include);
        }

        if (displayNameText == null)
        {
            displayNameText = FindTextByNames("PlayerDisplayName", "DisplayName", "DisplayNameText");
        }

        if (coinText == null)
        {
            coinText = FindTextByNames("CoinTxt", "CoinText", "PlayerCoinText");
        }

        SupabaseSession.CoinChanged += OnSessionCoinChanged;
        RefreshCoinText();
    }

    private void OnDestroy()
    {
        if (informationPlayerButton != null)
        {
            informationPlayerButton.onClick.RemoveListener(OpenInformationPanel);
        }

        if (matchHistoryButton != null)
        {
            matchHistoryButton.onClick.RemoveListener(OpenMatchHistoryPanel);
        }

        if (logoutButton != null)
        {
            logoutButton.onClick.RemoveListener(Logout);
        }

        SupabaseSession.CoinChanged -= OnSessionCoinChanged;
    }

    public void StartGame()
    {
        if (isLoading)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(gameSceneName))
        {
            Debug.LogError("MainMenuUI: gameSceneName is empty.");
            return;
        }

        OnlineRoomSession.Clear();
        isLoading = true;

        SceneLoadingRunner loadingRunner = new GameObject("Scene Loading Runner").AddComponent<SceneLoadingRunner>();
        DontDestroyOnLoad(loadingRunner.gameObject);
        loadingRunner.LoadScene(gameSceneName, minimumLoadingTime, loadingPanelPrefab, () => isLoading = false);
    }

    private void OpenInformationPanel()
    {
        if (!SupabaseSession.IsLoggedIn)
        {
            Debug.LogWarning("Login before opening player information.");
            return;
        }

        PanelInformation.OpenFromScene();
    }

    private void OpenMatchHistoryPanel()
    {
        if (!SupabaseSession.IsLoggedIn)
        {
            Debug.LogWarning("Login before opening match history.");
            return;
        }

        PanelMatchHistory.OpenFromScene();
    }

    public void Logout()
    {
        if (isLoggingOut)
        {
            return;
        }

        StartCoroutine(LogoutRoutine());
    }

    private IEnumerator LogoutRoutine()
    {
        isLoggingOut = true;
        SetMenuButtonsInteractable(false);
        OnlineMatchLoadingOverlay.Show(0.1f);

        AuthService authService = FindFirstObjectByType<AuthService>();
        bool logoutCompleted = false;

        if (authService != null)
        {
            yield return authService.SignOut((success, message) =>
            {
                logoutCompleted = success;
                Debug.Log(message);
            });
        }
        else
        {
            OnlineRoomSession.Clear();
            SupabaseSession.Clear();
            logoutCompleted = true;
        }

        OnlineMatchLoadingOverlay.SetProgress(1f);
        OnlineMatchLoadingOverlay.Hide();

        RefreshLoggedOutView();
        SetMenuButtonsInteractable(true);
        isLoggingOut = false;

        if (!logoutCompleted)
        {
            Debug.LogWarning("Logout did not complete cleanly, but local session was cleared.");
        }
    }

    private void RefreshLoggedOutView()
    {
        if (panelLogin == null)
        {
            panelLogin = FindFirstObjectByType<PanelLogin>(FindObjectsInactive.Include);
        }

        if (panelLogin != null)
        {
            panelLogin.ResetForLoggedOut();
            panelLogin.gameObject.SetActive(true);
        }

        ClearRoomUiState();

        if (displayNameText != null)
        {
            displayNameText.text = string.Empty;
        }

        PanelInformation.RefreshInformationPlayerAvatar();
        RefreshCoinText();
    }

    private void ClearRoomUiState()
    {
        PanelRoomTest[] roomPanels = FindObjectsByType<PanelRoomTest>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (PanelRoomTest roomPanel in roomPanels)
        {
            if (roomPanel != null)
            {
                roomPanel.ClearTransientText();
            }
        }

        PanelRoomMatch[] roomMatchPanels = FindObjectsByType<PanelRoomMatch>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (PanelRoomMatch roomMatchPanel in roomMatchPanels)
        {
            if (roomMatchPanel != null && roomMatchPanel.gameObject.activeInHierarchy)
            {
                roomMatchPanel.CloseDirectly();
            }
        }
    }

    private void SetMenuButtonsInteractable(bool interactable)
    {
        if (informationPlayerButton != null)
        {
            informationPlayerButton.interactable = interactable;
        }

        if (matchHistoryButton != null)
        {
            matchHistoryButton.interactable = interactable;
        }

        if (logoutButton != null)
        {
            logoutButton.interactable = interactable;
        }
    }

    private Button FindButtonByNames(params string[] objectNames)
    {
        foreach (string objectName in objectNames)
        {
            GameObject buttonObject = GameObject.Find(objectName);
            if (buttonObject == null)
            {
                continue;
            }

            Button button = buttonObject.GetComponent<Button>();
            if (button != null)
            {
                return button;
            }
        }

        return null;
    }

    private TMP_Text FindTextByNames(params string[] objectNames)
    {
        foreach (string objectName in objectNames)
        {
            GameObject textObject = GameObject.Find(objectName);
            if (textObject == null)
            {
                continue;
            }

            TMP_Text text = textObject.GetComponent<TMP_Text>();
            if (text != null)
            {
                return text;
            }
        }

        return null;
    }

    private void OnSessionCoinChanged(int coin)
    {
        RefreshCoinText();
    }

    private void RefreshCoinText()
    {
        if (coinText != null)
        {
            coinText.text = SupabaseSession.Coin.ToString();
        }
    }
}

public class SceneLoadingRunner : MonoBehaviour
{
    private System.Action onLoadFailed;

    public void LoadScene(string sceneName, float minimumLoadingTime, PanelLoading loadingPanelPrefab, System.Action onLoadFailed = null)
    {
        this.onLoadFailed = onLoadFailed;
        StartCoroutine(LoadSceneRoutine(sceneName, minimumLoadingTime, loadingPanelPrefab));
    }

    private IEnumerator LoadSceneRoutine(string sceneName, float minimumLoadingTime, PanelLoading loadingPanelPrefab)
    {
        Time.timeScale = 1f;

        PanelLoading loadingPanel = CreateLoadingPanel(loadingPanelPrefab);
        if (loadingPanel != null)
        {
            loadingPanel.SetProgress(0f);
        }

        yield return null;

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName);
        if (loadOperation == null)
        {
            Debug.LogError($"SceneLoadingRunner: cannot load scene '{sceneName}'. Check Build Settings and scene name.");

            if (loadingPanel != null)
            {
                Destroy(loadingPanel.transform.root.gameObject);
            }

            onLoadFailed?.Invoke();
            Destroy(gameObject);
            yield break;
        }

        loadOperation.allowSceneActivation = false;

        float elapsedTime = 0f;
        float visibleProgress = 0f;
        float duration = Mathf.Max(0.01f, minimumLoadingTime);

        while (loadOperation.progress < 0.9f || visibleProgress < 1f || elapsedTime < minimumLoadingTime)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float sceneProgress = Mathf.Clamp01(loadOperation.progress / 0.9f);
            float timeProgress = Mathf.Clamp01(elapsedTime / duration);
            float targetProgress = Mathf.Min(sceneProgress, timeProgress);

            visibleProgress = Mathf.MoveTowards(visibleProgress, targetProgress, Time.unscaledDeltaTime / duration);
            if (loadingPanel != null)
            {
                loadingPanel.SetProgress(visibleProgress);
            }

            yield return null;
        }

        if (loadingPanel != null)
        {
            loadingPanel.SetProgress(1f);
        }
        yield return null;

        loadOperation.allowSceneActivation = true;

        while (!loadOperation.isDone)
        {
            yield return null;
        }

        if (loadingPanel != null)
        {
            Destroy(loadingPanel.transform.root.gameObject);
        }

        Destroy(gameObject);
    }

    private PanelLoading CreateLoadingPanel(PanelLoading loadingPanelPrefab)
    {
        Canvas loadingCanvas = new GameObject("Loading Canvas").AddComponent<Canvas>();
        loadingCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        loadingCanvas.sortingOrder = short.MaxValue;
        loadingCanvas.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
        loadingCanvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        DontDestroyOnLoad(loadingCanvas.gameObject);

        if (loadingPanelPrefab == null)
        {
            loadingPanelPrefab = Resources.Load<PanelLoading>("UI/Panel - Loading");
        }

        if (loadingPanelPrefab == null)
        {
            Debug.LogError("Missing PanelLoading prefab at Resources/UI/Panel - Loading.");
            return null;
        }

        PanelLoading loadingPanel = Instantiate(loadingPanelPrefab, loadingCanvas.transform);
        RectTransform panelRect = loadingPanel.GetComponent<RectTransform>();

        if (panelRect != null)
        {
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
        }

        loadingPanel.Open();
        return loadingPanel;
    }
}
