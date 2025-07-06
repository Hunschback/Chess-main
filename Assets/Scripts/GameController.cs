// Assets/Scripts/GameController.cs
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using UnityEngine.UI;
using UnityEngine.SceneManagement;


using Mirror;          // add this at the top




public class GameController : NetworkBehaviour


{
    [SyncVar] public bool WhiteTurn = true;
    [SyncVar] public bool GameOver = false;
    public static GameController Instance; void Awake() => Instance = this;

    /* ───────────────────────────────────────────────────────
       AUTHORITATIVE SERVER MOVE  (called from CmdTryMove)
       returns true if move succeeds, false if illegal
    ─────────────────────────────────────────────────────── */
    [Server]
    public bool PerformMove(Vector2Int from, Vector2Int to, bool promo,
                        NetworkConnectionToClient sender = null)
    {
        var pieceGO = BoardManager.Instance.logicalBoard[from.x, from.y];
        if (pieceGO == null) return false;

        var pc = pieceGO.GetComponent<PieceController>();
        bool pieceIsWhite = pieceGO.CompareTag("White");

        // NEW – make sure the player who sent the Command owns this colour
        var ps = sender.identity.GetComponent<PlayerState>();
        bool senderIsWhite = ps.Color == PlayerColor.White;
        if ((senderIsWhite && !pieceIsWhite) || (!senderIsWhite && pieceIsWhite))
            return false;           // trying to move opponent’s piece

        // existing turn check
        if ((WhiteTurn && !pieceIsWhite) || (!WhiteTurn && pieceIsWhite))
            return false;

        // 2) validate basic legality
        GameObject targetPiece = BoardManager.Instance.logicalBoard[to.x, to.y];
        if (!pc.IsValidMove(from, to, targetPiece)) return false;
        if (pc.MoveExposesKing(from, to)) return false;

        // 3) execute – this calls the extracted mutation code you’ll add next
        pc.ServerPerformMove(from, to, promo);

        // 4) toggle turn, update state & UI
        WhiteTurn = !WhiteTurn;
        UpdateKingCheckStatus();
        CheckForGameEnd();

        return true;
    }

    [HideInInspector] public CivConfiguration WhiteCiv;
    [HideInInspector] public CivConfiguration BlackCiv;

    [Header("Civ Setup")]
    public CivConfiguration defaultCivConfig;

    // For en passant
    [HideInInspector] public PieceController enPassantEligiblePawn;

    [Header("Game Over UI")]
    public GameObject gameOverPanel;      // assign in Inspector
    public Text gameOverTitle;            // assign in Inspector (optional)


    // Board & piece parents
    public GameObject Board;
    public GameObject PiecesParent;

    // Turn & selection
    public GameObject SelectedPiece;

    // Kings (for check + castling through check)
    // We'll auto-assign these in NewGame(); no Inspector hookup required
    public GameObject WhiteKing;
    public GameObject BlackKing;

    // Move history UI
    public MoveHistoryUI historyUI;

    /// A single half‐move in the game.
    public struct Move
    {
        public PieceController.PieceType piece;
        public Vector2Int from, to;
        public bool isCapture, isEnPassant, isCastling;
    }

    /// Full list of all half‐moves (white+black).
    [HideInInspector]
    public List<Move> moveHistory = new List<Move>();

    void Start()
    {
        // server builds the board
        if (isServer)
        {
            // 1) Choose civ configs

            if (defaultCivConfig == null)
            {
                // Load the first config found rather than a hard-coded path
                var all = Resources.LoadAll<CivConfiguration>("CivConfigs");
                if (all.Length > 0) defaultCivConfig = all[0];
                else
                {
                    Debug.LogError("No CivConfiguration found in Resources/CivConfigs!");
                    return;                 // abort; server will log but continue running
                }
}
            var w = MenuController.SelectedWhiteCiv ?? defaultCivConfig;
            var b = MenuController.SelectedBlackCiv ?? defaultCivConfig;

            if (w.startingPositions.Count == 0)
            {
                Debug.LogError($"{w.name} has zero StartingPositions!");
                return;
            }


            WhiteCiv = w;
            BlackCiv = b;
            if (w != null && b != null)
                NewGame(w, b);
            else
                Debug.LogError("Cannot start game: Civ configs missing");

        }

        // 2) Hide game-over UI on every client until needed
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }




    /// <summary>
    /// Single‑config overload: white and black use the *same* layout.
    /// </summary>
    public void NewGame(CivConfiguration config)
        => NewGame(config, config);

    /// <summary>
    /// Builds a fresh board using *two* CivConfigurations:
    /// one for White, one for Black.
    /// </summary>
    public void NewGame(CivConfiguration whiteConfig, CivConfiguration blackConfig)
    {
        // ensure we have a parent container
        if (PiecesParent == null)
        {
            PiecesParent = new GameObject("AllPieces");
            // keep every piece as a *child* of the board, so they follow it if it moves
            PiecesParent.transform.SetParent(BoardManager.Instance.boardRoot, false);
        }

        // 1) destroy any existing pieces
        foreach (Transform t in PiecesParent.transform)
            Destroy(t.gameObject);

        // 2) clear the logical board
        for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
                BoardManager.Instance.logicalBoard[x, y] = null;

        // 3) reset king refs
        WhiteKing = BlackKing = null;

        // 4) spawn White pieces
        foreach (var entry in whiteConfig.startingPositions)
        {
            Vector3 wpos = BoardManager.Instance.GridToWorld(entry.gridPos.x, entry.gridPos.y);
            var go = Instantiate(entry.whitePrefab, wpos, Quaternion.identity, PiecesParent.transform);
            go.tag = "White";
            BoardManager.Instance.logicalBoard[entry.gridPos.x, entry.gridPos.y] = go;

            // NEW: spawn so all clients get a NetworkIdentity
            if (NetworkServer.active) NetworkServer.Spawn(go);

            var pc = go.GetComponent<PieceController>();
            if (pc != null)
            {
                pc.currentGridPosition = entry.gridPos;
                if (pc.type == PieceController.PieceType.King) WhiteKing = go;
            }
        }

        // 5) Spawn Black pieces by mirroring *only* on the horizontal axis (invert y, keep x)
        foreach (var entry in blackConfig.startingPositions)
        {
            int mx = entry.gridPos.x;          // same file
            int my = 7 - entry.gridPos.y;      // flip rank
            Vector3 bpos = BoardManager.Instance.GridToWorld(mx, my);

            var go = Instantiate(entry.blackPrefab, bpos, Quaternion.identity, PiecesParent.transform);
            go.tag = "Black";
            BoardManager.Instance.logicalBoard[mx, my] = go;

            // inside the black-piece foreach:
            if (NetworkServer.active) NetworkServer.Spawn(go);   // ← ADD THIS


            var pc = go.GetComponent<PieceController>();
            if (pc != null)
            {
                pc.currentGridPosition = new Vector2Int(mx, my);
                if (pc.type == PieceController.PieceType.King) BlackKing = go;
            }
        }


        // 6) reset state & UI
        WhiteTurn = true;
        enPassantEligiblePawn = null;
        moveHistory.Clear();
        if (historyUI != null)
            historyUI.Refresh(moveHistory);
        UpdateKingCheckStatus();
    }

    /// Record a half‑move and update the UI
    public void RecordMove(PieceController.PieceType piece, Vector2Int from, Vector2Int to,
                           bool isCapture = false, bool isEnPassant = false, bool isCastling = false)
    {
        moveHistory.Add(new Move
        {
            piece = piece,
            from = from,
            to = to,
            isCapture = isCapture,
            isEnPassant = isEnPassant,
            isCastling = isCastling
        });
        if (historyUI != null)
            historyUI.Refresh(moveHistory);
    }

    /// Check if any enemy piece attacks a given square
    public bool IsSquareUnderAttack(Vector2Int square, string ownTeam)
    {
        var board = BoardManager.Instance.logicalBoard;
        for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
            {
                var attacker = board[x, y];
                if (attacker == null || attacker.CompareTag(ownTeam))
                    continue;

                var pc = attacker.GetComponent<PieceController>();
                if (pc != null)
                {
                    var from = new Vector2Int(x, y);
                    var kingObj = board[square.x, square.y];
                    if (pc.IsValidMove(from, square, kingObj))
                        return true;
                }
            }
        return false;
    }

    /// Is the specified side's king currently in check?
    public bool IsKingInCheck(string team)
    {
        var kingObj = team == "White" ? WhiteKing : BlackKing;
        // if you somehow still don't have a king, treat as not in check
        if (kingObj == null) return false;
        var pos = kingObj.GetComponent<PieceController>().currentGridPosition;
        return IsSquareUnderAttack(pos, team);
    }

    public void UpdateKingCheckStatus()
    {
        HighlightKing("White", IsKingInCheck("White"));
        HighlightKing("Black", IsKingInCheck("Black"));
    }

    void HighlightKing(string team, bool inCheck)
    {
        var king = team == "White" ? WhiteKing : BlackKing;
        if (king == null) return;
        var sr = king.GetComponent<SpriteRenderer>();
        if (sr != null)
            sr.color = inCheck ? Color.red : Color.white;
    }

    /// <summary>
    /// Returns true if the given side has at least one legal move.
    /// </summary>
    bool PlayerHasAnyLegalMove(string team)
    {
        var board = BoardManager.Instance.logicalBoard;
        for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
            {
                var go = board[x, y];
                if (go != null && go.CompareTag(team))
                {
                    var pc = go.GetComponent<PieceController>();
                    if (pc != null && pc.GetLegalMoves().Count > 0)
                        return true;
                }
            }
        return false;
    }

    /// <summary>
    /// Call after each turn flip to detect checkmate or stalemate.
    /// </summary>
    public void CheckForGameEnd()
    {
        string toMove = WhiteTurn ? "White" : "Black";
        bool inCheck = IsKingInCheck(toMove);
        bool hasMoves = PlayerHasAnyLegalMove(toMove);

        if (!hasMoves)
        {
            GameOver = true;

            // 1) Show the panel
            gameOverPanel.SetActive(true);

            // 2) Update title text
            if (gameOverTitle != null)
            {
                if (inCheck)
                    gameOverTitle.text = toMove + " is checkmated!";
                else
                    gameOverTitle.text = "Stalemate!";
            }
        }
    }

    /// Called by the ReturnButton’s OnClick()
    public void ReturnToMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }

}
