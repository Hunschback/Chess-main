using Mirror;

public class ChesspiresNetworkManager : NetworkManager
{
    // assigns White to the first client that connects, Black to the second
    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {

        if (conn.identity != null)        // the player already exists
            return;                        // ignore the 2nd request

        // Let Mirror create the player object first
        base.OnServerAddPlayer(conn);

        // Grab the PlayerState component that should already be on the player prefab
        PlayerState p = conn.identity.GetComponent<PlayerState>();

        // Safety net – add it if someone forgot
        if (p == null) p = conn.identity.gameObject.AddComponent<PlayerState>();

        // First player gets White, second gets Black
        p.Color = numPlayers == 1 ? PlayerColor.White : PlayerColor.Black;
    }
}
