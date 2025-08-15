using UnityEngine;
using TerrainGenerator;

public class InstanceManager : MonoBehaviour
{
    public static InstanceManager Instance { get; private set; }

    public Loading.Loading Loading;
    public VoxelTerrain Terrain;

    private void Awake() => Instance = this;
}
