using System.Collections;
using UnityEngine;

public class Gate : MonoBehaviour
{
    public GameObject gateVisual;
    private Collider gateCollider;
    private Collider[] gateVisualColliders;
    public float OpenDuration = 2f;
    public float OpenTargetY = -2f;
    private Coroutine openCoroutine;
    private bool isOpen;

    private void Awake()
    {
        gateCollider = GetComponent<Collider>();

        if (gateVisual == null)
        {
            gateVisual = gameObject;
        }

        gateVisualColliders = gateVisual.GetComponentsInChildren<Collider>();
    }

    public void OpenGate()
    {
        if (isOpen)
        {
            return;
        }

        isOpen = true;

        if (openCoroutine != null)
        {
            StopCoroutine(openCoroutine);
        }

        openCoroutine = StartCoroutine(OpenGateAnimation());
    }

    private IEnumerator OpenGateAnimation()
    {
        float currentOpenDuration = 0f;
        Vector3 startPos = gateVisual.transform.position;
        Vector3 targetPos = startPos + Vector3.up * OpenTargetY;
        float duration = Mathf.Max(0.01f, OpenDuration);

        while (currentOpenDuration < duration)
        {
            currentOpenDuration += Time.deltaTime;
            gateVisual.transform.position = Vector3.Lerp(startPos, targetPos, currentOpenDuration / duration);

            yield return null;
        }

        gateVisual.transform.position = targetPos;

        if (gateCollider != null)
        {
            gateCollider.enabled = false;
        }

        foreach (Collider visualCollider in gateVisualColliders)
        {
            if (visualCollider != null)
            {
                visualCollider.enabled = false;
            }
        }

        openCoroutine = null;
    }
}
