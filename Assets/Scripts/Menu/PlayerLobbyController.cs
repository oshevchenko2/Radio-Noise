using FishNet.Object;
using FishNet.Connection;
using FishNet.Object.Synchronizing;
using FishNet.CodeGenerating;

public class PlayerLobbyController : NetworkBehaviour
{
    [AllowMutableSyncType]
    public SyncVar<string> PlayerName = new("Player");
    [AllowMutableSyncType]
    public SyncVar<bool> IsReady = new(false);

    [ServerRpc(RequireOwnership = true)]
    public void CmdSetReady()
    {
        IsReady.Value = true;
        LobbyManager.Instance.UpdatePlayerReadyStatus(Owner, IsReady.Value);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (IsOwner)
        {
            LobbyManager.Instance?.CmdAddPlayer(PlayerName.Value);
        }
    }

    public override void OnStopClient()
    {
        base.OnStopClient();

        if (IsOwner)
        {
            LobbyManager.Instance?.CmdRemovePlayer(NetworkObject.Owner);
        }
    }
}
