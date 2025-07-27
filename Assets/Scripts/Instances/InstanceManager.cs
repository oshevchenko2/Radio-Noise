using UnityEngine;
using TerrainGenerator;
using Player;

public class InstanceManager : MonoBehaviour
{
    public static InstanceManager Instance { get; private set; }

    public Loading.Loading Loading;
    public VoxelTerrain Terrain;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }
}
