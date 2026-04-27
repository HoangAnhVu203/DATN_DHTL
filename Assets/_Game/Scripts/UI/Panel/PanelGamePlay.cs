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

    private Player player;

    private void OnEnable()
    {
        EnsureEventSystem();

        if (attackButton == null)
        {
            attackButton = GetComponentInChildren<Button>(true);
        }

        CachePlayer();

        if (attackButton != null)
        {
            attackButton.onClick.RemoveListener(OnAttackButtonClicked);
            attackButton.onClick.AddListener(OnAttackButtonClicked);
        }
    }

    private void OnDisable()
    {
        if (attackButton != null)
        {
            attackButton.onClick.RemoveListener(OnAttackButtonClicked);
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
