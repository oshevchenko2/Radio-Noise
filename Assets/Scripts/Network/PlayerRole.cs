using FishNet.Managing;
using UnityEngine;

namespace Network
{
    public class PlayerRole : MonoBehaviour
    {
        public NetworkManager networkManager;

        public void StartHost()
        {
            networkManager.ServerManager.StartConnection();
            networkManager.ClientManager.StartConnection();
            Debug.Log("Host connected.");
        }

        public void StartClient()
        {
            networkManager.ClientManager.StartConnection();
            Debug.Log("Connecting...");
        }
    }
}