using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PanelLogin : MonoBehaviour
{
    private const string DefaultDisplayNameObjectName = "PlayerDisplayName";

    [SerializeField] private AuthService authService;
    [SerializeField] private TMP_InputField emailInput;
    [SerializeField] private TMP_InputField passwordInput;
    [SerializeField] private Button loginButton;
    [SerializeField] private Text statusText;
    [SerializeField] private TMP_Text displayNameText;
    [SerializeField] private GameObject panelToHideOnSuccess;

    private void Awake()
    {
        loginButton.onClick.AddListener(OnLoginClicked);

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
    }

    private void OnDestroy()
    {
        loginButton.onClick.RemoveListener(OnLoginClicked);
    }

    private void OnLoginClicked()
    {
        string email = emailInput.text.Trim();
        string password = passwordInput.text;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            SetStatus("Nhập email và mật khẩu.");
            return;
        }

        loginButton.interactable = false;
        SetStatus("Đang đăng nhập...");

        StartCoroutine(authService.SignIn(email, password, OnLoginCompleted));
    }

    private void OnLoginCompleted(bool success, string message)
    {
        loginButton.interactable = true;

        if (!success)
        {
            SetStatus(GetUserFriendlyError(message));
            Debug.LogError(message);
            return;
        }

        SetStatus("Đăng nhập thành công.");
        UpdateDisplayName();

        if (panelToHideOnSuccess != null)
        {
            panelToHideOnSuccess.SetActive(false);
        }

        Debug.Log($"Logged in: {SupabaseSession.UserId} - {SupabaseSession.DisplayName}");
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    private string GetUserFriendlyError(string error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return "Đăng nhập thất bại.";
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

        if (lowerError.Contains("supabase config"))
        {
            return "Thiếu cấu hình Supabase.";
        }

        if (lowerError.Contains("400"))
        {
            return "Thông tin đăng nhập không hợp lệ.";
        }

        return "Đăng nhập thất bại. Xem Console để biết chi tiết.";
    }

    private void UpdateDisplayName()
    {
        if (displayNameText == null)
        {
            return;
        }

        displayNameText.text = SupabaseSession.DisplayName;
    }
}
