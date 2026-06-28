using UnityEngine;
using UnityEngine.UI;

public class PanelGameOver : UICanvas
{
    [SerializeField] private Button returnRoomButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private MatchResultLeaderboardRenderer leaderboardRenderer;

    // Puts this panel into its default ready state.
    public override void SetUp()
    {
        base.SetUp();
        ResolveReferences();
        RenderMatchLeaderboard();
    }

    // Removes listeners and runtime resources before destruction.
    private void OnDestroy()
    {
        RemoveListeners();
    }

    private void ResolveReferences()
    {
        if (returnRoomButton == null)
        {
            returnRoomButton = FindButton("Button_MainMenu");
        }

        if (restartButton == null)
        {
            restartButton = FindButton("Button_Restart");
        }

        if (leaderboardRenderer == null)
        {
            leaderboardRenderer = GetComponent<MatchResultLeaderboardRenderer>();
            if (leaderboardRenderer == null)
            {
                leaderboardRenderer = gameObject.AddComponent<MatchResultLeaderboardRenderer>();
            }
        }

        RemoveListeners();

        if (returnRoomButton != null)
        {
            returnRoomButton.onClick.AddListener(ReturnToRoom);
        }

        if (restartButton != null)
        {
            restartButton.onClick.AddListener(ReturnToRoom);
        }
    }

    // Removes the listeners.
    private void RemoveListeners()
    {
        if (returnRoomButton != null)
        {
            returnRoomButton.onClick.RemoveListener(ReturnToRoom);
        }

        if (restartButton != null)
        {
            restartButton.onClick.RemoveListener(ReturnToRoom);
        }
    }

    private Button FindButton(string buttonName)
    {
        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            if (button != null && button.name == buttonName)
            {
                return button;
            }
        }

        return null;
    }

    // Returns to room.
    private void ReturnToRoom()
    {
        ReturnToRoomAfterMatch.StartReturn();
    }

    // Runs the render match leaderboard step.
    private void RenderMatchLeaderboard()
    {
        if (leaderboardRenderer != null)
        {
            leaderboardRenderer.Render();
        }
    }
}
