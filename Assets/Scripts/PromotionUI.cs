using UnityEngine;
using UnityEngine.UI;

public class PromotionUI : MonoBehaviour
{
    [Header("Promotion Panel")]
    public GameObject panel;

    [Header("Promotion Buttons")]
    public Button queenButton;
    public Button rookButton;
    public Button bishopButton;
    public Button knightButton;

    [Header("Cancel Area")]
    public Button cancelButton;   // full-screen transparent behind the panel

    private PieceController currentPawn;

    // Show all options (standard promotion) — no cancel
    public void Show(PieceController pawn)
    {
        currentPawn = pawn;
        queenButton.gameObject.SetActive(true);
        rookButton.gameObject.SetActive(true);
        bishopButton.gameObject.SetActive(true);
        knightButton.gameObject.SetActive(true);
        // hide cancel for standard promotion
        cancelButton.gameObject.SetActive(false);
        panel.SetActive(true);
    }

    // Show only knight option (Burgundians on 6th rank) — allow cancel
    public void ShowKnightOnly(PieceController pawn)
    {
        currentPawn = pawn;
        queenButton.gameObject.SetActive(false);
        rookButton.gameObject.SetActive(false);
        bishopButton.gameObject.SetActive(false);
        knightButton.gameObject.SetActive(true);
        // enable cancel for optional promotion
        cancelButton.gameObject.SetActive(true);
        panel.SetActive(true);
    }

    // Called by cancelButton's OnClick(): dismiss without promoting
    public void CancelPromotion()
    {
        panel.SetActive(false);
        cancelButton.gameObject.SetActive(false);
    }

    // Called by each promotion button via OnClick()
    public void SelectPromotion(string pieceName)
    {
        cancelButton.gameObject.SetActive(false);
        currentPawn.Promote(pieceName);
        panel.SetActive(false);
    }
}
