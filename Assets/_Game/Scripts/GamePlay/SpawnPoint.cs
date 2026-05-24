using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class SpawnPoint : MonoBehaviour
{
    public GameObject EnemyToSpawn;

    [SerializeField] private Vector3 gizmoSize = new Vector3(0.8f, 1.8f, 0.8f);
    [SerializeField] private Color gizmoColor = new Color(1f, 0.55f, 0f, 0.35f);
    [SerializeField] private Color selectedGizmoColor = new Color(0f, 1f, 0.35f, 0.45f);

    private void OnDrawGizmos()
    {
        DrawSpawnGizmo(gizmoColor, false);
    }

    private void OnDrawGizmosSelected()
    {
        DrawSpawnGizmo(selectedGizmoColor, true);
    }

    private void DrawSpawnGizmo(Color color, bool selected)
    {
        Vector3 size = new Vector3(
            Mathf.Max(0.05f, gizmoSize.x),
            Mathf.Max(0.05f, gizmoSize.y),
            Mathf.Max(0.05f, gizmoSize.z)
        );

        Vector3 center = transform.position + Vector3.up * (size.y * 0.5f);

        Gizmos.matrix = Matrix4x4.TRS(center, transform.rotation, Vector3.one);
        Gizmos.color = color;
        Gizmos.DrawCube(Vector3.zero, size);
        Gizmos.color = new Color(color.r, color.g, color.b, 1f);
        Gizmos.DrawWireCube(Vector3.zero, size);
        Gizmos.matrix = Matrix4x4.identity;

#if UNITY_EDITOR
        if (selected)
        {
            string enemyName = EnemyToSpawn != null ? EnemyToSpawn.name : "No Enemy";
            Handles.Label(center + Vector3.up * (size.y * 0.6f), $"{name}\n{enemyName}");
        }
#endif
    }
}
