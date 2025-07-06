using Mirror;

public enum PlayerColor { White, Black }

public class PlayerState : NetworkBehaviour
{
    [SyncVar]
    public PlayerColor Color;
}
