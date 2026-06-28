using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
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
    private bool roomRefreshInProgress;

    // Stores the room service and room data for this panel.
    public void SetRoom(RoomService service, RoomService.RoomData room)
    {
        roomService = service;
        OnlineRoomSession.SetRoom(room);
        ResetRuntimeState();
        ResolveReferences();
        ResolveLocalAvatarSprites();
        SetButtonsInteractable(true);
        RefreshRoomCodeText();
        RefreshActionButton();
        RefreshPlayers();
        StartRefreshing();
    }

    // Shows this panel and refreshes its visible state.
    public override void Open()
    {
        base.Open();
        ResolveReferences();
        ResolveLocalAvatarSprites();
        SetButtonsInteractable(true);
        RefreshRoomCodeText();
        RefreshActionButton();

        if (OnlineRoomSession.IsInRoom && roomService != null)
        {
            RefreshPlayers();
            StartRefreshing();
        }
    }

    // Closes this panel immediately.
    public override void CloseDirectly()
    {
        StopRefreshing();
        base.CloseDirectly();
    }

    // Removes listeners and runtime resources before destruction.
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

    // Resets the runtime state.
    private void ResetRuntimeState()
    {
        StopRefreshing();
        localPlayerReady = false;
        actionInProgress = false;
        matchCheckInProgress = false;
        isLoadingMatchScene = false;
        roomRefreshInProgress = false;
    }

    // Starts polling room state.
    private void StartRefreshing()
    {
        StopRefreshing();
        refreshCoroutine = StartCoroutine(RefreshRoutine());
    }

    // Stops the refreshing process.
    private void StopRefreshing()
    {
        if (refreshCoroutine == null)
        {
            return;
        }

        StopCoroutine(refreshCoroutine);
        refreshCoroutine = null;
    }

    // Runs the refresh coroutine.
    private IEnumerator RefreshRoutine()
    {
        while (gameObject.activeInHierarchy && OnlineRoomSession.IsInRoom)
        {
            RefreshRoomState();
            CheckForStartedMatch();
            yield return new WaitForSecondsRealtime(RefreshInterval);
        }
    }

    // Refreshes the room state.
    private void RefreshRoomState()
    {
        if (roomRefreshInProgress || roomService == null || !OnlineRoomSession.IsInRoom)
        {
            return;
        }

        StartCoroutine(RefreshRoomStateRoutine());
    }

    // Runs the refresh room state coroutine.
    private IEnumerator RefreshRoomStateRoutine()
    {
        roomRefreshInProgress = true;

        yield return roomService.SendRoomHeartbeat(OnlineRoomSession.RoomId, (success, error) =>
        {
            if (!success && !string.IsNullOrWhiteSpace(error))
            {
                Debug.LogWarning(error);
            }
        });

        RefreshPlayers();
        roomRefreshInProgress = false;
    }

    // Refreshes the players.
    private void RefreshPlayers()
    {
        if (roomService == null || !OnlineRoomSession.IsInRoom)
        {
            return;
        }

        StartCoroutine(roomService.GetRoomPlayers(OnlineRoomSession.RoomId, OnRoomPlayersLoaded));
    }

    // Handles the loaded room players data.
    private void OnRoomPlayersLoaded(bool success, List<RoomService.RoomPlayerData> players, string error)
    {
        if (!success)
        {
            Debug.LogError(error);
            return;
        }

        OnlineRoomSession.Players = players ?? new List<RoomService.RoomPlayerData>();
        localPlayerReady = false;
        bool localPlayerStillInRoom = false;
        SyncHostFromPlayers(OnlineRoomSession.Players);

        for (int i = 0; i < playerSlots.Count; i++)
        {
            if (i < OnlineRoomSession.Players.Count)
            {
                RoomService.RoomPlayerData player = OnlineRoomSession.Players[i];
                playerSlots[i].Show(player);

                if (player.user_id == SupabaseSession.UserId)
                {
                    localPlayerReady = player.is_ready;
                    localPlayerStillInRoom = true;
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

        if (OnlineRoomSession.Players.Count > 0 && !localPlayerStillInRoom)
        {
            OnlineRoomSession.Clear();
            CloseDirectly();
            return;
        }

        RefreshActionButton();
    }

    // Updates host state from the room player list.
    private void SyncHostFromPlayers(List<RoomService.RoomPlayerData> players)
    {
        if (players == null)
        {
            return;
        }

        foreach (RoomService.RoomPlayerData player in players)
        {
            if (player != null && player.is_host)
            {
                OnlineRoomSession.HostId = player.user_id;
                return;
            }
        }
    }

    // Checks whether the room already has a started match.
    private void CheckForStartedMatch()
    {
        if (matchCheckInProgress || isLoadingMatchScene || roomService == null || !OnlineRoomSession.IsInRoom)
        {
            return;
        }

        matchCheckInProgress = true;
        StartCoroutine(roomService.GetActiveMatch(OnlineRoomSession.RoomId, OnActiveMatchLoaded));
    }

    // Handles the loaded active match data.
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

        if (!string.IsNullOrWhiteSpace(OnlineRoomSession.LastCompletedMatchId)
            && data.match_id == OnlineRoomSession.LastCompletedMatchId)
        {
            return;
        }

        BeginLoadMatch(data);
    }

    // Loads the avatar.
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

    // Checks whether the avatar key points to a local sprite.
    private bool IsLocalAvatarKey(string avatarKey)
    {
        return !string.IsNullOrWhiteSpace(avatarKey) && avatarKey.StartsWith("avatar_");
    }

    // Returns the local avatar sprite.
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

    // Returns the default avatar sprite.
    private Sprite GetDefaultAvatarSprite()
    {
        if (defaultAvatarSprite != null)
        {
            return defaultAvatarSprite;
        }

        ResolveLocalAvatarSprites();
        return localAvatarSprites != null && localAvatarSprites.Length > 0 ? localAvatarSprites[0] : null;
    }

    // Handles the leave room click.
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

    // Handles the leave room request result.
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

    // Handles the action button click.
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

    // Runs the start match as host coroutine.
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

    // Handles the ready request result.
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

    // Handles the start match request result.
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

    // Starts loading the active match scene.
    private void BeginLoadMatch(RoomService.MatchData match)
    {
        if (isLoadingMatchScene)
        {
            return;
        }

        OnlineRoomSession.SetMatch(match);
        OnlineRoomSession.CacheExpectedMatchPlayerCount();
        isLoadingMatchScene = true;
        StopRefreshing();
        SetButtonsInteractable(false);
        OnlineMatchLoadingOverlay.LoadScene(gameSceneName);
    }

    // Refreshes the action button.
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

    // Refreshes the room code text.
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

    // Updates the buttons interactable.
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

        // Plays the er slot view.
        public PlayerSlotView(Transform slotTransform, Sprite fallbackAvatar)
        {
            root = slotTransform.gameObject;
            avatarImage = FindInChildren<Image>(slotTransform, "PlayerAvatar");
            nameText = FindInChildren<Text>(slotTransform, "PlayerName");
            defaultAvatarSprite = fallbackAvatar != null ? fallbackAvatar : avatarImage != null ? avatarImage.sprite : null;
            Clear();
        }

        // Displays this view with the supplied data.
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

        // Resets this view back to an empty state.
        public void Clear()
        {
            root.SetActive(false);

            if (nameText != null)
            {
                nameText.text = string.Empty;
            }

            SetAvatar(defaultAvatarSprite);
        }

        // Updates the avatar.
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
