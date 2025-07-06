using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewCivConfig", menuName = "Chess/Civ Configuration")]
public class CivConfiguration : ScriptableObject
{
    [System.Serializable]
    public struct PieceEntry
    {
        // prefab for this civ's piece on the White side
        public GameObject whitePrefab;
        // prefab for this civ's piece on the Black side
        public GameObject blackPrefab;
        // grid coordinates: file (0–7), rank (0–7)
        public Vector2Int gridPos;
        // which side this entry belongs to
        public bool isWhite;
    }

    [Header("Starting Positions")]
    public List<PieceEntry> startingPositions = new List<PieceEntry>();

    [Header("Dynamic Spawn Prefabs")]
    // Standard pawn spawn prefabs (used for conversions, etc.)
    public GameObject whitePawnPrefab;
    public GameObject blackPawnPrefab;

    // Horseman prefabs (for Huns civ)
    public GameObject whiteHorsemanPrefab;
    public GameObject blackHorsemanPrefab;

    // Witch prefabs (for Haitians civ)
    public GameObject whiteWitchPrefab;
    public GameObject blackWitchPrefab;
}
