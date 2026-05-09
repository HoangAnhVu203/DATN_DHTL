using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameState
{
    Loading,
    Home,
    StartMatch,
    Pause,
    EndMatch,
    Victory,
    Lose
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private GameState initialState = GameState.Loading;
    [SerializeField] private GameState stateAfterLoading = GameState.StartMatch;
    [SerializeField] private float loadingDuration = 0.5f;
    [SerializeField] private bool autoFindSceneObjects = true;
    [SerializeField] private bool openGameplayUIOnStartMatch = true;
    [SerializeField] private Player player;
    [SerializeField] private Spawner[] spawners;

    private Coroutine loadingCoroutine;
    private bool hasFinishedMatch;
    private bool hasEnteredState;

    public GameState CurrentState { get; private set; }
    public bool IsPlaying => CurrentState == GameState.StartMatch;
    public bool IsPaused => CurrentState == GameState.Pause;
    public event Action<GameState, GameState> StateChanged;

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

    private void Start()
    {
        SubscribeSceneEvents();
        ChangeState(initialState);
    }

    private void OnDestroy()
    {
        UnsubscribeSceneEvents();

        if (Instance == this)
        {
            Instance = null;
        }
    }

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

    public void GoHome()
    {
        hasFinishedMatch = false;
        ChangeState(GameState.Home);
    }

    public void StartMatch()
    {
        hasFinishedMatch = false;
        ChangeState(GameState.StartMatch);
    }

    public void StartMathc()
    {
        StartMatch();
    }

    public void Pause()
    {
        if (CurrentState != GameState.StartMatch)
        {
            return;
        }

        ChangeState(GameState.Pause);
    }

    public void Resume()
    {
        if (CurrentState != GameState.Pause)
        {
            return;
        }

        ChangeState(GameState.StartMatch);
    }

    public void EndMatch()
    {
        ChangeState(GameState.EndMatch);
    }

    public void Victory()
    {
        FinishMatch(GameState.Victory);
    }

    public void Lose()
    {
        FinishMatch(GameState.Lose);
    }

    public void RestartLevel()
    {
        Time.timeScale = 1f;
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex);
    }

    public void LoadScene(int buildIndex)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(buildIndex);
    }

    public void QuitGame()
    {
        Application.Quit();
    }

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
                Time.timeScale = 1f;
                OpenGameplayUI();
                break;

            case GameState.Pause:
                Time.timeScale = 0f;
                break;

            case GameState.EndMatch:
            case GameState.Victory:
            case GameState.Lose:
                Time.timeScale = 0f;
                CloseGameplayUI();
                break;
        }
    }

    private void ExitState(GameState state)
    {
        if (state == GameState.Loading && loadingCoroutine != null)
        {
            StopCoroutine(loadingCoroutine);
            loadingCoroutine = null;
        }
    }

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

    private IEnumerator LoadingRoutine()
    {
        if (loadingDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(loadingDuration);
        }

        loadingCoroutine = null;
        ChangeState(stateAfterLoading);
    }

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

    private void OnPlayerDied(Character deadCharacter)
    {
        Lose();
    }

    private void OnSpawnerCleared(Spawner clearedSpawner)
    {
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

    private void OpenGameplayUI()
    {
        if (!openGameplayUIOnStartMatch || UIManager.Instance == null)
        {
            return;
        }

        UIManager.Instance.OpenUI<PanelGamePlay>();
    }

    private void CloseGameplayUI()
    {
        if (UIManager.Instance == null || !UIManager.Instance.IsUILoaded<PanelGamePlay>())
        {
            return;
        }

        UIManager.Instance.CloseUIDirectly<PanelGamePlay>();
    }
}
