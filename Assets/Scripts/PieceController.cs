using UnityEngine;
using System.Collections.Generic;
using Mirror;   //  ← add just after the existing using UnityEngine;



public class PieceController : NetworkBehaviour   // instead of MonoBehaviour
{
    public enum PieceType
    {
        Pawn,
        Knight,
        Bishop,
        Rook,
        Queen,
        King,
        Horseman,   // ← new
        Witch       // ← new
    }

    // replace the old one-liner
    PlayerState LocalPlayer =>
        NetworkClient.connection?.identity != null
            ? NetworkClient.connection.identity.GetComponent<PlayerState>()
            : null;



    // Promotion sprites
    public Sprite whiteQueenSprite, whiteRookSprite, whiteBishopSprite, whiteKnightSprite, whitePawnSprite;
    public Sprite blackQueenSprite, blackRookSprite, blackBishopSprite, blackKnightSprite, blackPawnSprite;

    // State
    public PieceType type;
    public Vector2Int currentGridPosition;
    [HideInInspector] public bool hasMoved = false;

    private Vector2Int originalGridPosition;
    private Vector3 offset;
    private bool isDragging = false;
    private GameController gameController;

    // Civ config for this piece
    private CivConfiguration MyCiv => tag == "White" ? gameController.WhiteCiv : gameController.BlackCiv;

    void Awake()
    {
        gameController = FindObjectOfType<GameController>();
    }

    void OnMouseDown()
    {
        // must be my turn AND my colour matches the piece I clicked
        var me = LocalPlayer;
        if (me == null) return;
        bool myTurn = (me.Color == PlayerColor.White && gameController.WhiteTurn) ||
                      (me.Color == PlayerColor.Black && !gameController.WhiteTurn);
        bool myPiece = (tag == "White" && me.Color == PlayerColor.White) ||
                       (tag == "Black" && me.Color == PlayerColor.Black);

        if (gameController.GameOver || !myTurn || !myPiece) return;

        if ((tag == "White" && gameController.WhiteTurn) || (tag == "Black" && !gameController.WhiteTurn))
        {
            originalGridPosition = currentGridPosition = BoardManager.Instance.WorldToGrid(transform.position);
            offset = transform.position - GetMouseWorldPosition();
            isDragging = true;
            HighlightManager.Instance.ShowHighlights(GetLegalMoves());
        }
    }

    void OnMouseDrag()
    {
        if (isDragging)
            transform.position = GetMouseWorldPosition() + offset;
    }

    void OnMouseUp()
    {
        if (!isDragging) return;
        isDragging = false;

        // convert drag-end world → grid
        Vector2Int from = originalGridPosition;
        Vector2Int to = BoardManager.Instance.WorldToGrid(transform.position);

        // quick client-side discard of obviously illegal tries
        if (to == from) { SnapToGrid(from); return; }

        // Ask the server – the server re-validates
        CmdTryMove(from, to);
    }

    [Command(requiresAuthority = false)]
    void CmdTryMove(Vector2Int from, Vector2Int to, NetworkConnectionToClient sender = null)
    {
        bool ok = GameController.Instance.PerformMove(from, to, false, sender);
        if (ok) RpcApplyMove(from, to, false);
    }



    void AttemptCastling(bool kingSide)
    {
        int y = originalGridPosition.y;
        int rookX = kingSide ? 7 : 0;
        int newKingX = kingSide ? originalGridPosition.x + 2 : originalGridPosition.x - 2;
        int newRookX = kingSide ? newKingX - 1 : newKingX + 1;

        var rookObj = BoardManager.Instance.logicalBoard[rookX, y];
        if (rookObj == null) return;
        var rookPC = rookObj.GetComponent<PieceController>();
        if (rookPC == null || rookPC.hasMoved) return;

        int step = kingSide ? 1 : -1;
        for (int x = originalGridPosition.x + step; x != rookX; x += step)
            if (BoardManager.Instance.logicalBoard[x, y] != null) return;

        if (gameController.IsKingInCheck(tag)) return;
        if (gameController.IsSquareUnderAttack(new Vector2Int(originalGridPosition.x + step, y), tag)) return;
        if (gameController.IsSquareUnderAttack(new Vector2Int(newKingX, y), tag)) return;

        // Move king
        BoardManager.Instance.logicalBoard[originalGridPosition.x, y] = null;
        currentGridPosition = new Vector2Int(newKingX, y);
        BoardManager.Instance.logicalBoard[newKingX, y] = gameObject;
        SnapToGrid(currentGridPosition);
        hasMoved = true;
        gameController.RecordMove(type, originalGridPosition, currentGridPosition, false, false, true);

        // Move rook
        BoardManager.Instance.logicalBoard[rookX, y] = null;
        rookPC.currentGridPosition = new Vector2Int(newRookX, y);
        BoardManager.Instance.logicalBoard[newRookX, y] = rookObj;
        rookObj.transform.position = BoardManager.Instance.GridToWorld(newRookX, y);
        rookPC.hasMoved = true;
    }

    void CheckForPromotion()
    {
        if (type != PieceType.Pawn)
            return;
        bool reachedEnd = (tag == "White" && currentGridPosition.y == 7)
                         || (tag == "Black" && currentGridPosition.y == 0);
        // Burgundians: optional knight promotion on 6th+ rank
        if (MyCiv.name == "Burgundians")
        {
            bool reachedSixth = (tag == "White" && currentGridPosition.y >= 5)
                                || (tag == "Black" && currentGridPosition.y <= 2);
            if (reachedSixth & !reachedEnd)
            {
                // Automatically offer/promote to Knight
                FindObjectOfType<PromotionUI>()?.ShowKnightOnly(this);
                return;
            }
        }

        // Standard promotion on final rank
        
        if (reachedEnd)
            FindObjectOfType<PromotionUI>()?.Show(this);
    }



    public void Promote(string pieceName)
    {
        var sr = GetComponent<SpriteRenderer>();
        switch (pieceName)
        {
            case "Queen": sr.sprite = (tag == "White" ? whiteQueenSprite : blackQueenSprite); type = PieceType.Queen; break;
            case "Rook": sr.sprite = (tag == "White" ? whiteRookSprite : blackRookSprite); type = PieceType.Rook; break;
            case "Bishop": sr.sprite = (tag == "White" ? whiteBishopSprite : blackBishopSprite); type = PieceType.Bishop; break;
            case "Knight": sr.sprite = (tag == "White" ? whiteKnightSprite : blackKnightSprite); type = PieceType.Knight; break;
            default: sr.sprite = (tag == "White" ? whiteQueenSprite : blackQueenSprite); type = PieceType.Queen; break;
        }
        gameController.UpdateKingCheckStatus();
        gameController.CheckForGameEnd();
    }

    public List<Vector2Int> GetLegalMoves()
    {
        var moves = new List<Vector2Int>();
        for (int x = 0; x < 8; x++) for (int y = 0; y < 8; y++)
            {
                var to = new Vector2Int(x, y);
                var occ = BoardManager.Instance.logicalBoard[x, y];
                if (to != currentGridPosition && IsValidMove(currentGridPosition, to, occ) && !MoveExposesKing(currentGridPosition, to))
                    moves.Add(to);
            }
        return moves;
    }

    void SnapToGrid(Vector2Int g) => transform.position = BoardManager.Instance.GridToWorld(g.x, g.y);

    Vector3 GetMouseWorldPosition()
    {
        var m = Input.mousePosition;
        m.z = Camera.main.WorldToScreenPoint(transform.position).z;
        return Camera.main.ScreenToWorldPoint(m);
    }

    public bool IsValidMove(Vector2Int from, Vector2Int to, GameObject target)
    {
        if (target != null && target.CompareTag(tag)) return false;
        var delta = to - from;
        int dx = Mathf.Abs(delta.x), dy = Mathf.Abs(delta.y);
        string civName = MyCiv.name;
        switch (type)
        {
            case PieceType.Pawn:
                int dir = (tag == "White") ? 1 : -1;
                bool start = (tag == "White" && from.y == 1) || (tag == "Black" && from.y == 6);
                bool isCap = (target != null && target.tag != tag && Mathf.Abs(delta.x) == 1 && delta.y == dir);
                if (civName == "Teutons" && isCap && target.GetComponent<PieceController>().type == PieceType.Pawn && gameController.moveHistory.Count / 2 < 12)
                    return false;
                if (civName == "Vikings" && delta.x == 0 && delta.y == dir && target != null)
                    return true;
                if (civName == "Britons")
                {
                    // 1) normal 2-square initial advance
                    if (delta.x == 0 && delta.y == 2 * dir && target == null && start)
                        return true;
                    // 2) normal 1-square advance
                    if (delta.x == 0 && delta.y == dir && target == null)
                        return true;
                    // 3) diagonal capture (1-square)
                    if (Mathf.Abs(delta.x) == 1 && delta.y == dir && target != null)
                        return true;
                    // 4) straight-ahead “attack” (2-squares)    
                    if (delta.x == 0 && delta.y == 2 * dir && target != null)
                        return true;
                    return false;
                }

                if (civName == "French")
                {
                    if (delta.x == 0 && delta.y == dir) return false;
                    if (Mathf.Abs(delta.x) <= 1 && Mathf.Abs(delta.y) <= 1) return target == null || target.tag != tag;
                    return false;
                }
                if (target == null)
                {
                    if (delta.x == 0 && delta.y == dir) return true;
                    if (delta.x == 0 && delta.y == 2 * dir && start) return true;
                    var ep = gameController.enPassantEligiblePawn;
                    if (Mathf.Abs(delta.x) == 1 && delta.y == dir && ep != null && ep.currentGridPosition.x == to.x && ep.currentGridPosition.y == from.y)
                        return true;
                }
                if (Mathf.Abs(delta.x) == 1 && delta.y == dir && target != null && target.tag != tag) return true;
                return false;
            case PieceType.Knight:
                if (civName == "Burgundians" && ((dx == 1 && dy == 0) || (dx == 0 && dy == 1))) return true;
                return (dx == 1 && dy == 2) || (dx == 2 && dy == 1);
            case PieceType.Bishop:
                return dx == dy && ClearPath(from, to);
            case PieceType.Rook:
                return (dx == 0 || dy == 0) && ClearPath(from, to);
            case PieceType.Queen:
                return ((dx == dy) || (dx == 0 || dy == 0)) && ClearPath(from, to);
            case PieceType.King:
                // Egyptians: 2-square diagonal only if ClearPath
                if (civName == "Egyptians" && Mathf.Abs(delta.x) == 2 && Mathf.Abs(delta.y) == 2 && ClearPath(from, to))
                    return true;
                // Spanish: knight moves
                if (civName == "Spanish" && ((dx == 1 && dy == 2) || (dx == 2 && dy == 1)))
                    return true;
                // normal king
                if (dx <= 1 && dy <= 1) return true;
                // castling
                if (dy == 0 && dx == 2 && !hasMoved) return true;
                return false;
            case PieceType.Horseman:
                return (dx <= 1 && dy <= 1);
            case PieceType.Witch:
                return ((dx == 1 && dy == 2) || (dx == 2 && dy == 1));

            default: return false;
        }
    }

    bool ClearPath(Vector2Int from, Vector2Int to)
    {
        var dir = new Vector2Int((to.x > from.x) ? 1 : (to.x < from.x ? -1 : 0), (to.y > from.y) ? 1 : (to.y < from.y ? -1 : 0));
        var c = from + dir;
        while (c != to)
        {
            if (BoardManager.Instance.logicalBoard[c.x, c.y] != null) return false;
            c += dir;
        }
        return true;
    }

    public bool MoveExposesKing(Vector2Int from, Vector2Int to)
    {
        var orig = BoardManager.Instance.logicalBoard[to.x, to.y];
        BoardManager.Instance.logicalBoard[from.x, from.y] = null;
        BoardManager.Instance.logicalBoard[to.x, to.y] = gameObject;
        var old = currentGridPosition; currentGridPosition = to;
        bool bad = gameController.IsKingInCheck(tag);
        currentGridPosition = old;
        BoardManager.Instance.logicalBoard[from.x, from.y] = gameObject;
        BoardManager.Instance.logicalBoard[to.x, to.y] = orig;
        return bad;
    }

    /* ───────────────────────────────────────────────
   NEW: Network-side execution helpers
─────────────────────────────────────────────── */

    // Server-only: contains ALL board-mutating code you copied out of OnMouseUp
    [Server]
    public void ServerPerformMove(Vector2Int from, Vector2Int to, bool promo)
    {
        /* 1) initialise the context variables the old code expects */
        originalGridPosition = from;
        currentGridPosition = from;

        Vector2Int targetGrid = to;
        GameObject targetPiece = BoardManager.Instance.logicalBoard[to.x, to.y];
        int dx = targetGrid.x - originalGridPosition.x;
        int dy = targetGrid.y - originalGridPosition.y;

        // paste — START of board-mutation block (everything from
        // “Witch special” down to the final HighlightManager.ClearHighlights)
        // **delete** SnapToGrid calls – visuals happen client-side
        // **delete** HighlightManager.ClearHighlights – client-side only
        // keep Destroy/Instantiate/RecordMove/etc.
        // paste — END

        // Witch special: convert neighbors on click-in-place
        if (type == PieceType.Witch && targetGrid == originalGridPosition)
        {
            foreach (var nb in BoardManager.Instance.GetNeighborCoords(originalGridPosition))
            {
                var victim = BoardManager.Instance.logicalBoard[nb.x, nb.y];
                if (victim != null)
                {
                    Destroy(victim);
                    var pawnPrefab = (tag == "White") ? MyCiv.whitePawnPrefab : MyCiv.blackPawnPrefab;
                    var go = Instantiate(pawnPrefab,
                                         BoardManager.Instance.GridToWorld(nb.x, nb.y),
                                         Quaternion.identity,
                                         gameController.PiecesParent.transform);
                    go.tag = tag;
                    BoardManager.Instance.logicalBoard[nb.x, nb.y] = go;
                }
            }
            hasMoved = true;
            HighlightManager.Instance.ClearHighlights();
            return;
        }

        // Britons: attack-only capture (diagonal or 2-forward), pawn does NOT move
        if (type == PieceType.Pawn && MyCiv.name == "Britons" && targetPiece != null)
        {
            int dir = (tag == "White") ? 1 : -1;
            bool diag = Mathf.Abs(dx) == 1 && dy == dir;
            bool twoFwd = dx == 0 && dy == 2 * dir;
            if (diag || twoFwd)
            {
                // … inside the Britons capture block …
                Destroy(targetPiece);
                BoardManager.Instance.logicalBoard[targetGrid.x, targetGrid.y] = null;

                // ← ADD THIS LINE to snap back to original square:

                gameController.RecordMove(type, originalGridPosition, currentGridPosition, true);
                // …

                hasMoved = true;
                HighlightManager.Instance.ClearHighlights();
                return;
            }
        }

        bool didEnPassant = false;


        // Castling
        if (type == PieceType.King && !hasMoved && targetGrid.y == originalGridPosition.y && Mathf.Abs(dx) == 2)
        {
            AttemptCastling(dx > 0);
        }
        else
        {
            // En passant
            if (type == PieceType.Pawn && targetPiece == null)
            {
                int dir = (tag == "White") ? 1 : -1;
                if (Mathf.Abs(targetGrid.x - originalGridPosition.x) == 1 && targetGrid.y - originalGridPosition.y == dir)
                {
                    var ep = gameController.enPassantEligiblePawn;
                    if (ep != null && ep.currentGridPosition.x == targetGrid.x && ep.currentGridPosition.y == originalGridPosition.y)
                    {
                        Destroy(ep.gameObject);
                        BoardManager.Instance.logicalBoard[targetGrid.x, originalGridPosition.y] = null;
                        didEnPassant = true;
                    }
                }
            }

            // Capture and civ-specific losses
            PieceController capturedPC = null;
            if (!didEnPassant && targetPiece != null && targetPiece.tag != tag)
            {
                capturedPC = targetPiece.GetComponent<PieceController>();
                Destroy(targetPiece);
                if (MyCiv.name == "French")
                {
                    bool hasPawn = false;
                    foreach (Transform t in gameController.PiecesParent.transform)
                        if (t.tag == tag && t.GetComponent<PieceController>().type == PieceType.Pawn)
                        { hasPawn = true; break; }
                    if (!hasPawn) gameController.GameOver = true;
                }
                if (MyCiv.name == "Spanish")
                {
                    bool hasKnight = false;
                    foreach (Transform t in gameController.PiecesParent.transform)
                        if (t.tag == tag && t.GetComponent<PieceController>().type == PieceType.Knight)
                        { hasKnight = true; break; }
                    if (!hasKnight) gameController.GameOver = true;
                }
            }



            // Move piece
            BoardManager.Instance.logicalBoard[currentGridPosition.x, currentGridPosition.y] = null;
            currentGridPosition = targetGrid;
            BoardManager.Instance.logicalBoard[currentGridPosition.x, currentGridPosition.y] = gameObject;

            // EP eligibility
            if (type == PieceType.Pawn && Mathf.Abs(currentGridPosition.y - originalGridPosition.y) == 2)
                gameController.enPassantEligiblePawn = this;
            else
                gameController.enPassantEligiblePawn = null;

            CheckForPromotion();

            bool didCapture = !didEnPassant && (targetPiece != null && targetPiece.tag != tag);
            bool didCastle = type == PieceType.King && Mathf.Abs(currentGridPosition.x - originalGridPosition.x) == 2;
            gameController.RecordMove(type, originalGridPosition, currentGridPosition, didCapture, didEnPassant, didCastle);

            // after you’ve destroyed the captured piece and stored its PieceController in capturedPC:
            if (type == PieceType.Witch && didCapture && capturedPC != null)
            {
                // Adopt the new logical type
                type = capturedPC.type;

                // Now swap out this GameObject’s artwork to match the proper prefab
                var sr = GetComponent<SpriteRenderer>();

                switch (type)
                {
                    // for all your other built‑in chess pieces, you can still use the
                    // existing sprite fields on this script:
                    case PieceType.Knight:
                        sr.sprite = (tag == "White") ? whiteKnightSprite : blackKnightSprite;
                        break;
                    case PieceType.Bishop:
                        sr.sprite = (tag == "White") ? whiteBishopSprite : blackBishopSprite;
                        break;
                    case PieceType.Rook:
                        sr.sprite = (tag == "White") ? whiteRookSprite : blackRookSprite;
                        break;
                    case PieceType.Queen:
                        sr.sprite = (tag == "White") ? whiteQueenSprite : blackQueenSprite;
                        break;
                    case PieceType.Pawn:
                        sr.sprite = (tag == "White") ? whitePawnSprite : blackPawnSprite;
                        break;
                    case PieceType.King:
                        // assume you have a whiteKingSprite / blackKingSprite if needed
                        break;
                }

                Vector3 fallbackScale;
                switch (type)
                {
                    case PieceType.Pawn: fallbackScale = new Vector3(2, 2, 1); break;
                    case PieceType.Knight: fallbackScale = new Vector3(2, 2, 1); break;
                    case PieceType.Bishop: fallbackScale = new Vector3(2, 2, 1); break;
                    case PieceType.Rook: fallbackScale = new Vector3(2, 2, 1); break;
                    case PieceType.Queen: fallbackScale = new Vector3(2, 2, 1); break;
                    case PieceType.King: fallbackScale = new Vector3(2, 2, 1); break;
                    case PieceType.Horseman: fallbackScale = new Vector3(2, 2, 1); break;
                    case PieceType.Witch: fallbackScale = new Vector3(1, 1, 1); break;
                    default: fallbackScale = transform.localScale; break;
                }
                transform.localScale = fallbackScale;

            }


            // only Haitian witches, and only witches of the victim’s color
            if (didCapture && MyCiv.name == "Haitians")
            {
                foreach (var neighbor in BoardManager.Instance.GetNeighborCoords(targetGrid))
                {
                    var obj = BoardManager.Instance.logicalBoard[neighbor.x, neighbor.y];
                    if (obj != null)
                    {
                        var pc = obj.GetComponent<PieceController>();
                        // obj.tag != this.tag ensures we only look at *enemy* witches
                        if (pc != null && pc.type == PieceType.Witch && obj.tag != this.tag)
                        {
                            Destroy(this.gameObject);
                            break;
                        }
                    }
                }
            }

            hasMoved = true;
        }
    }

    // Runs on every client (including host) to update visuals only
    [ClientRpc]
    public void RpcApplyMove(Vector2Int from, Vector2Int to, bool promo)
    {
        originalGridPosition = from;
        currentGridPosition = to;
        SnapToGrid(to);
        HighlightManager.Instance.ClearHighlights();
    }

}
