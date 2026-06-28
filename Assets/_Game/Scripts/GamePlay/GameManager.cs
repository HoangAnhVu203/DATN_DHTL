using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private GameState initialState = GameState.Loading;
    [SerializeField] private GameState stateAfterLoading = GameState.StartMatch;
    [SerializeField] private float loadingDuration = 0.5f;
    [SerializeField] private float loseDelayAfterPlayerDeath = 5f;
    [SerializeField] private float victorySlowMotionDuration = 2f;
    [SerializeField] private float victorySlowMotionScale = 0.2f;
    [SerializeField] private bool autoFindSceneObjects = true;
    [SerializeField] private bool openGameplayUIOnStartMatch = true;
    [SerializeField] private Player player;
    [SerializeField] private Spawner[] spawners;

    private Coroutine loadingCoroutine;
    private Coroutine delayedLoseCoroutine;
    private Coroutine victorySlowMotionCoroutine;
    private bool hasFinishedMatch;
    private bool hasEnteredState;

    public GameState CurrentState { get; private set; }
    public bool IsPlaying => CurrentState == GameState.StartMatch;
    public bool IsPaused => CurrentState == GameState.Pause;
    public event Action<GameState, GameState> StateChanged;

    // Sets up this component before gameplay starts.
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (autoFindSceneObjects)
        {
            CacheSceneObjects();
        }
    }

    // Runs the first scene-time setup for this object.
    private void Start()
    {
        SubscribeSceneEvents();
        ChangeState(initialState);
    }

    // Removes listeners and runtime resources before destruction.
    private void OnDestroy()
    {
        StopDelayedLose();
        StopVictorySlowMotion(resetTimeScale: false);
        UnsubscribeSceneEvents();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    // Switches to the requested game state.
    public void ChangeState(GameState newState)
    {
        if (hasEnteredState && CurrentState == newState)
        {
            return;
        }

        GameState previousState = CurrentState;
        if (hasEnteredState)
        {
            ExitState(previousState);
        }

        CurrentState = newState;
        hasEnteredState = true;
        EnterState(newState);
        StateChanged?.Invoke(previousState, newState);
    }

    // Navigates to home.
    public void GoHome()
    {
        hasFinishedMatch = false;
        StopDelayedLose();
        StopVictorySlowMotion(resetTimeScale: true);
        ChangeState(GameState.Home);
    }

    // Moves the room into a running match.
    public void StartMatch()
    {
        hasFinishedMatch = false;
        StopDelayedLose();
        StopVictorySlowMotion(resetTimeScale: true);
        ChangeState(GameState.StartMatch);
    }

    // Starts the mathc process.
    public void StartMathc()
    {
        StartMatch();
    }

    // Pauses gameplay and opens the pause state.
    public void Pause()
    {
        if (CurrentState != GameState.StartMatch)
        {
            return;
        }

        ChangeState(GameState.Pause);
    }

    // Resumes gameplay from pause.
    public void Resume()
    {
        if (CurrentState != GameState.Pause)
        {
            return;
        }

        ChangeState(GameState.StartMatch);
    }

    // Marks the active match as ended with its result.
    public void EndMatch()
    {
        ChangeState(GameState.EndMatch);
    }

    // Handles the win result for the match.
    public void Victory()
    {
        FinishMatch(GameState.Victory);
    }

    // Handles the lose result for the match.
    public void Lose()
    {
        FinishMatch(GameState.Lose);
    }

    // Restarts the current level.
    public void RestartLevel()
    {
        Time.timeScale = 1f;
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex);
    }

    // Loads the requested scene.
    public void LoadScene(int buildIndex)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(buildIndex);
    }

    // Requests the application to quit.
    public void QuitGame()
    {
        Application.Quit();
    }

    // Enters the requested game state.
    private void EnterState(GameState state)
    {
        switch (state)
        {
            case GameState.Loading:
                EnterLoading();
                break;

            case GameState.Home:
                Time.timeScale = 0f;
                CloseGameplayUI();
                break;

            case GameState.StartMatch:
                StopVictorySlowMotion(resetTimeScale: false);
                Time.timeScale = 1f;
                OpenGameplayUI();
                break;

            case GameState.Pause:
                Time.timeScale = 0f;
                break;

            case GameState.EndMatch:
                Time.timeScale = 0f;
                CloseGameplayUI();
                break;

            case GameState.Victory:
                EnterVictory();
                break;

            case GameState.Lose:
                StopVictorySlowMotion(resetTimeScale: false);
                Time.timeScale = 0f;
                CloseGameplayUI();
                OpenGameOverUI();
                break;
        }
    }

    // Exits the requested game state.
    private void ExitState(GameState state)
    {
        if (state == GameState.Loading && loadingCoroutine != null)
        {
            StopCoroutine(loadingCoroutine);
            loadingCoroutine = null;
        }

        if (state == GameState.Victory)
        {
            StopVictorySlowMotion(resetTimeScale: false);
        }
    }

    // Enters the loading state.
    private void EnterLoading()
    {
        Time.timeScale = 1f;
        CloseGameplayUI();

        if (loadingCoroutine != null)
        {
            StopCoroutine(loadingCoroutine);
        }

        loadingCoroutine = StartCoroutine(LoadingRoutine());
    }

    // Runs the loading coroutine.
    private IEnumerator LoadingRoutine()
    {
        if (loadingDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(loadingDuration);
        }

        loadingCoroutine = null;
        ChangeState(stateAfterLoading);
    }

    // Enters the victory state.
    private void EnterVictory()
    {
        StopVictorySlowMotion(resetTimeScale: false);

        if (victorySlowMotionDuration <= 0f)
        {
            Time.timeScale = 0f;
            CloseGameplayUI();
            OpenGameIsFinishedUI();
            return;
        }

        victorySlowMotionCoroutine = StartCoroutine(VictorySlowMotionRoutine());
    }

    // Runs the victory slow motion coroutine.
    private IEnumerator VictorySlowMotionRoutine()
    {
        Time.timeScale = Mathf.Clamp(victorySlowMotionScale, 0.01f, 1f);

        yield return new WaitForSecondsRealtime(victorySlowMotionDuration);

        victorySlowMotionCoroutine = null;
        Time.timeScale = 0f;
        CloseGameplayUI();
        OpenGameIsFinishedUI();
    }

    // Finishes the match step.
    private void FinishMatch(GameState resultState)
    {
        if (hasFinishedMatch)
        {
            return;
        }

        hasFinishedMatch = true;
        ChangeState(GameState.EndMatch);
        ChangeState(resultState);
    }

    private void CacheSceneObjects()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<Player>();
        }

        if (spawners == null || spawners.Length == 0)
        {
            spawners = FindObjectsByType<Spawner>(FindObjectsSortMode.None);
        }
    }

    // Subscribes to scene events events.
    private void SubscribeSceneEvents()
    {
        if (player != null)
        {
            player.Died += OnPlayerDied;
        }

        if (spawners == null)
        {
            return;
        }

        foreach (Spawner spawner in spawners)
        {
            if (spawner != null)
            {
                spawner.Cleared += OnSpawnerCleared;
            }
        }
    }

    // Unsubscribes from scene events events.
    private void UnsubscribeSceneEvents()
    {
        if (player != null)
        {
            player.Died -= OnPlayerDied;
        }

        if (spawners == null)
        {
            return;
        }

        foreach (Spawner spawner in spawners)
        {
            if (spawner != null)
            {
                spawner.Cleared -= OnSpawnerCleared;
            }
        }
    }

    // Handles the player died callback.
    private void OnPlayerDied(Character deadCharacter)
    {
        if (NetworkMatchManager.IsOnlineMatchActive())
        {
            return;
        }

        if (delayedLoseCoroutine != null || hasFinishedMatch)
        {
            return;
        }

        delayedLoseCoroutine = StartCoroutine(DelayLoseAfterPlayerDeath());
    }

    // Runs the delay lose after player death step.
    private IEnumerator DelayLoseAfterPlayerDeath()
    {
        if (loseDelayAfterPlayerDeath > 0f)
        {
            yield return new WaitForSeconds(loseDelayAfterPlayerDeath);
        }

        delayedLoseCoroutine = null;
        Lose();
    }

    // Stops the delayed lose process.
    private void StopDelayedLose()
    {
        if (delayedLoseCoroutine == null)
        {
            return;
        }

        StopCoroutine(delayedLoseCoroutine);
        delayedLoseCoroutine = null;
    }

    // Stops the victory slow motion process.
    private void StopVictorySlowMotion(bool resetTimeScale)
    {
        if (victorySlowMotionCoroutine != null)
        {
            StopCoroutine(victorySlowMotionCoroutine);
            victorySlowMotionCoroutine = null;
        }

        if (resetTimeScale)
        {
            Time.timeScale = 1f;
        }
    }

    // Checks match completion after a spawner is cleared.
    private void OnSpawnerCleared(Spawner clearedSpawner)
    {
        if (NetworkMatchManager.IsOnlineMatchActive())
        {
            return;
        }

        if (spawners == null || spawners.Length == 0)
        {
            return;
        }

        foreach (Spawner spawner in spawners)
        {
            if (spawner != null && !spawner.IsCleared)
            {
                return;
            }
        }

        Victory();
    }

    // Opens the gameplay ui UI.
    private void OpenGameplayUI()
    {
        if (!openGameplayUIOnStartMatch || UIManager.Instance == null)
        {
            return;
        }

        UIManager.Instance.OpenUI<PanelGamePlay>();
    }

    // Closes the gameplay ui UI.
    private void CloseGameplayUI()
    {
        if (UIManager.Instance == null || !UIManager.Instance.IsUILoaded<PanelGamePlay>())
        {
            return;
        }

        UIManager.Instance.CloseUIDirectly<PanelGamePlay>();
    }

    // Opens the game is finished ui UI.
    private void OpenGameIsFinishedUI()
    {
        if (UIManager.Instance == null)
        {
            return;
        }

        UIManager.Instance.CloseUIDirectly<PanelGameOver>();
        UIManager.Instance.OpenUI<PanelGameIsFinished>();
    }

    // Opens the game over ui UI.
    private void OpenGameOverUI()
    {
        if (UIManager.Instance == null)
        {
            return;
        }

        UIManager.Instance.CloseUIDirectly<PanelGameIsFinished>();
        UIManager.Instance.OpenUI<PanelGameOver>();
    }
}
