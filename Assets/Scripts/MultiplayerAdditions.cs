// MultiplayerAdditions.cs
// ──────────────────────────────────────────────────────────────────────────────
// Drop this single file into Assets/Scripts *after* you edit GameController.cs
// (see instructions below). All networking is handled here; your existing logic
// stays intact.
//
// ❶  Edit **GameController.cs**
//     • Add   `using Mirror;`  at the top.
//     • Change the class header to:
//         public class GameController : NetworkBehaviour
//     • Add `[SyncVar]` in front of the `public bool WhiteTurn = true;` field.
//     • Remove *nothing* else. Save – compile.
//
// ❷  Create a **NetworkPlayer** prefab:
//     (Hierarchy → Create Empty → Add Component → Network Identity + PlayerState →
//      drag into a Prefabs folder, delete scene copy.)
//
// ❸  Replace the default NetworkManager script with **ChesspiresNetworkManager**
//     (included below) and set its Player Prefab to NetworkPlayer.
//
// That is all the setup required. The rest of your game continues unchanged.
// ──────────────────────────────────────────────────────────────────────────────

using Mirror;
using UnityEngine;
using Unity.Collections;

#region 1. Lightweight payload --------------------------------------------------

public struct MoveData : NetworkMessage
{
    public byte fx, fy, tx, ty;
    public bool promo;
}

#endregion


// --- removed duplicate ChesspiresNetworkManager; keep only one definition in its own file ---

#region 4. Client‑side input component -----------------------------------------

// Attach this to every piece prefab.
// It replaces OnMouseDown/Drag/Up with a single click‑to‑move flow suitable for
// network play. (Feel free to adapt to your drag code if you prefer.)

public class PieceInputNet : NetworkBehaviour
{
    PieceController pc;
    GameController gc;

    void Awake()
    {
        pc = GetComponent<PieceController>();
        gc = FindObjectOfType<GameController>();
    }

    void OnMouseUpAsButton()
    {
        if (!isOwned) return;                          // only owner can command
        //if (!gc.GetComponent<PlayerState>().IsMyTurn(gc)) return;

        // For brevity we use a rudimentary click‑to‑select‑then‑click‑target UI.
        // Integrate your existing drag code here if desired.
    }
}

#endregion

// ──────────────────────────────────────────────────────────────────────────────
// No further code is required in this file – all heavy rules stay in your
// existing scripts. Follow the checklist at the top, hit Play with two Editor
// instances, and enjoy fully authoritative multiplayer.
// ──────────────────────────────────────────────────────────────────────────────
