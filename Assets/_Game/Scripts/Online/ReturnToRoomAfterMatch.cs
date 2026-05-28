using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ReturnToRoomAfterMatch : MonoBehaviour
{
    private const string HomeSceneName = "HomeScene";

    private bool isReturning;
    private string returningRoomId;
    private string returningMatchId;
    private string returningResult;
    private bool returningPlayerWasHost;

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

    public void ReturnToRoom()
    {
        if (isReturning)
        {
            return;
        }

        isReturning = true;
        StartCoroutine(ReturnRoutine());
    }

    private IEnumerator ReturnRoutine()
    {
        Time.timeScale = 1f;

        returningRoomId = OnlineRoomSession.RoomId;
        returningMatchId = OnlineRoomSession.MatchId;
        returningResult = ResolveMatchResult();
        returningPlayerWasHost = OnlineRoomSession.IsHost;

        OnlineRoomSession.MarkCurrentMatchCompleted();
        OnlineRoomSession.ClearMatch();

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(HomeSceneName);
        while (loadOperation != null && !loadOperation.isDone)
        {
            yield return null;
        }

        yield return null;
        yield return ResetRoomStateAfterMatch();
        OpenRoomPanel();
        Destroy(gameObject);
    }

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
