using System.Collections.Generic;
using FishNet.Object;
using FishNet.Connection;

public class LobbyManager : NetworkBehaviour
{
    public static LobbyManager Instance;

    private Dictionary<NetworkConnection, string> players = new();

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdAddPlayer(string playerName, NetworkConnection sender = null)
    {
        if (!IsHostInitialized) return;
        
        players[sender] = playerName;
        UpdateLobbyUI();
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdRemovePlayer(NetworkConnection conn, NetworkConnection sender = null)
    {
        if (!IsHostInitialized) return;

        if (players.ContainsKey(conn))
        {
            players.Remove(conn);
            UpdateLobbyUI();
        }
    }

    public void UpdatePlayerReadyStatus(NetworkConnection conn, bool isReady)
    {
        UpdateLobbyUI();

        if (AllPlayersReady())
        {
            LobbyUI ui = FindFirstObjectByType<LobbyUI>();
            ui?.SetStartGameButtonInteractable(true);
        }
    }

    private bool AllPlayersReady()
    {
        foreach (var kvp in players)
        {
            NetworkObject obj = kvp.Key.FirstObject;
            if (obj != null && obj.TryGetComponent(out PlayerLobbyController plc))
            {
                if (!plc.IsReady.Value)
                    return false;
            }
        }
        return true;
    }

    public void TryStartGame()
    {
        if (!AllPlayersReady())
            return;

        UnityEngine.Debug.Log("All players are ready. Starting the game...");
    }

    private void UpdateLobbyUI()
    {
        foreach (NetworkConnection conn in players.Keys)
        {
            TargetUpdatePlayerList(conn, players);
        }
    }

    [TargetRpc]
    private void TargetUpdatePlayerList(NetworkConnection target, Dictionary<NetworkConnection, string> currentPlayers)
    {
        LobbyUI lobbyUI = FindFirstObjectByType<LobbyUI>();
        if (lobbyUI == null) return;

        lobbyUI.ClearPlayerList();

        foreach (var kvp in currentPlayers)
        {
            lobbyUI.AddPlayerLabel(kvp.Key, kvp.Value);
        }

        foreach (var kvp in currentPlayers)
        {
            NetworkObject obj = kvp.Key.FirstObject;
            if (obj != null && obj.TryGetComponent(out PlayerLobbyController plc) && plc.IsReady.Value)
            {
                lobbyUI.SetPlayerReady(kvp.Key);
            }
        }
    }

    [Server]
    public void KickPlayer(NetworkConnection conn)
    {
        if (!IsHostInitialized) return;
        
        if (conn != null)
        {
            conn.Disconnect(true);
            CmdRemovePlayer(conn);
        }
    }
}
