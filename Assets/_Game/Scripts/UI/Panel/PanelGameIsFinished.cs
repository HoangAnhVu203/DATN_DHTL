using UnityEngine;
using UnityEngine.UI;

public class PanelGameIsFinished : UICanvas
{
    [SerializeField] private Button returnRoomButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private MatchResultLeaderboardRenderer leaderboardRenderer;

    public override void SetUp()
    {
        base.SetUp();
        ResolveReferences();
        RenderMatchLeaderboard();
    }

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

    private void ReturnToRoom()
    {
        ReturnToRoomAfterMatch.StartReturn();
    }

    private void RenderMatchLeaderboard()
    {
        if (leaderboardRenderer != null)
        {
            leaderboardRenderer.Render();
        }
    }
}
