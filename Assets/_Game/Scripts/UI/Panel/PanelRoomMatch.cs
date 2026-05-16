using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PanelRoomMatch : UICanvas
{
    private const float RefreshInterval = 2f;

    [SerializeField] private Button leaveRoomButton;
    [SerializeField] private Button actionButton;
    [SerializeField] private Text actionButtonText;
    [SerializeField] private TMP_Text roomCodeText;
    [SerializeField] private Sprite defaultAvatarSprite;
    [SerializeField] private Sprite[] localAvatarSprites;
    [SerializeField] private string gameSceneName = "GameScene";

    private readonly List<PlayerSlotView> playerSlots = new List<PlayerSlotView>();
    private RoomService roomService;
    private Coroutine refreshCoroutine;
    private bool localPlayerReady;
    private bool actionInProgress;
    private bool matchCheckInProgress;
    private bool isLoadingMatchScene;

    public void SetRoom(RoomService service, RoomService.RoomData room)
    {
        roomService = service;
        OnlineRoomSession.SetRoom(room);
        ResolveReferences();
        ResolveLocalAvatarSprites();
        RefreshRoomCodeText();
        RefreshActionButton();
        RefreshPlayers();
        StartRefreshing();
    }

    public override void Open()
    {
        base.Open();
        ResolveReferences();
        ResolveLocalAvatarSprites();
        RefreshRoomCodeText();
        RefreshActionButton();

        if (OnlineRoomSession.IsInRoom && roomService != null)
        {
            RefreshPlayers();
            StartRefreshing();
        }
    }

    public override void CloseDirectly()
    {
        StopRefreshing();
        base.CloseDirectly();
    }

    private void OnDestroy()
    {
        StopRefreshing();

        if (leaveRoomButton != null)
        {
            leaveRoomButton.onClick.RemoveListener(OnLeaveRoomClicked);
        }

        if (actionButton != null)
        {
            actionButton.onClick.RemoveListener(OnActionButtonClicked);
        }
    }

    private void ResolveReferences()
    {
        if (playerSlots.Count == 0)
        {
            Transform playerHolder = FindChild(transform, "PlayerHolder");
            Transform searchRoot = playerHolder != null ? playerHolder : transform;

            foreach (Transform child in searchRoot)
            {
                if (child.name.StartsWith("PlayerSlot"))
                {
                    playerSlots.Add(new PlayerSlotView(child, defaultAvatarSprite));
                }
            }
        }

        if (leaveRoomButton == null)
        {
            Transform leaveButtonTransform = FindChild(transform, "LeaveRoomBtn");
            if (leaveButtonTransform != null)
            {
                leaveRoomButton = leaveButtonTransform.GetComponent<Button>();
            }
        }

        if (actionButton == null)
        {
            Transform actionButtonTransform = FindChild(transform, "StartMatch");
            if (actionButtonTransform != null)
            {
                actionButton = actionButtonTransform.GetComponent<Button>();
            }
        }

        if (actionButtonText == null && actionButton != null)
        {
            actionButtonText = actionButton.GetComponentInChildren<Text>(true);
        }

        if (roomCodeText == null)
        {
            Transform roomCodeTransform = FindChild(transform, "RoomCodeText");
            if (roomCodeTransform == null)
            {
                roomCodeTransform = FindChild(transform, "RoomCodetxt");
            }

            if (roomCodeTransform == null)
            {
                roomCodeTransform = FindChild(transform, "RoomCode");
            }

            if (roomCodeTransform != null)
            {
                roomCodeText = roomCodeTransform.GetComponent<TMP_Text>();
            }
        }

        if (leaveRoomButton != null)
        {
            leaveRoomButton.onClick.RemoveListener(OnLeaveRoomClicked);
            leaveRoomButton.onClick.AddListener(OnLeaveRoomClicked);
        }

        if (actionButton != null)
        {
            actionButton.onClick.RemoveListener(OnActionButtonClicked);
            actionButton.onClick.AddListener(OnActionButtonClicked);
        }
    }

    private void StartRefreshing()
    {
        StopRefreshing();
        refreshCoroutine = StartCoroutine(RefreshRoutine());
    }

    private void StopRefreshing()
    {
        if (refreshCoroutine == null)
        {
            return;
        }

        StopCoroutine(refreshCoroutine);
        refreshCoroutine = null;
    }

    private IEnumerator RefreshRoutine()
    {
        while (gameObject.activeInHierarchy && OnlineRoomSession.IsInRoom)
        {
            RefreshPlayers();
            CheckForStartedMatch();
            yield return new WaitForSecondsRealtime(RefreshInterval);
        }
    }

    private void RefreshPlayers()
    {
        if (roomService == null || !OnlineRoomSession.IsInRoom)
        {
            return;
        }

        StartCoroutine(roomService.GetRoomPlayers(OnlineRoomSession.RoomId, OnRoomPlayersLoaded));
    }

    private void OnRoomPlayersLoaded(bool success, List<RoomService.RoomPlayerData> players, string error)
    {
        if (!success)
        {
            Debug.LogError(error);
            return;
        }

        OnlineRoomSession.Players = players ?? new List<RoomService.RoomPlayerData>();
        localPlayerReady = false;

        for (int i = 0; i < playerSlots.Count; i++)
        {
            if (i < OnlineRoomSession.Players.Count)
            {
                RoomService.RoomPlayerData player = OnlineRoomSession.Players[i];
                playerSlots[i].Show(player);

                if (player.user_id == SupabaseSession.UserId)
                {
                    localPlayerReady = player.is_ready;
                }

                if (IsLocalAvatarKey(player.avatar_url))
                {
                    playerSlots[i].SetAvatar(GetLocalAvatarSprite(player.avatar_url));
                }
                else if (!string.IsNullOrWhiteSpace(player.avatar_url)
                         && (player.avatar_url.StartsWith("http://") || player.avatar_url.StartsWith("https://")))
                {
                    StartCoroutine(LoadAvatar(player.avatar_url, playerSlots[i]));
                }
                else
                {
                    playerSlots[i].SetAvatar(GetDefaultAvatarSprite());
                }
            }
            else
            {
                playerSlots[i].Clear();
            }
        }

        RefreshActionButton();
    }

    private void CheckForStartedMatch()
    {
        if (matchCheckInProgress || isLoadingMatchScene || roomService == null || !OnlineRoomSession.IsInRoom)
        {
            return;
        }

        matchCheckInProgress = true;
        StartCoroutine(roomService.GetActiveMatch(OnlineRoomSession.RoomId, OnActiveMatchLoaded));
    }

    private void OnActiveMatchLoaded(bool success, RoomService.MatchData data, string error)
    {
        matchCheckInProgress = false;

        if (!success)
        {
            if (!string.IsNullOrWhiteSpace(error))
            {
                Debug.LogWarning(error);
            }

            return;
        }

        if (data == null || string.IsNullOrWhiteSpace(data.match_id))
        {
            return;
        }

        BeginLoadMatch(data);
    }

    private IEnumerator LoadAvatar(string avatarUrl, PlayerSlotView slot)
    {
        using UnityWebRequest request = UnityWebRequestTexture.GetTexture(avatarUrl);
        yield return request.SendWebRequest();

        if (request.responseCode < 200 || request.responseCode >= 300 || request.result != UnityWebRequest.Result.Success)
        {
            yield break;
        }

        Texture2D texture = DownloadHandlerTexture.GetContent(request);
        if (texture == null)
        {
            yield break;
        }

        Sprite avatarSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        slot.SetAvatar(avatarSprite);
    }

    private void ResolveLocalAvatarSprites()
    {
        if (localAvatarSprites != null && localAvatarSprites.Length > 0)
        {
            return;
        }

        PanelInformation informationPrefab = Resources.Load<PanelInformation>("UI/Panel - Information");
        if (informationPrefab == null)
        {
            localAvatarSprites = defaultAvatarSprite != null ? new[] { defaultAvatarSprite } : new Sprite[0];
            return;
        }

        Transform avatarHolder = FindChild(informationPrefab.transform, "SelectAvatarHolder");
        if (avatarHolder == null)
        {
            localAvatarSprites = defaultAvatarSprite != null ? new[] { defaultAvatarSprite } : new Sprite[0];
            return;
        }

        List<Sprite> sprites = new List<Sprite>();
        Button[] avatarButtons = avatarHolder.GetComponentsInChildren<Button>(true);

        foreach (Button avatarButton in avatarButtons)
        {
            Image avatarImage = avatarButton.GetComponent<Image>();
            if (avatarImage != null && avatarImage.sprite != null && !sprites.Contains(avatarImage.sprite))
            {
                sprites.Add(avatarImage.sprite);
            }
        }

        localAvatarSprites = sprites.ToArray();
    }

    private bool IsLocalAvatarKey(string avatarKey)
    {
        return !string.IsNullOrWhiteSpace(avatarKey) && avatarKey.StartsWith("avatar_");
    }

    private Sprite GetLocalAvatarSprite(string avatarKey)
    {
        ResolveLocalAvatarSprites();

        if (localAvatarSprites == null || localAvatarSprites.Length == 0)
        {
            return GetDefaultAvatarSprite();
        }

        string indexText = avatarKey.Substring("avatar_".Length);
        if (!int.TryParse(indexText, out int index) || index < 0 || index >= localAvatarSprites.Length)
        {
            return GetDefaultAvatarSprite();
        }

        return localAvatarSprites[index];
    }

    private Sprite GetDefaultAvatarSprite()
    {
        if (defaultAvatarSprite != null)
        {
            return defaultAvatarSprite;
        }

        ResolveLocalAvatarSprites();
        return localAvatarSprites != null && localAvatarSprites.Length > 0 ? localAvatarSprites[0] : null;
    }

    private void OnLeaveRoomClicked()
    {
        if (actionInProgress || roomService == null || !OnlineRoomSession.IsInRoom)
        {
            return;
        }

        actionInProgress = true;
        SetButtonsInteractable(false);
        StartCoroutine(roomService.LeaveRoom(OnlineRoomSession.RoomId, OnLeaveRoomCompleted));
    }

    private void OnLeaveRoomCompleted(bool success, string error)
    {
        actionInProgress = false;
        SetButtonsInteractable(true);

        if (!success)
        {
            Debug.LogError(error);
            return;
        }

        OnlineRoomSession.Clear();
        CloseDirectly();
    }

    private void OnActionButtonClicked()
    {
        if (actionInProgress || roomService == null || !OnlineRoomSession.IsInRoom)
        {
            return;
        }

        actionInProgress = true;
        SetButtonsInteractable(false);

        if (OnlineRoomSession.IsHost)
        {
            StartCoroutine(StartMatchAsHostRoutine());
            return;
        }

        StartCoroutine(roomService.SetReady(OnlineRoomSession.RoomId, !localPlayerReady, OnReadyCompleted));
    }

    private IEnumerator StartMatchAsHostRoutine()
    {
        bool hostReadySuccess = false;
        string hostReadyError = null;

        yield return roomService.SetReady(OnlineRoomSession.RoomId, true, (success, data, error) =>
        {
            hostReadySuccess = success;
            hostReadyError = error;
        });

        if (!hostReadySuccess)
        {
            actionInProgress = false;
            SetButtonsInteractable(true);
            Debug.LogError(hostReadyError);
            yield break;
        }

        yield return roomService.StartMatch(OnlineRoomSession.RoomId, OnStartMatchCompleted);
    }

    private void OnReadyCompleted(bool success, RoomService.ReadyData data, string error)
    {
        actionInProgress = false;
        SetButtonsInteractable(true);

        if (!success)
        {
            Debug.LogError(error);
            return;
        }

        localPlayerReady = data != null && data.is_ready;
        RefreshActionButton();
        RefreshPlayers();
    }

    private void OnStartMatchCompleted(bool success, RoomService.MatchData data, string error)
    {
        actionInProgress = false;
        SetButtonsInteractable(true);

        if (!success)
        {
            Debug.LogError(error);
            return;
        }

        if (data == null || string.IsNullOrWhiteSpace(data.match_id))
        {
            Debug.LogError("Start match succeeded but match data is empty.");
            return;
        }

        Debug.Log($"Match started: {data.match_id} - seed {data.seed}");
        BeginLoadMatch(data);
    }

    private void BeginLoadMatch(RoomService.MatchData match)
    {
        if (isLoadingMatchScene)
        {
            return;
        }

        OnlineRoomSession.SetMatch(match);
        isLoadingMatchScene = true;
        StopRefreshing();
        SetButtonsInteractable(false);
        SceneManager.LoadScene(gameSceneName);
    }

    private void RefreshActionButton()
    {
        if (actionButtonText == null)
        {
            return;
        }

        if (OnlineRoomSession.IsHost)
        {
            actionButtonText.text = "Start Match";
            return;
        }

        actionButtonText.text = localPlayerReady ? "Unready" : "Ready";
    }

    private void RefreshRoomCodeText()
    {
        if (roomCodeText == null)
        {
            return;
        }

        roomCodeText.text = string.IsNullOrWhiteSpace(OnlineRoomSession.RoomCode)
            ? "Room Code: ---"
            : $"Room Code: {OnlineRoomSession.RoomCode}";
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (leaveRoomButton != null)
        {
            leaveRoomButton.interactable = interactable;
        }

        if (actionButton != null)
        {
            actionButton.interactable = interactable;
        }
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

    private class PlayerSlotView
    {
        private readonly GameObject root;
        private readonly Image avatarImage;
        private readonly Text nameText;
        private readonly Sprite defaultAvatarSprite;

        public PlayerSlotView(Transform slotTransform, Sprite fallbackAvatar)
        {
            root = slotTransform.gameObject;
            avatarImage = FindInChildren<Image>(slotTransform, "PlayerAvatar");
            nameText = FindInChildren<Text>(slotTransform, "PlayerName");
            defaultAvatarSprite = fallbackAvatar != null ? fallbackAvatar : avatarImage != null ? avatarImage.sprite : null;
            Clear();
        }

        public void Show(RoomService.RoomPlayerData player)
        {
            root.SetActive(true);

            if (nameText != null)
            {
                string label = player.display_name;

                if (player.is_host)
                {
                    label += " (Host)";
                }
                else if (player.is_ready)
                {
                    label += " (Ready)";
                }

                nameText.text = label;
            }

            SetAvatar(defaultAvatarSprite);
        }

        public void Clear()
        {
            root.SetActive(false);

            if (nameText != null)
            {
                nameText.text = string.Empty;
            }

            SetAvatar(defaultAvatarSprite);
        }

        public void SetAvatar(Sprite avatar)
        {
            if (avatarImage != null)
            {
                avatarImage.sprite = avatar;
                avatarImage.enabled = avatar != null;
            }
        }

        private static T FindInChildren<T>(Transform root, string childName) where T : Component
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == childName)
                {
                    return child.GetComponent<T>();
                }
            }

            return root.GetComponentInChildren<T>(true);
        }
    }
}
