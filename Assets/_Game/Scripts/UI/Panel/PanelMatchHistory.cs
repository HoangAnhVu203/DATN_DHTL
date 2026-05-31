using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class PanelMatchHistory : UICanvas
{
    [SerializeField] private SupabaseConfig config;
    [SerializeField] private Transform contentRoot;
    [SerializeField] private GameObject rowTemplate;
    [SerializeField] private Button closeButton;
    [SerializeField] private Text statusText;
    [SerializeField] private int limit = 50;

    private readonly List<GameObject> spawnedRows = new List<GameObject>();
    private bool referencesResolved;
    private Coroutine loadRoutine;

    public static PanelMatchHistory OpenFromScene()
    {
        UIManager uiManager = FindFirstObjectByType<UIManager>();
        if (uiManager != null)
        {
            return uiManager.OpenUI<PanelMatchHistory>();
        }

        PanelMatchHistory existingPanel = FindFirstObjectByType<PanelMatchHistory>(FindObjectsInactive.Include);
        if (existingPanel != null)
        {
            existingPanel.Open();
            return existingPanel;
        }

        PanelMatchHistory prefab = Resources.Load<PanelMatchHistory>("UI/Panel - MatchHistory");
        Canvas canvas = FindFirstObjectByType<Canvas>();
        Transform parent = canvas != null ? canvas.transform : null;
        PanelMatchHistory panel = Instantiate(prefab, parent);
        panel.Open();
        return panel;
    }

    public override void Open()
    {
        base.Open();
        ResolveReferences();
        Refresh();
    }

    public override void CloseDirectly()
    {
        if (loadRoutine != null)
        {
            StopCoroutine(loadRoutine);
            loadRoutine = null;
        }

        base.CloseDirectly();
    }

    private void OnDestroy()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseDirectly);
        }
    }

    public void Refresh()
    {
        ResolveReferences();
        ClearRows();
        SetStatus("Loading...");

        if (!CanUseSupabase())
        {
            SetStatus("Ban chua dang nhap.");
            return;
        }

        if (rowTemplate == null || contentRoot == null)
        {
            SetStatus("Missing match history UI template.");
            return;
        }

        if (loadRoutine != null)
        {
            StopCoroutine(loadRoutine);
        }

        loadRoutine = StartCoroutine(LoadMatchHistoryRoutine());
    }

    private IEnumerator LoadMatchHistoryRoutine()
    {
        string jsonBody = JsonUtility.ToJson(new MatchHistoryRequest
        {
            limit = Mathf.Max(1, limit)
        });

        string url = $"{config.FunctionUrl}/get_match_history";
        using UnityWebRequest request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.timeout = 10;
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("apikey", config.AnonKey);
        request.SetRequestHeader("Authorization", $"Bearer {SupabaseSession.AccessToken}");
        request.SetRequestHeader("Accept", "application/json");

        yield return request.SendWebRequest();

        string responseText = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
        if (request.responseCode < 200 || request.responseCode >= 300 || request.result != UnityWebRequest.Result.Success)
        {
            SetStatus(BuildErrorMessage(request.responseCode, request.error, responseText));
            Debug.LogError($"get_match_history failed: {BuildErrorMessage(request.responseCode, request.error, responseText)}");
            loadRoutine = null;
            yield break;
        }

        MatchHistoryResponse response = JsonUtility.FromJson<MatchHistoryResponse>(responseText);
        if (response == null || !response.success)
        {
            string error = response?.error?.message ?? "Get match history failed.";
            SetStatus(error);
            Debug.LogError($"get_match_history failed: {error}");
            loadRoutine = null;
            yield break;
        }

        RenderRows(response.data ?? Array.Empty<MatchHistoryData>());
        loadRoutine = null;
    }

    private void RenderRows(MatchHistoryData[] histories)
    {
        ClearRows();

        if (rowTemplate != null)
        {
            rowTemplate.SetActive(false);
        }

        if (histories.Length == 0)
        {
            SetStatus("Chua co lich su dau.");
            return;
        }

        SetStatus(string.Empty);

        foreach (MatchHistoryData history in histories)
        {
            if (history == null)
            {
                continue;
            }

            GameObject row = Instantiate(rowTemplate, contentRoot);
            row.name = $"MatchHistory_{history.match_id}";
            row.SetActive(true);
            spawnedRows.Add(row);

            SetRowText(row, "Result", FormatResult(history.result));
            SetRowText(row, "Started At", FormatStartedAt(history.started_at, history.created_at));
            SetRowText(row, "Duration", FormatDuration(history.duration_sec));
            SetRowText(row, "Kills", history.kills.ToString());
            SetRowText(row, "Damage", history.damage_dealt.ToString());
            SetRowText(row, "Downs", history.downs.ToString());
            SetRowText(row, "Revives", history.revives.ToString());
        }
    }

    private void ClearRows()
    {
        foreach (GameObject row in spawnedRows)
        {
            if (row != null)
            {
                Destroy(row);
            }
        }

        spawnedRows.Clear();
    }

    private void SetRowText(GameObject row, string childName, string value)
    {
        Transform child = FindChild(row.transform, childName);
        if (child == null)
        {
            Debug.LogWarning($"PanelMatchHistory: missing '{childName}' text in row template.");
            return;
        }

        Text text = child.GetComponent<Text>();
        if (text != null)
        {
            text.text = value;
        }
    }

    private void ResolveReferences()
    {
        if (referencesResolved)
        {
            return;
        }

        if (config == null)
        {
            AuthService authService = FindFirstObjectByType<AuthService>();
            if (authService != null)
            {
                config = authService.Config;
            }
        }

        if (contentRoot == null)
        {
            Transform contentTransform = FindChild(transform, "Content");
            contentRoot = contentTransform;
        }

        if (rowTemplate == null && contentRoot != null && contentRoot.childCount > 0)
        {
            rowTemplate = contentRoot.GetChild(0).gameObject;
        }

        closeButton ??= FindButtonByName("CloseBtn");
        statusText ??= FindStatusText();

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseDirectly);
            closeButton.onClick.AddListener(CloseDirectly);
        }

        if (rowTemplate != null)
        {
            rowTemplate.SetActive(false);
        }

        referencesResolved = true;
    }

    private bool CanUseSupabase()
    {
        return config != null
               && SupabaseSession.IsLoggedIn
               && !string.IsNullOrWhiteSpace(SupabaseSession.AccessToken)
               && !string.IsNullOrWhiteSpace(config.FunctionUrl);
    }

    private Text FindStatusText()
    {
        foreach (Text text in GetComponentsInChildren<Text>(true))
        {
            if (text != null && text.name.Equals("StatusText", StringComparison.OrdinalIgnoreCase))
            {
                return text;
            }
        }

        return null;
    }

    private Button FindButtonByName(string objectName)
    {
        Transform root = FindChild(transform, objectName);
        return root != null ? root.GetComponent<Button>() : null;
    }

    private Transform FindChild(Transform root, string childName)
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

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    private string FormatResult(string result)
    {
        if (string.Equals(result, "win", StringComparison.OrdinalIgnoreCase)
            || string.Equals(result, "victory", StringComparison.OrdinalIgnoreCase))
        {
            return "Win";
        }

        if (string.Equals(result, "lose", StringComparison.OrdinalIgnoreCase)
            || string.Equals(result, "loss", StringComparison.OrdinalIgnoreCase)
            || string.Equals(result, "defeat", StringComparison.OrdinalIgnoreCase))
        {
            return "Lose";
        }

        return string.IsNullOrWhiteSpace(result) ? "-" : result;
    }

    private string FormatStartedAt(string startedAt, string createdAt)
    {
        string rawValue = !string.IsNullOrWhiteSpace(startedAt) ? startedAt : createdAt;
        if (DateTimeOffset.TryParse(rawValue, out DateTimeOffset parsed))
        {
            return parsed.ToLocalTime().ToString("dd/MM HH:mm");
        }

        return "-";
    }

    private string FormatDuration(int durationSeconds)
    {
        durationSeconds = Mathf.Max(0, durationSeconds);
        int minutes = durationSeconds / 60;
        int seconds = durationSeconds % 60;
        return $"{minutes:00}:{seconds:00}";
    }

    private string BuildErrorMessage(long statusCode, string requestError, string responseText)
    {
        if (!string.IsNullOrWhiteSpace(responseText))
        {
            return $"HTTP {statusCode}: {responseText}";
        }

        if (!string.IsNullOrWhiteSpace(requestError))
        {
            return $"HTTP {statusCode}: {requestError}";
        }

        return $"HTTP {statusCode}: Supabase request failed.";
    }

    [Serializable]
    private class MatchHistoryRequest
    {
        public int limit;
    }

    [Serializable]
    private class MatchHistoryResponse
    {
        public bool success;
        public MatchHistoryData[] data;
        public ErrorData error;
    }

    [Serializable]
    private class MatchHistoryData
    {
        public string user_id;
        public string match_id;
        public string room_id;
        public string result;
        public string status;
        public int seed;
        public string started_at;
        public string ended_at;
        public int duration_sec;
        public string created_at;
        public bool is_host;
        public int survive_time_sec;
        public int kills;
        public int downs;
        public int revives;
        public int damage_dealt;
        public bool is_dead;
        public bool is_win;
    }

    [Serializable]
    private class ErrorData
    {
        public string code;
        public string message;
    }
}
