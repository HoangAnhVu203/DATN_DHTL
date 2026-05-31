using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class PanelInformation : UICanvas
{
    private const string LocalAvatarPrefix = "avatar_";
    private const string PlayerDisplayNameObjectName = "PlayerDisplayName";
    private const string InformationButtonObjectName = "InformationPlayer";

    [SerializeField] private SupabaseConfig config;
    [SerializeField] private Image avatarImage;
    [SerializeField] private Button avatarButton;
    [SerializeField] private GameObject selectAvatarPanel;
    [SerializeField] private Transform selectAvatarHolder;
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private TMP_InputField displayNameInput;
    [SerializeField] private Button editUsernameButton;
    [SerializeField] private Button editDisplayNameButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button historyButton;
    [SerializeField] private TMP_Text statusText;

    private readonly List<Sprite> avatarSprites = new List<Sprite>();
    private string selectedAvatarKey;
    private bool isSaving;
    private bool referencesResolved;

    public static void RefreshInformationPlayerAvatar()
    {
        GameObject infoButtonObject = GameObject.Find(InformationButtonObjectName);
        if (infoButtonObject == null)
        {
            return;
        }

        Image informationButtonImage = infoButtonObject.GetComponent<Image>();
        if (informationButtonImage == null)
        {
            return;
        }

        Sprite avatarSprite = GetSpriteForAvatarFromPrefab(SupabaseSession.AvatarUrl);
        if (avatarSprite == null)
        {
            return;
        }

        informationButtonImage.sprite = avatarSprite;
        informationButtonImage.enabled = true;
    }

    public static PanelInformation OpenFromScene()
    {
        UIManager uiManager = FindFirstObjectByType<UIManager>();
        if (uiManager != null)
        {
            return uiManager.OpenUI<PanelInformation>();
        }

        PanelInformation existingPanel = FindFirstObjectByType<PanelInformation>(FindObjectsInactive.Include);
        if (existingPanel != null)
        {
            existingPanel.Open();
            return existingPanel;
        }

        PanelInformation prefab = Resources.Load<PanelInformation>("UI/Panel - Information");
        Canvas canvas = FindFirstObjectByType<Canvas>();
        Transform parent = canvas != null ? canvas.transform : null;
        PanelInformation panel = Instantiate(prefab, parent);
        panel.Open();
        return panel;
    }

    public override void Open()
    {
        base.Open();
        ResolveReferences();
        LoadSessionIntoUI();
        StartCoroutine(LoadProfileRoutine());
    }

    private void OnDestroy()
    {
        if (avatarButton != null)
        {
            avatarButton.onClick.RemoveListener(OpenSelectAvatarPanel);
        }

        if (editUsernameButton != null)
        {
            editUsernameButton.onClick.RemoveListener(EnableUsernameEdit);
        }

        if (editDisplayNameButton != null)
        {
            editDisplayNameButton.onClick.RemoveListener(EnableDisplayNameEdit);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(OnCloseClicked);
        }

        if (historyButton != null)
        {
            historyButton.onClick.RemoveListener(OpenMatchHistoryPanel);
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

        Transform avatarTransform = FindChild(transform, "Avatar");
        if (avatarTransform != null)
        {
            avatarImage ??= avatarTransform.GetComponent<Image>();
            avatarButton ??= avatarTransform.GetComponent<Button>();
        }

        if (selectAvatarPanel == null)
        {
            Transform panelTransform = FindChild(transform, "Panel - SelectAvatar");
            selectAvatarPanel = panelTransform != null ? panelTransform.gameObject : null;
        }

        if (selectAvatarHolder == null)
        {
            Transform holderTransform = FindChild(transform, "SelectAvatarHolder");
            selectAvatarHolder = holderTransform;
        }

        usernameInput ??= FindInputUnder("ID");
        displayNameInput ??= FindInputUnder("UserName");
        closeButton ??= FindButtonByName("CloseBtn");

        ResolveEditButtons();
        ResolveStatusText();
        ResolveAvatarOptions();

        if (avatarButton != null)
        {
            avatarButton.onClick.RemoveListener(OpenSelectAvatarPanel);
            avatarButton.onClick.AddListener(OpenSelectAvatarPanel);
        }

        if (editUsernameButton != null)
        {
            editUsernameButton.onClick.RemoveListener(EnableUsernameEdit);
            editUsernameButton.onClick.AddListener(EnableUsernameEdit);
        }

        if (editDisplayNameButton != null)
        {
            editDisplayNameButton.onClick.RemoveListener(EnableDisplayNameEdit);
            editDisplayNameButton.onClick.AddListener(EnableDisplayNameEdit);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(OnCloseClicked);
            closeButton.onClick.AddListener(OnCloseClicked);
        }

        if (historyButton != null)
        {
            historyButton.onClick.RemoveListener(OpenMatchHistoryPanel);
            historyButton.onClick.AddListener(OpenMatchHistoryPanel);
        }

        if (selectAvatarPanel != null)
        {
            selectAvatarPanel.SetActive(false);
        }

        referencesResolved = true;
    }

    private void ResolveEditButtons()
    {
        Transform usernameRoot = FindChild(transform, "ID");
        Transform displayNameRoot = FindChild(transform, "UserName");

        if (usernameRoot != null)
        {
            editUsernameButton ??= FindFirstChildButtonExcludingInput(usernameRoot);
        }

        if (displayNameRoot != null)
        {
            editDisplayNameButton ??= FindFirstChildButtonExcludingInput(displayNameRoot);
        }
    }

    private void ResolveStatusText()
    {
        if (statusText != null)
        {
            return;
        }

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text text in texts)
        {
            if (text != null && text.name.Equals("StatusText", StringComparison.OrdinalIgnoreCase))
            {
                statusText = text;
                return;
            }
        }
    }

    private void ResolveAvatarOptions()
    {
        if (selectAvatarHolder == null || avatarSprites.Count > 0)
        {
            return;
        }

        Button[] buttons = selectAvatarHolder.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            Image optionImage = button.GetComponent<Image>();
            if (optionImage == null || optionImage.sprite == null)
            {
                continue;
            }

            int avatarIndex = avatarSprites.Count;
            Sprite avatarSprite = optionImage.sprite;
            string avatarKey = $"{LocalAvatarPrefix}{avatarIndex}";

            avatarSprites.Add(avatarSprite);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SelectAvatar(avatarKey, avatarSprite));
        }
    }

    private void LoadSessionIntoUI()
    {
        if (usernameInput != null)
        {
            usernameInput.text = SupabaseSession.Username ?? string.Empty;
            usernameInput.readOnly = true;
        }

        if (displayNameInput != null)
        {
            displayNameInput.text = SupabaseSession.DisplayName ?? string.Empty;
            displayNameInput.readOnly = true;
        }

        selectedAvatarKey = SupabaseSession.AvatarUrl;
        ApplyAvatar(selectedAvatarKey);
        SetStatus(string.Empty);
    }

    private IEnumerator LoadProfileRoutine()
    {
        if (!CanUseSupabase())
        {
            yield break;
        }

        string userId = Uri.EscapeDataString(SupabaseSession.UserId);
        string url = $"{config.SupabaseUrl}/rest/v1/users?id=eq.{userId}&select=id,username,display_name,avatar_url";

        using UnityWebRequest request = UnityWebRequest.Get(url);
        ApplyAuthHeaders(request);

        yield return request.SendWebRequest();

        string responseText = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
        if (request.responseCode < 200 || request.responseCode >= 300 || request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError(BuildErrorMessage(request.responseCode, request.error, responseText));
            yield break;
        }

        UserProfile[] profiles = FromJsonArray<UserProfile>(responseText);
        if (profiles.Length == 0)
        {
            yield break;
        }

        UserProfile profile = profiles[0];
        SupabaseSession.Username = profile.username;
        SupabaseSession.DisplayName = profile.display_name;
        SupabaseSession.AvatarUrl = profile.avatar_url;
        LoadSessionIntoUI();
    }

    private void OpenSelectAvatarPanel()
    {
        if (selectAvatarPanel != null)
        {
            selectAvatarPanel.SetActive(true);
        }
    }

    private void SelectAvatar(string avatarKey, Sprite avatarSprite)
    {
        selectedAvatarKey = avatarKey;

        if (avatarImage != null)
        {
            avatarImage.sprite = avatarSprite;
            avatarImage.enabled = avatarSprite != null;
        }

        if (selectAvatarPanel != null)
        {
            selectAvatarPanel.SetActive(false);
        }
    }

    private void EnableUsernameEdit()
    {
        EnableInput(usernameInput);
    }

    private void EnableDisplayNameEdit()
    {
        EnableInput(displayNameInput);
    }

    private void EnableInput(TMP_InputField input)
    {
        if (input == null)
        {
            return;
        }

        input.readOnly = false;
        input.Select();
        input.ActivateInputField();
    }

    private void OnCloseClicked()
    {
        if (isSaving)
        {
            return;
        }

        StartCoroutine(SaveAndCloseRoutine());
    }

    private void OpenMatchHistoryPanel()
    {
        if (!SupabaseSession.IsLoggedIn)
        {
            SetStatus("Ban chua dang nhap.");
            return;
        }

        PanelMatchHistory.OpenFromScene();
    }

    private IEnumerator SaveAndCloseRoutine()
    {
        if (!CanUseSupabase())
        {
            CloseDirectly();
            yield break;
        }

        string username = usernameInput != null ? usernameInput.text.Trim() : SupabaseSession.Username;
        string displayName = displayNameInput != null ? displayNameInput.text.Trim() : SupabaseSession.DisplayName;

        if (string.IsNullOrWhiteSpace(username))
        {
            SetStatus("Username khong duoc de trong.");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            SetStatus("Display name khong duoc de trong.");
            yield break;
        }

        isSaving = true;
        SetStatus("Dang luu...");

        string userId = Uri.EscapeDataString(SupabaseSession.UserId);
        string url = $"{config.SupabaseUrl}/rest/v1/users?id=eq.{userId}";
        string jsonBody = JsonUtility.ToJson(new UpdateProfileRequest
        {
            username = username,
            display_name = displayName,
            avatar_url = selectedAvatarKey,
            updated_at = DateTime.UtcNow.ToString("O")
        });

        using UnityWebRequest request = new UnityWebRequest(url, "PATCH");
        byte[] body = Encoding.UTF8.GetBytes(jsonBody);
        request.uploadHandler = new UploadHandlerRaw(body);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Prefer", "return=representation");
        ApplyAuthHeaders(request);

        yield return request.SendWebRequest();

        string responseText = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
        isSaving = false;

        if (request.responseCode < 200 || request.responseCode >= 300 || request.result != UnityWebRequest.Result.Success)
        {
            SetStatus("Luu that bai.");
            Debug.LogError(BuildErrorMessage(request.responseCode, request.error, responseText));
            yield break;
        }

        SupabaseSession.Username = username;
        SupabaseSession.DisplayName = displayName;
        SupabaseSession.AvatarUrl = selectedAvatarKey;

        TMP_Text displayNameText = FindDisplayNameText();
        if (displayNameText != null)
        {
            displayNameText.text = displayName;
        }

        Image informationButtonImage = FindInformationButtonImage();
        if (informationButtonImage != null)
        {
            informationButtonImage.sprite = GetSpriteForAvatar(selectedAvatarKey) ?? informationButtonImage.sprite;
        }

        CloseDirectly();
    }

    private bool CanUseSupabase()
    {
        return config != null && SupabaseSession.IsLoggedIn && !string.IsNullOrWhiteSpace(SupabaseSession.UserId);
    }

    private void ApplyAuthHeaders(UnityWebRequest request)
    {
        request.SetRequestHeader("apikey", config.AnonKey);
        request.SetRequestHeader("Authorization", $"Bearer {SupabaseSession.AccessToken}");
        request.SetRequestHeader("Accept", "application/json");
    }

    private void ApplyAvatar(string avatarKey)
    {
        Sprite sprite = GetSpriteForAvatar(avatarKey);
        if (avatarImage != null)
        {
            avatarImage.sprite = sprite ?? avatarImage.sprite;
            avatarImage.enabled = avatarImage.sprite != null;
        }
    }

    private Sprite GetSpriteForAvatar(string avatarKey)
    {
        if (string.IsNullOrWhiteSpace(avatarKey) || !avatarKey.StartsWith(LocalAvatarPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return avatarSprites.Count > 0 ? avatarSprites[0] : null;
        }

        string indexText = avatarKey.Substring(LocalAvatarPrefix.Length);
        if (!int.TryParse(indexText, out int index) || index < 0 || index >= avatarSprites.Count)
        {
            return avatarSprites.Count > 0 ? avatarSprites[0] : null;
        }

        return avatarSprites[index];
    }

    private TMP_Text FindDisplayNameText()
    {
        GameObject displayNameObject = GameObject.Find(PlayerDisplayNameObjectName);
        return displayNameObject != null ? displayNameObject.GetComponent<TMP_Text>() : null;
    }

    private Image FindInformationButtonImage()
    {
        GameObject infoButtonObject = GameObject.Find(InformationButtonObjectName);
        return infoButtonObject != null ? infoButtonObject.GetComponent<Image>() : null;
    }

    private static Sprite GetSpriteForAvatarFromPrefab(string avatarKey)
    {
        List<Sprite> sprites = LoadAvatarSpritesFromInformationPrefab();
        if (sprites.Count == 0)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(avatarKey) || !avatarKey.StartsWith(LocalAvatarPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return sprites[0];
        }

        string indexText = avatarKey.Substring(LocalAvatarPrefix.Length);
        if (!int.TryParse(indexText, out int index) || index < 0 || index >= sprites.Count)
        {
            return sprites[0];
        }

        return sprites[index];
    }

    private static List<Sprite> LoadAvatarSpritesFromInformationPrefab()
    {
        List<Sprite> sprites = new List<Sprite>();
        GameObject prefab = Resources.Load<GameObject>("UI/Panel - Information");
        if (prefab == null)
        {
            return sprites;
        }

        Transform holder = FindChildIn(prefab.transform, "SelectAvatarHolder");
        if (holder == null)
        {
            return sprites;
        }

        Button[] buttons = holder.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            Image optionImage = button.GetComponent<Image>();
            if (optionImage != null && optionImage.sprite != null)
            {
                sprites.Add(optionImage.sprite);
            }
        }

        return sprites;
    }

    private static Transform FindChildIn(Transform root, string childName)
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

    private TMP_InputField FindInputUnder(string objectName)
    {
        Transform root = FindChild(transform, objectName);
        return root != null ? root.GetComponentInChildren<TMP_InputField>(true) : null;
    }

    private Button FindButtonByName(string objectName)
    {
        Transform root = FindChild(transform, objectName);
        return root != null ? root.GetComponent<Button>() : null;
    }

    private Button FindFirstChildButtonExcludingInput(Transform root)
    {
        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (button.GetComponent<TMP_InputField>() == null && button.GetComponentInParent<TMP_InputField>() == null)
            {
                return button;
            }
        }

        return null;
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

    private T[] FromJsonArray<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<T>();
        }

        JsonArrayWrapper<T> wrapper = JsonUtility.FromJson<JsonArrayWrapper<T>>($"{{\"items\":{json}}}");
        return wrapper?.items ?? Array.Empty<T>();
    }

    [Serializable]
    private class UserProfile
    {
        public string id;
        public string username;
        public string display_name;
        public string avatar_url;
    }

    [Serializable]
    private class UpdateProfileRequest
    {
        public string username;
        public string display_name;
        public string avatar_url;
        public string updated_at;
    }

    [Serializable]
    private class JsonArrayWrapper<T>
    {
        public T[] items;
    }
}
