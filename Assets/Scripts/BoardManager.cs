using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class BoardManager : MonoBehaviour
{
    public static BoardManager Instance;

    [Header("Scene references")]
    public Transform boardRoot;          // drag the “Board” GameObject in the inspector

    [Header("Tuning")]
    public float squareSize = 1f;        //  1 → every square is 1 unit wide
    public float pieceZ = -1f;       //  z-offset that keeps pieces in front of the board

    public readonly GameObject[,] logicalBoard = new GameObject[8, 8];

    void Awake()
    {
        Instance = this;
    #if UNITY_EDITOR     // leave it in builds too, it’s tiny
        Debug.Log($"[{(NetworkServer.active ? "Host" : "Client")}] " +
                  $"boardRoot = {boardRoot.position} scale = {boardRoot.lossyScale}");
    #endif
        if (boardRoot == null) boardRoot = transform;   // safe fallback
    }

    /* ------------ grid <---> world ------------------------------------------------ */

    public Vector3 GridToWorld(int x, int y)
    {
        // local origin is the *centre* of a8 (0,0)
        var local = new Vector3((-3.5f + x) * squareSize,
                                (-3.5f + y) * squareSize,
                                pieceZ);
        return boardRoot.TransformPoint(local);
    }

    public Vector2Int WorldToGrid(Vector3 world)
    {
        var local = boardRoot.InverseTransformPoint(world);
        int x = Mathf.RoundToInt(local.x / squareSize + 3.5f);
        int y = Mathf.RoundToInt(local.y / squareSize + 3.5f);
        return new Vector2Int(x, y);
    }

    /// Returns a list of all orthogonal + diagonal neighbors
    /// inside the 8×8 board for the given cell.
    public List<Vector2Int> GetNeighborCoords(Vector2Int cell)
    {
        var neighbors = new List<Vector2Int>(8);
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                // skip the cell itself
                if (dx == 0 && dy == 0) continue;

                int nx = cell.x + dx;
                int ny = cell.y + dy;
                // only add if inside 0–7 range
                if (nx >= 0 && nx < 8 && ny >= 0 && ny < 8)
                    neighbors.Add(new Vector2Int(nx, ny));
            }
        }
        return neighbors;
    }
}
