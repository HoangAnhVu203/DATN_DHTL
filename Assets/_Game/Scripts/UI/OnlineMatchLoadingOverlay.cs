using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class OnlineMatchLoadingOverlay
{
    private const string LoadingPrefabPath = "UI/Panel - Loading";

    private static Canvas loadingCanvas;
    private static PanelLoading loadingPanel;

    public static bool IsVisible => loadingPanel != null && loadingPanel.gameObject.activeSelf;

    public static void Show(float progress = 0f)
    {
        EnsurePanel();

        if (loadingPanel == null)
        {
            return;
        }

        loadingPanel.Open();
        SetProgress(progress);
    }

    public static void SetProgress(float progress)
    {
        if (loadingPanel == null)
        {
            return;
        }

        loadingPanel.SetProgress(progress);
    }

    public static void Hide()
    {
        if (loadingCanvas == null)
        {
            loadingPanel = null;
            return;
        }

        Object.Destroy(loadingCanvas.gameObject);
        loadingCanvas = null;
        loadingPanel = null;
    }

    public static void LoadScene(string sceneName)
    {
        GameObject runnerObject = new GameObject("Online Match Scene Loading Runner");
        Object.DontDestroyOnLoad(runnerObject);
        runnerObject.AddComponent<OnlineMatchSceneLoadingRunner>().Load(sceneName);
    }

    private static void EnsurePanel()
    {
        if (loadingPanel != null && loadingCanvas != null)
        {
            return;
        }

        PanelLoading prefab = Resources.Load<PanelLoading>(LoadingPrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"Missing PanelLoading prefab at Resources/{LoadingPrefabPath}.");
            return;
        }

        loadingCanvas = new GameObject("Online Match Loading Canvas").AddComponent<Canvas>();
        loadingCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        loadingCanvas.sortingOrder = short.MaxValue;
        loadingCanvas.gameObject.AddComponent<CanvasScaler>();
        loadingCanvas.gameObject.AddComponent<GraphicRaycaster>();
        Object.DontDestroyOnLoad(loadingCanvas.gameObject);

        loadingPanel = Object.Instantiate(prefab, loadingCanvas.transform);
        RectTransform panelRect = loadingPanel.GetComponent<RectTransform>();
        if (panelRect != null)
        {
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
        }
    }
}

public class OnlineMatchSceneLoadingRunner : MonoBehaviour
{
    public void Load(string sceneName)
    {
        StartCoroutine(LoadRoutine(sceneName));
    }

    private IEnumerator LoadRoutine(string sceneName)
    {
        OnlineMatchLoadingOverlay.Show(0f);
        yield return null;

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName);
        if (loadOperation == null)
        {
            OnlineMatchLoadingOverlay.Hide();
            Destroy(gameObject);
            yield break;
        }

        while (!loadOperation.isDone)
        {
            float sceneProgress = Mathf.Clamp01(loadOperation.progress / 0.9f);
            OnlineMatchLoadingOverlay.SetProgress(Mathf.Lerp(0f, 0.6f, sceneProgress));
            yield return null;
        }

        OnlineMatchLoadingOverlay.SetProgress(0.6f);
        Destroy(gameObject);
    }
}
