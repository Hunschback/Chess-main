using System.Collections.Generic;
using UnityEngine;


public class HighlightManager : MonoBehaviour
{
    public static HighlightManager Instance;

    public GameObject highlightPrefab;
    private List<GameObject> activeHighlights = new List<GameObject>();

    void Awake()
    {
        Instance = this;
    }

    public void ShowHighlights(List<Vector2Int> positions)
    {
        ClearHighlights();
        Debug.Log($"Showing {positions.Count} highlights");

        foreach (Vector2Int pos in positions)
        {
            Vector3 worldPos = BoardManager.Instance.GridToWorld(pos.x, pos.y);
            worldPos.z = -0.5f; // Put it in front of the board but behind pieces
            GameObject highlight = Instantiate(highlightPrefab, worldPos, Quaternion.identity);

            activeHighlights.Add(highlight);
        }
    }


    public void ClearHighlights()
    {
        foreach (GameObject h in activeHighlights)
        {
            Destroy(h);
        }
        activeHighlights.Clear();
    }
}
