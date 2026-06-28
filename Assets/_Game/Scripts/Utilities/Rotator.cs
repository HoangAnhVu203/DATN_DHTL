using UnityEngine;

public class Rotator : MonoBehaviour
{
    public float speed = 80f;

    // Runs the per-frame work for this behaviour.
    private void Update()
    {
        transform.Rotate(new Vector3(0f, speed * Time.deltaTime, 0f), Space.World);
    }
}
