using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private float minimumLoadingTime = 0.5f;
    [SerializeField] private PanelLoading loadingPanelPrefab;

    private bool isLoading;

    public void StartGame()
    {
        if (isLoading)
        {
            return;
        }

        isLoading = true;

        SceneLoadingRunner loadingRunner = new GameObject("Scene Loading Runner").AddComponent<SceneLoadingRunner>();
        DontDestroyOnLoad(loadingRunner.gameObject);
        loadingRunner.LoadScene(gameSceneName, minimumLoadingTime, loadingPanelPrefab);
    }
}

public class SceneLoadingRunner : MonoBehaviour
{
    public void LoadScene(string sceneName, float minimumLoadingTime, PanelLoading loadingPanelPrefab)
    {
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
