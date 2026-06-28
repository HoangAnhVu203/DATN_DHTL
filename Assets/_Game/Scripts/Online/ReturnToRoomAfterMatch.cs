using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ReturnToRoomAfterMatch : MonoBehaviour
{
    private const string HomeSceneName = "HomeScene";
    private const float LoadingCompleteHoldSeconds = 0.2f;

    private bool isReturning;
    private string returningRoomId;
    private string returningMatchId;
    private string returningResult;
    private bool returningPlayerWasHost;

    // Starts the return process.
    public static void StartReturn()
    {
        ReturnToRoomAfterMatch existingRunner = FindFirstObjectByType<ReturnToRoomAfterMatch>();
        if (existingRunner != null)
        {
            existingRunner.ReturnToRoom();
            return;
        }

        GameObject runnerObject = new GameObject(nameof(ReturnToRoomAfterMatch));
        DontDestroyOnLoad(runnerObject);
        runnerObject.AddComponent<ReturnToRoomAfterMatch>().ReturnToRoom();
    }

    // Returns the player to the room after a match.
    public void ReturnToRoom()
    {
        if (isReturning)
        {
            return;
        }

        isReturning = true;
        StartCoroutine(ReturnRoutine());
    }

    // Runs the room return cleanup and loading steps.
    private IEnumerator ReturnRoutine()
    {
        Time.timeScale = 1f;
        OnlineMatchLoadingOverlay.Show(0f);

        returningRoomId = OnlineRoomSession.RoomId;
        returningMatchId = OnlineRoomSession.MatchId;
        returningResult = ResolveMatchResult();
        returningPlayerWasHost = OnlineRoomSession.IsHost;

        OnlineRoomSession.MarkCurrentMatchCompleted();
        OnlineRoomSession.ClearMatch();

        yield return null;
        OnlineMatchLoadingOverlay.SetProgress(0.05f);

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(HomeSceneName);
        if (loadOperation == null)
        {
            Debug.LogError($"ReturnToRoomAfterMatch: could not load scene '{HomeSceneName}'.");
            OnlineMatchLoadingOverlay.Hide();
            Destroy(gameObject);
            yield break;
        }

        while (!loadOperation.isDone)
        {
            float sceneProgress = Mathf.Clamp01(loadOperation.progress / 0.9f);
            OnlineMatchLoadingOverlay.SetProgress(Mathf.Lerp(0.05f, 0.6f, sceneProgress));
            yield return null;
        }

        OnlineMatchLoadingOverlay.SetProgress(0.65f);
        yield return null;

        yield return ResetRoomStateAfterMatch();
        OnlineMatchLoadingOverlay.SetProgress(0.85f);

        OpenRoomPanel();
        OnlineMatchLoadingOverlay.SetProgress(0.95f);

        yield return null;
        OnlineMatchLoadingOverlay.SetProgress(1f);

        if (LoadingCompleteHoldSeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(LoadingCompleteHoldSeconds);
        }

        OnlineMatchLoadingOverlay.Hide();
        Destroy(gameObject);
    }

    // Resets the room state after match.
    private IEnumerator ResetRoomStateAfterMatch()
    {
        if (string.IsNullOrWhiteSpace(returningRoomId))
        {
            yield break;
        }

        RoomService roomService = FindFirstObjectByType<RoomService>();
        if (roomService == null)
        {
            yield break;
        }

        bool requestFinished = false;
        bool requestSuccess = false;
        string requestError = null;

        yield return roomService.ResetRoomAfterMatch(returningRoomId, returningMatchId, returningResult, returningPlayerWasHost, (success, error) =>
        {
            requestFinished = true;
            requestSuccess = success;
            requestError = error;
        });

        if (!requestFinished || !requestSuccess)
        {
            Debug.LogWarning($"ReturnToRoomAfterMatch: reset room after match had errors. {requestError}");
        }
        else
        {
            OnlineRoomSession.Status = "waiting";
        }

        if (!requestSuccess)
        {
            bool localReadyReset = false;
            yield return roomService.ForceResetLocalReady(returningRoomId, (success, error) =>
            {
                localReadyReset = success;
                requestError = error;
            });

            if (!localReadyReset)
            {
                Debug.LogWarning($"ReturnToRoomAfterMatch: failed to force reset local ready. {requestError}");
            }
        }

        if (OnlineRoomSession.Players != null)
        {
            foreach (RoomService.RoomPlayerData player in OnlineRoomSession.Players)
            {
                if (player == null)
                {
                    continue;
                }

                if (requestSuccess || returningPlayerWasHost || player.user_id == SupabaseSession.UserId)
                {
                    player.is_ready = false;
                }
            }
        }
    }

    // Opens the room panel UI.
    private void OpenRoomPanel()
    {
        if (!OnlineRoomSession.IsInRoom)
        {
            Debug.LogWarning("ReturnToRoomAfterMatch: no active room session to return to.");
            return;
        }

        RoomService roomService = FindFirstObjectByType<RoomService>();
        if (roomService == null)
        {
            Debug.LogWarning("ReturnToRoomAfterMatch: no RoomService found in HomeScene.");
            return;
        }

        PanelRoomMatch panel = OpenRoomMatchPanel();
        if (panel == null)
        {
            Debug.LogWarning("ReturnToRoomAfterMatch: could not open PanelRoomMatch.");
            return;
        }

        panel.SetRoom(roomService, BuildCurrentRoomData());
    }

    // Chooses the result string to send back to the room service.
    private string ResolveMatchResult()
    {
        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.CurrentState == GameState.Victory)
            {
                return "win";
            }

            if (GameManager.Instance.CurrentState == GameState.Lose)
            {
                return "lose";
            }
        }

        return "finished";
    }

    // Opens the room match panel UI.
    private PanelRoomMatch OpenRoomMatchPanel()
    {
        UIManager uiManager = FindFirstObjectByType<UIManager>();
        if (uiManager != null)
        {
            return uiManager.OpenUI<PanelRoomMatch>();
        }

        PanelRoomMatch prefab = Resources.Load<PanelRoomMatch>("UI/Panel - RoomMatch");
        if (prefab == null)
        {
            return null;
        }

        Canvas canvas = FindFirstObjectByType<Canvas>();
        Transform parent = canvas != null ? canvas.transform : null;
        PanelRoomMatch panel = Instantiate(prefab, parent);
        panel.Open();
        return panel;
    }

    // Builds the current room data.
    private RoomService.RoomData BuildCurrentRoomData()
    {
        return new RoomService.RoomData
        {
            room_id = OnlineRoomSession.RoomId,
            room_code = OnlineRoomSession.RoomCode,
            host_id = OnlineRoomSession.HostId,
            status = OnlineRoomSession.Status,
            max_players = OnlineRoomSession.MaxPlayers
        };
    }
}
