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

    // Sets up this component before gameplay starts.
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

    // Restores runtime state when this component becomes active.
    private void OnEnable()
    {
        if (!SupabaseSession.IsLoggedIn)
        {
            ResetForLoggedOut();
        }

        HideIfAlreadyLoggedIn();
    }

    // Removes listeners and runtime resources before destruction.
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

    // Handles the submit click.
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

    // Handles the login request result.
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

    // Resets the for logged out.
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

    // Starts the login loading process.
    private void StartLoginLoading()
    {
        StopLoginLoading();
        OnlineMatchLoadingOverlay.Show(0.05f);
        loginLoadingCoroutine = StartCoroutine(LoginLoadingRoutine());
    }

    // Stops the login loading process.
    private void StopLoginLoading()
    {
        if (loginLoadingCoroutine == null)
        {
            return;
        }

        StopCoroutine(loginLoadingCoroutine);
        loginLoadingCoroutine = null;
    }

    // Runs the login loading coroutine.
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

    // Handles the register request result.
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

    // Writes a short status message to the UI.
    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    // Returns the user friendly error.
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

    // Updates the display name.
    private void UpdateDisplayName()
    {
        if (displayNameText == null)
        {
            return;
        }

        displayNameText.text = SupabaseSession.DisplayName;
    }

    // Hides the if already logged in.
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

    // Runs the toggle mode step.
    private void ToggleMode()
    {
        isRegisterMode = !isRegisterMode;
        SetStatus(string.Empty);
        RefreshModeText();
    }

    // Clears the input fields.
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

    // Refreshes the mode text.
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

    // Updates the toggle interactable.
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
