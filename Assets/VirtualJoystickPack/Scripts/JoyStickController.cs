using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class JoyStickController : MonoBehaviour
{
    private const int NoTouchId = -1;

    public static Vector3 direct;

    private Vector3 screen;
    private Vector3 startPoint;
    private Vector3 updatePoint;
    private bool wasPressed;
    private bool pointerStartedOverButton;
    private int activeTouchId = NoTouchId;

    public RectTransform joystickBG;
    public RectTransform joystickControl;
    public float magnitude;
    public GameObject joystickPanel;

    private void Awake()
    {
        screen.x = Screen.width;
        screen.y = Screen.height;

        direct = Vector3.zero;
    }
    
    private void Update()
    {
        UpdateScreenSize();

        if (UpdateTouchJoystick())
        {
            wasPressed = false;
            pointerStartedOverButton = false;
            return;
        }

        UpdateMouseJoystick();
    }

    private void UpdateMouseJoystick()
    {
        bool isPressed = TryGetMousePosition(out Vector3 pointerPosition);

        if (isPressed && !wasPressed)
        {
            pointerStartedOverButton = IsPointerOverButton(pointerPosition);

            if (pointerStartedOverButton)
            {
                StopJoystick();
            }
            else
            {
                StartJoystick(pointerPosition);
            }
        }

        if (isPressed && !pointerStartedOverButton)
        {
            UpdateJoystick(pointerPosition);
        }

        if (!isPressed && wasPressed)
        {
            if (!pointerStartedOverButton)
            {
                StopJoystick();
            }

            pointerStartedOverButton = false;
        }

        wasPressed = isPressed;
    }

    private bool UpdateTouchJoystick()
    {
        if (Touchscreen.current == null)
        {
            return false;
        }

        bool hasPressedTouch = HasPressedTouch();

        if (!hasPressedTouch)
        {
            if (activeTouchId != NoTouchId)
            {
                StopJoystick();
                activeTouchId = NoTouchId;
            }

            return false;
        }

        if (activeTouchId != NoTouchId)
        {
            if (TryGetTouchPosition(activeTouchId, out Vector3 activePointerPosition))
            {
                UpdateJoystick(activePointerPosition);
                return true;
            }

            StopJoystick();
            activeTouchId = NoTouchId;
        }

        foreach (var touch in Touchscreen.current.touches)
        {
            if (!touch.press.isPressed)
            {
                continue;
            }

            Vector3 pointerPosition = ScreenToJoystickPosition(touch.position.ReadValue());

            if (IsPointerOverButton(pointerPosition))
            {
                continue;
            }

            activeTouchId = touch.touchId.ReadValue();
            StartJoystick(pointerPosition);
            UpdateJoystick(pointerPosition);
            return true;
        }

        return true;
    }

    private void UpdateScreenSize()
    {
        screen.x = Screen.width;
        screen.y = Screen.height;
    }

    private bool TryGetMousePosition(out Vector3 pointerPosition)
    {
        pointerPosition = Vector3.zero;

        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            pointerPosition = ScreenToJoystickPosition(Mouse.current.position.ReadValue());
            return true;
        }

        return false;
    }

    private bool HasPressedTouch()
    {
        foreach (var touch in Touchscreen.current.touches)
        {
            if (touch.press.isPressed)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetTouchPosition(int touchId, out Vector3 pointerPosition)
    {
        foreach (var touch in Touchscreen.current.touches)
        {
            if (touch.touchId.ReadValue() == touchId && touch.press.isPressed)
            {
                pointerPosition = ScreenToJoystickPosition(touch.position.ReadValue());
                return true;
            }
        }

        pointerPosition = Vector3.zero;
        return false;
    }

    private Vector3 ScreenToJoystickPosition(Vector2 screenPosition)
    {
        Vector3 pointerPosition = screenPosition;
        pointerPosition -= screen / 2f;
        return pointerPosition;
    }

    private void StartJoystick(Vector3 pointerPosition)
    {
        startPoint = pointerPosition;
        joystickBG.anchoredPosition = startPoint;
        joystickControl.anchoredPosition = startPoint;
        joystickPanel.SetActive(true);
    }

    private void UpdateJoystick(Vector3 pointerPosition)
    {
        updatePoint = pointerPosition;
        Vector3 delta = updatePoint - startPoint;

        joystickControl.anchoredPosition = Vector3.ClampMagnitude(delta, magnitude) + startPoint;

        if (delta.sqrMagnitude <= 0.001f)
        {
            direct = Vector3.zero;
            return;
        }

        direct = delta.normalized;
        direct.z = direct.y;
        direct.y = 0f;
    }

    private void StopJoystick()
    {
        joystickPanel.SetActive(false);
        joystickControl.anchoredPosition = startPoint;
        direct = Vector3.zero;
    }

    private void OnDisable()
    {
        wasPressed = false;
        pointerStartedOverButton = false;
        activeTouchId = NoTouchId;
        direct = Vector3.zero;
    }

    private bool IsPointerOverButton(Vector3 pointerPosition)
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = pointerPosition + screen / 2f
        };

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        for (int i = 0; i < results.Count; i++)
        {
            if (results[i].gameObject.GetComponentInParent<Button>() != null)
            {
                return true;
            }
        }

        return false;
    }
}
