using UnityEngine;
using System.Collections.Generic;
using FishNet;
using FishNet.Managing;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Transporting;
using FishNet.Object.Synchronizing;

public class LobbyUI : MonoBehaviour
{
    public GameObject lobbyPanel;
    public Transform playerListParent;
    public GameObject playerLabelPrefab;
    public GameObject kickButtonPrefab;

    public UnityEngine.UI.Button startGameButton;
    public UnityEngine.UI.Button readyButton;
    public UnityEngine.UI.Button leaveButton;

    private Dictionary<NetworkConnection, PlayerLabelUI> playerLabels = new();

    private void Start()
    {
        lobbyPanel.SetActive(false);

        if (startGameButton != null)
            startGameButton.gameObject.SetActive(false);
        if (readyButton != null)
            readyButton.gameObject.SetActive(false);
        if (leaveButton != null)
            leaveButton.gameObject.SetActive(false);

        InstanceFinder.ClientManager.OnClientConnectionState += OnClientConnectionState;

        if (startGameButton != null)
            startGameButton.onClick.AddListener(OnStartGameClicked);

        if (readyButton != null)
            readyButton.onClick.AddListener(OnReadyClicked);

        if (leaveButton != null)
            leaveButton.onClick.AddListener(OnLeaveClicked);
    }

    private void OnDestroy()
    {
        InstanceFinder.ClientManager.OnClientConnectionState -= OnClientConnectionState;
    }

    private void OnClientConnectionState(ClientConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Started)
        {
            lobbyPanel.SetActive(true);

            bool isHost = InstanceFinder.IsServerStarted && InstanceFinder.IsClientStarted;

            if (startGameButton != null)
                startGameButton.gameObject.SetActive(isHost);

            if (readyButton != null)
                readyButton.gameObject.SetActive(!isHost);

            if (leaveButton != null)
                leaveButton.gameObject.SetActive(true);
        }
        else if (args.ConnectionState == LocalConnectionState.Stopped)
        {
            lobbyPanel.SetActive(false);
            ClearPlayerList();

            if (startGameButton != null)
                startGameButton.gameObject.SetActive(false);
            if (readyButton != null)
                readyButton.gameObject.SetActive(false);
            if (leaveButton != null)
                leaveButton.gameObject.SetActive(false);
        }
    }

    private void OnLeaveClicked()
    {
        if (InstanceFinder.IsServerStarted)
            InstanceFinder.ServerManager.StopConnection(true);

        if (InstanceFinder.IsClientStarted)
            InstanceFinder.ClientManager.StopConnection();
    }

    public void AddPlayerLabel(NetworkConnection conn, string playerName)
    {
        if (playerLabels.ContainsKey(conn)) return;

        GameObject go = Instantiate(playerLabelPrefab, playerListParent);
        PlayerLabelUI label = go.GetComponent<PlayerLabelUI>();
        label.SetName(playerName);
        playerLabels[conn] = label;

        bool isHost = InstanceFinder.IsServerStarted && InstanceFinder.IsClientStarted;
        if (isHost && conn != InstanceFinder.ClientManager.Connection)
        {
            GameObject kickBtn = Instantiate(kickButtonPrefab, go.transform);
            kickBtn.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(() =>
            {
                LobbyManager.Instance.KickPlayer(conn);
            });
        }
    }

    public void SetPlayerReady(NetworkConnection conn)
    {
        if (playerLabels.TryGetValue(conn, out PlayerLabelUI label))
        {
            label.MarkReady();
        }
    }

    public void SetStartGameButtonInteractable(bool interactable)
    {
        if (startGameButton != null)
            startGameButton.interactable = interactable;
    }

    public void ClearPlayerList()
    {
        foreach (var entry in playerLabels.Values)
        {
            Destroy(entry.gameObject);
        }
        playerLabels.Clear();
    }

    private void OnReadyClicked()
    {
        var localPlayer = InstanceFinder.ClientManager.Connection.FirstObject;
        if (localPlayer != null && localPlayer.TryGetComponent(out PlayerLobbyController plc))
        {
            plc.CmdSetReady();
            readyButton.interactable = false;
        }
    }

    private void OnStartGameClicked()
    {
        LobbyManager.Instance?.TryStartGame();
    }
}
