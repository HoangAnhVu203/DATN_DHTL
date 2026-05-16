using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PanelRoomTest : MonoBehaviour
{
    [SerializeField] private RoomService roomService;
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button joinRoomButton;
    [SerializeField] private TMP_InputField joinRoomCodeInput;
    [SerializeField] private TMP_Text roomCodeText;
    [SerializeField] private TMP_Text statusText;

    private PanelRoomMatch activeRoomMatchPanel;

    private void Awake()
    {
        ResolveSceneReferences();
        createRoomButton.onClick.AddListener(OnCreateRoomClicked);

        if (joinRoomButton != null)
        {
            joinRoomButton.onClick.AddListener(OnJoinRoomClicked);
        }
    }

    private void OnDestroy()
    {
        createRoomButton.onClick.RemoveListener(OnCreateRoomClicked);

        if (joinRoomButton != null)
        {
            joinRoomButton.onClick.RemoveListener(OnJoinRoomClicked);
        }
    }

    private void OnCreateRoomClicked()
    {
        createRoomButton.interactable = false;
        SetStatus("Đang tạo phòng...");

        StartCoroutine(roomService.CreateRoom(4, OnCreateRoomCompleted));
    }

    private void OnJoinRoomClicked()
    {
        if (joinRoomCodeInput == null)
        {
            SetStatus("Thiếu ô nhập room code.");
            return;
        }

        string roomCode = joinRoomCodeInput.text.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(roomCode))
        {
            SetStatus("Nhập room code.");
            return;
        }

        SetButtonsInteractable(false);
        SetStatus("Đang vào phòng...");

        StartCoroutine(roomService.JoinRoom(roomCode, OnJoinRoomCompleted));
    }

    private void OnCreateRoomCompleted(bool success, RoomService.RoomData data, string error)
    {
        SetButtonsInteractable(true);

        if (!success)
        {
            SetStatus("Tạo phòng thất bại.");
            Debug.LogError(error);
            return;
        }

        roomCodeText.text = $"Room Code: {data.room_code}";
        SetStatus("Tạo phòng thành công.");

        Debug.Log($"Room created: {data.room_id} - {data.room_code}");
        EnterRoom(data);
    }

    private void OnJoinRoomCompleted(bool success, RoomService.RoomData data, string error)
    {
        SetButtonsInteractable(true);

        if (!success)
        {
            SetStatus("Vào phòng thất bại.");
            Debug.LogError(error);
            return;
        }

        roomCodeText.text = $"Room Code: {data.room_code}";
        SetStatus("Vào phòng thành công.");

        Debug.Log($"Joined room: {data.room_id} - {data.room_code}");
        EnterRoom(data);
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    private void EnterRoom(RoomService.RoomData data)
    {
        OnlineRoomSession.SetRoom(data);
        PanelRoomMatch panel = OpenRoomMatchPanel();
        panel.SetRoom(roomService, data);
        activeRoomMatchPanel = panel;
    }

    private PanelRoomMatch OpenRoomMatchPanel()
    {
        UIManager uiManager = FindFirstObjectByType<UIManager>();
        if (uiManager != null)
        {
            return uiManager.OpenUI<PanelRoomMatch>();
        }

        if (activeRoomMatchPanel != null)
        {
            activeRoomMatchPanel.Open();
            return activeRoomMatchPanel;
        }

        PanelRoomMatch prefab = Resources.Load<PanelRoomMatch>("UI/Panel - RoomMatch");
        Canvas canvas = GetComponentInParent<Canvas>();
        Transform parent = canvas != null ? canvas.transform : transform;
        activeRoomMatchPanel = Instantiate(prefab, parent);
        activeRoomMatchPanel.Open();
        return activeRoomMatchPanel;
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (createRoomButton != null)
        {
            createRoomButton.interactable = interactable;
        }

        if (joinRoomButton != null)
        {
            joinRoomButton.interactable = interactable;
        }
    }

    private void ResolveSceneReferences()
    {
        if (joinRoomButton == null)
        {
            GameObject joinButtonObject = GameObject.Find("JoinRoomBtn");
            if (joinButtonObject != null)
            {
                joinRoomButton = joinButtonObject.GetComponent<Button>();
            }
        }

        if (joinRoomCodeInput == null)
        {
            TMP_InputField[] inputs = FindObjectsByType<TMP_InputField>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (TMP_InputField input in inputs)
            {
                if (input != null && input.GetComponentInParent<PanelLogin>() == null)
                {
                    joinRoomCodeInput = input;
                    break;
                }
            }
        }
    }
}
