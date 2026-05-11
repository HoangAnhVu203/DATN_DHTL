using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PanelRoomTest : MonoBehaviour
{
    [SerializeField] private RoomService roomService;
    [SerializeField] private Button createRoomButton;
    [SerializeField] private TMP_Text roomCodeText;
    [SerializeField] private TMP_Text statusText;

    private void Awake()
    {
        createRoomButton.onClick.AddListener(OnCreateRoomClicked);
    }

    private void OnDestroy()
    {
        createRoomButton.onClick.RemoveListener(OnCreateRoomClicked);
    }

    private void OnCreateRoomClicked()
    {
        createRoomButton.interactable = false;
        SetStatus("Đang tạo phòng...");

        StartCoroutine(roomService.CreateRoom(4, OnCreateRoomCompleted));
    }

    private void OnCreateRoomCompleted(bool success, RoomService.CreateRoomData data, string error)
    {
        createRoomButton.interactable = true;

        if (!success)
        {
            SetStatus("Tạo phòng thất bại.");
            Debug.LogError(error);
            return;
        }

        roomCodeText.text = $"Room Code: {data.room_code}";
        SetStatus("Tạo phòng thành công.");

        Debug.Log($"Room created: {data.room_id} - {data.room_code}");
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }
}
