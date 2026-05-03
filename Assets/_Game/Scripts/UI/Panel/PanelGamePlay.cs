using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.UI;

public class PanelGamePlay : UICanvas
{
    private const string PlayerTag = "Player";

    [SerializeField] private Button attackButton;
    [SerializeField] private Button slideButton;

    private Player player;

    private void OnEnable()
    {
        EnsureEventSystem();

        if (attackButton == null)
        {
            attackButton = FindButtonByName("Skill1") ?? GetComponentInChildren<Button>(true);
        }

        if (slideButton == null)
        {
            slideButton = FindButtonByName("Skill2 - Slide");
        }

        CachePlayer();

        if (attackButton != null)
        {
            attackButton.onClick.RemoveListener(OnAttackButtonClicked);
            attackButton.onClick.AddListener(OnAttackButtonClicked);
        }

        if (slideButton != null)
        {
            slideButton.onClick.RemoveListener(OnSlideButtonClicked);
            slideButton.onClick.AddListener(OnSlideButtonClicked);
        }
    }

    private void OnDisable()
    {
        if (attackButton != null)
        {
            attackButton.onClick.RemoveListener(OnAttackButtonClicked);
        }

        if (slideButton != null)
        {
            slideButton.onClick.RemoveListener(OnSlideButtonClicked);
        }
    }

    public void OnAttackButtonClicked()
    {
        if (player == null)
        {
            CachePlayer();
        }

        if (player != null)
        {
            player.Attack();
        }
    }

    public void OnSlideButtonClicked()
    {
        if (player == null)
        {
            CachePlayer();
        }

        if (player != null)
        {
            player.Slide();
        }
    }

    private Button FindButtonByName(string buttonName)
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);

        foreach (Button button in buttons)
        {
            if (button != null && button.gameObject.name == buttonName)
            {
                return button;
            }
        }

        return null;
    }

    private void CachePlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag(PlayerTag);

        if (playerObject != null)
        {
            player = playerObject.GetComponent<Player>();

            if (player == null)
            {
                player = playerObject.GetComponentInParent<Player>();
            }
        }

        if (player == null)
        {
            player = FindFirstObjectByType<Player>();
        }
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();

#if ENABLE_INPUT_SYSTEM
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
#else
        eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
    }
}
