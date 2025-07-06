using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MoveHistoryUI : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform content;     // assign: MoveHistoryScroll → Viewport → Content
    public GameObject moveRowPrefab;  // assign: your MoveRow prefab

    private List<GameObject> rows = new List<GameObject>();

    // Call to rebuild the move list
    public void Refresh(List<GameController.Move> history)
    {
        // 1) Clear old rows
        foreach (var go in rows) Destroy(go);
        rows.Clear();

        // 2) Instantiate new rows
        for (int i = 0; i < history.Count; i += 2)
        {
            GameObject row = Instantiate(moveRowPrefab, content);
            Text[] cols = row.GetComponentsInChildren<Text>(true);

            int moveNum = (i / 2) + 1;
            cols[0].text = moveNum + ".";
            cols[1].text = ToAlgebraic(history[i]);
            cols[2].text = (i + 1 < history.Count)
                ? ToAlgebraic(history[i + 1])
                : "";

            rows.Add(row);
        }

        // 3) Scroll to bottom
        Canvas.ForceUpdateCanvases();
        ScrollRect scroll = GetComponent<ScrollRect>();
        if (scroll != null) scroll.verticalNormalizedPosition = 0f;
    }

    string ToAlgebraic(GameController.Move m)
    {
        // 1) Handle castling first
        if (m.isCastling)
            return (m.to.x - m.from.x == 2) ? "O-O" : "O-O-O";

        // 2) Piece letter (empty for pawn)
        string pieceChar;
        switch (m.piece)
        {
            case PieceController.PieceType.Knight: pieceChar = "N"; break;
            case PieceController.PieceType.King: pieceChar = "K"; break;
            case PieceController.PieceType.Queen: pieceChar = "Q"; break;
            case PieceController.PieceType.Rook: pieceChar = "R"; break;
            case PieceController.PieceType.Bishop: pieceChar = "B"; break;
            default: pieceChar = ""; break;
        }

        // 3) Capture marker
        string capture = (m.isCapture || m.isEnPassant) ? "x" : "";

        // 4) File+rank
        char fileChar = (char)('a' + m.to.x);
        char rankChar = (char)('1' + m.to.y);
        string square = fileChar.ToString() + rankChar.ToString();

        // 5) En passant suffix
        string ep = m.isEnPassant ? " e.p." : "";

        return $"{pieceChar}{capture}{square}{ep}";
    }

}
