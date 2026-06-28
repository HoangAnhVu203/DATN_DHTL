using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MatchResultLeaderboardRenderer : MonoBehaviour
{
    [SerializeField] private Transform contentRoot;
    [SerializeField] private GameObject rowPrefab;

    private readonly List<GameObject> spawnedRows = new List<GameObject>();

    // Rebuilds the match result rows from the latest stats.
    public void Render()
    {
        ResolveReferences();
        ClearRows();

        if (contentRoot == null)
        {
            Debug.LogWarning($"{nameof(MatchResultLeaderboardRenderer)}: missing leaderboard content root.");
            return;
        }

        List<OnlineMatchStats.MatchLeaderboardRow> rows = OnlineMatchStats.GetLeaderboardSnapshot();
        for (int i = 0; i < rows.Count; i++)
        {
            GameObject row = CreateRow();
            row.name = $"LeaderboardRow_{rows[i].rank}";
            row.SetActive(true);
            spawnedRows.Add(row);

            SetText(row, "Rank", rows[i].rank.ToString());
            SetText(row, "Display Name", rows[i].displayName);
            SetText(row, "Kills", rows[i].kills.ToString());
            SetText(row, "Damage", rows[i].damageDealt.ToString());
            SetText(row, "Revives", rows[i].revives.ToString());
        }
    }

    private void ResolveReferences()
    {
        if (contentRoot == null)
        {
            ScrollRect scrollRect = GetComponentInChildren<ScrollRect>(true);
            if (scrollRect != null)
            {
                contentRoot = scrollRect.content;
            }
        }

        if (rowPrefab == null && contentRoot != null && contentRoot.childCount > 0)
        {
            rowPrefab = contentRoot.GetChild(0).gameObject;
            rowPrefab.SetActive(false);
        }
    }

    // Creates the row.
    private GameObject CreateRow()
    {
        if (rowPrefab != null)
        {
            return Instantiate(rowPrefab, contentRoot);
        }

        return CreateFallbackRow();
    }

    // Creates the fallback row.
    private GameObject CreateFallbackRow()
    {
        GameObject row = new GameObject("LeaderboardContentUI", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(contentRoot, false);

        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0f, 30f);

        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 4f;

        CreateText(row.transform, "Rank");
        CreateText(row.transform, "Display Name");
        CreateText(row.transform, "Kills");
        CreateText(row.transform, "Damage");
        CreateText(row.transform, "Revives");

        return row;
    }

    // Creates the text.
    private void CreateText(Transform parent, string textName)
    {
        GameObject textObject = new GameObject(textName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(LayoutElement));
        textObject.transform.SetParent(parent, false);

        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 14;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;

        LayoutElement layoutElement = textObject.GetComponent<LayoutElement>();
        layoutElement.minHeight = 30f;
        layoutElement.flexibleWidth = textName == "Display Name" ? 2f : 1f;
    }

    // Updates the text.
    private void SetText(GameObject row, string childName, string value)
    {
        Transform child = FindChild(row.transform, childName);
        if (child == null)
        {
            Debug.LogWarning($"{nameof(MatchResultLeaderboardRenderer)}: missing '{childName}' in row prefab.");
            return;
        }

        Text text = child.GetComponent<Text>();
        if (text != null)
        {
            text.text = value;
        }
    }

    private Transform FindChild(Transform root, string childName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
            {
                return child;
            }
        }

        return null;
    }

    // Clears the rows.
    private void ClearRows()
    {
        foreach (GameObject row in spawnedRows)
        {
            if (row != null)
            {
                Destroy(row);
            }
        }

        spawnedRows.Clear();
    }
}
