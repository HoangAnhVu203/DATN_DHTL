using TMPro;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PanelLogin : MonoBehaviour
{
    private const string DefaultDisplayNameObjectName = "PlayerDisplayName";
    private const string LoginModeSubmitText = "Login";
    private const string LoginModeToggleText = "Register";
    private const string RegisterModeSubmitText = "Register";
    private const string RegisterModeToggleText = "Login";

    [SerializeField] private AuthService authService;
    [SerializeField] private TMP_InputField emailInput;
    [SerializeField] private TMP_InputField passwordInput;
    [SerializeField] private Button loginButton;
    [SerializeField] private Button registerToggleButton;
    [SerializeField] private Text loginButtonText;
    [SerializeField] private TMP_Text registerToggleText;
    [SerializeField] private Text statusText;
    [SerializeField] private TMP_Text displayNameText;
    [SerializeField] private GameObject panelToHideOnSuccess;

    private bool isRegisterMode;
    private Coroutine loginLoadingCoroutine;

    private void Awake()
    {
        ResolveButtonTextReferences();
        ResolveRegisterToggleButton();

        loginButton.onClick.AddListener(OnSubmitClicked);

        if (registerToggleButton != null)
        {
            registerToggleButton.onClick.AddListener(ToggleMode);
        }

        if (panelToHideOnSuccess == null)
        {
            panelToHideOnSuccess = gameObject;
        }

        if (displayNameText == null)
        {
            GameObject displayNameObject = GameObject.Find(DefaultDisplayNameObjectName);
            if (displayNameObject != null)
            {
                displayNameText = displayNameObject.GetComponent<TMP_Text>();
            }
        }

        RefreshModeText();
        HideIfAlreadyLoggedIn();
    }

    private void OnEnable()
    {
        if (!SupabaseSession.IsLoggedIn)
        {
            ResetForLoggedOut();
        }

        HideIfAlreadyLoggedIn();
    }

    private void OnDestroy()
    {
        StopLoginLoading();
        OnlineMatchLoadingOverlay.Hide();

        loginButton.onClick.RemoveListener(OnSubmitClicked);

        if (registerToggleButton != null)
        {
            registerToggleButton.onClick.RemoveListener(ToggleMode);
        }
    }

    private void OnSubmitClicked()
    {
        string email = emailInput.text.Trim();
        string password = passwordInput.text;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            SetStatus("Nhập email và mật khẩu.");
            return;
        }

        loginButton.interactable = false;
        SetToggleInteractable(false);

        if (isRegisterMode)
        {
            SetStatus("Đang đăng ký...");
            StartCoroutine(authService.SignUp(email, password, OnRegisterCompleted));
            return;
        }

        SetStatus("Đang đăng nhập...");
        StartLoginLoading();
        StartCoroutine(authService.SignIn(email, password, OnLoginCompleted));
    }

    private void OnLoginCompleted(bool success, string message)
    {
        StopLoginLoading();
        loginButton.interactable = true;
        SetToggleInteractable(true);

        if (!success)
        {
            OnlineMatchLoadingOverlay.Hide();
            SetStatus(GetUserFriendlyError(message, false));
            Debug.LogError(message);
            return;
        }

        OnlineMatchLoadingOverlay.SetProgress(0.85f);
        SetStatus("Đăng nhập thành công.");
        UpdateDisplayName();
        PanelInformation.RefreshInformationPlayerAvatar();

        if (panelToHideOnSuccess != null)
        {
            panelToHideOnSuccess.SetActive(false);
        }

        OnlineMatchLoadingOverlay.SetProgress(1f);
        OnlineMatchLoadingOverlay.Hide();
        Debug.Log($"Logged in: {SupabaseSession.UserId} - {SupabaseSession.DisplayName}");
    }

    public void ResetForLoggedOut()
    {
        StopLoginLoading();
        OnlineMatchLoadingOverlay.Hide();
        isRegisterMode = false;
        RefreshModeText();
        ClearInputFields();
        SetStatus(string.Empty);

        if (loginButton != null)
        {
            loginButton.interactable = true;
        }

        SetToggleInteractable(true);
    }

    private void StartLoginLoading()
    {
        StopLoginLoading();
        OnlineMatchLoadingOverlay.Show(0.05f);
        loginLoadingCoroutine = StartCoroutine(LoginLoadingRoutine());
    }

    private void StopLoginLoading()
    {
        if (loginLoadingCoroutine == null)
        {
            return;
        }

        StopCoroutine(loginLoadingCoroutine);
        loginLoadingCoroutine = null;
    }

    private IEnumerator LoginLoadingRoutine()
    {
        float progress = 0.05f;

        while (true)
        {
            progress = Mathf.MoveTowards(progress, 0.8f, Time.unscaledDeltaTime * 0.35f);
            OnlineMatchLoadingOverlay.SetProgress(progress);
            yield return null;
        }
    }

    private void OnRegisterCompleted(bool success, string message)
    {
        loginButton.interactable = true;
        SetToggleInteractable(true);

        if (!success)
        {
            SetStatus(GetUserFriendlyError(message, true));
            Debug.LogError(message);
            return;
        }

        SetStatus("Đăng ký thành công.");
        Debug.Log(message);
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    private string GetUserFriendlyError(string error, bool registerContext)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return registerContext ? "Đăng ký thất bại." : "Đăng nhập thất bại.";
        }

        string lowerError = error.ToLowerInvariant();

        if (lowerError.Contains("invalid login credentials"))
        {
            return "Sai email hoặc mật khẩu.";
        }

        if (lowerError.Contains("email not confirmed"))
        {
            return "Email chưa được xác nhận.";
        }

        if (lowerError.Contains("user already registered") || lowerError.Contains("already registered"))
        {
            return "Email này đã được đăng ký.";
        }

        if (lowerError.Contains("password") && lowerError.Contains("6"))
        {
            return "Mật khẩu phải có ít nhất 6 ký tự.";
        }

        if (lowerError.Contains("supabase config"))
        {
            return "Thiếu cấu hình Supabase.";
        }

        if (lowerError.Contains("400"))
        {
            return registerContext ? "Thông tin đăng ký không hợp lệ." : "Thông tin đăng nhập không hợp lệ.";
        }

        return registerContext
            ? "Đăng ký thất bại. Xem Console để biết chi tiết."
            : "Đăng nhập thất bại. Xem Console để biết chi tiết.";
    }

    private void UpdateDisplayName()
    {
        if (displayNameText == null)
        {
            return;
        }

        displayNameText.text = SupabaseSession.DisplayName;
    }

    private void HideIfAlreadyLoggedIn()
    {
        if (!SupabaseSession.IsLoggedIn)
        {
            return;
        }

        UpdateDisplayName();
        PanelInformation.RefreshInformationPlayerAvatar();

        if (panelToHideOnSuccess != null)
        {
            panelToHideOnSuccess.SetActive(false);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void ToggleMode()
    {
        isRegisterMode = !isRegisterMode;
        SetStatus(string.Empty);
        RefreshModeText();
    }

    private void ClearInputFields()
    {
        if (emailInput != null)
        {
            emailInput.text = string.Empty;
        }

        if (passwordInput != null)
        {
            passwordInput.text = string.Empty;
        }
    }

    private void RefreshModeText()
    {
        if (loginButtonText != null)
        {
            loginButtonText.text = isRegisterMode ? RegisterModeSubmitText : LoginModeSubmitText;
        }

        if (registerToggleText != null)
        {
            registerToggleText.text = isRegisterMode ? RegisterModeToggleText : LoginModeToggleText;
        }
    }

    private void SetToggleInteractable(bool interactable)
    {
        if (registerToggleButton != null)
        {
            registerToggleButton.interactable = interactable;
        }
    }

    private void ResolveButtonTextReferences()
    {
        if (loginButtonText == null && loginButton != null)
        {
            loginButtonText = loginButton.GetComponentInChildren<Text>();
        }

        if (registerToggleText == null)
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text text in texts)
            {
                if (text != null && text.text.Trim().Equals(LoginModeToggleText, System.StringComparison.OrdinalIgnoreCase))
                {
                    registerToggleText = text;
                    break;
                }
            }
        }
    }

    private void ResolveRegisterToggleButton()
    {
        if (registerToggleButton != null || registerToggleText == null)
        {
            return;
        }

        registerToggleButton = registerToggleText.GetComponent<Button>();
        if (registerToggleButton == null)
        {
            registerToggleButton = registerToggleText.gameObject.AddComponent<Button>();
            registerToggleButton.targetGraphic = registerToggleText;
        }
    }
}
