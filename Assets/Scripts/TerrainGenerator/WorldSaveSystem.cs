using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using UnityEngine;

namespace TerrainGenerator
{
    public class WorldSaveSystem : MonoBehaviour
    {
        /*
        public static WorldSaveSystem Instance { get; private set; }
        public VoxelTerrain Terrain;
        private string _filePath;

        void Awake()
        {
            _filePath = Path.Combine(Application.persistentDataPath, "world.radionoise");
            CreateInstance();
        }

        void CreateInstance()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public async void SaveWorld()
        {
            if (Terrain == null)
            {
                Debug.LogError("Terrain is null");
                return;
            }

            WorldData data = new WorldData
            {
                SeedX = Terrain.GetSeedX(),
                SeedZ = Terrain.GetSeedZ(),
                BiomeMapEntries = new List<BiomeMapEntry>(),
                ChunkDatas = new List<ChunkData>()
            };

            Dictionary<Vector2Int, BiomeType> biomeMap = Terrain.GetBiomeMap();
            foreach (var kvp in biomeMap)
            {
                data.BiomeMapEntries.Add(new BiomeMapEntry
                {
                    ChunkX = kvp.Key.x,
                    ChunkZ = kvp.Key.y,
                    BiomeType = (int)kvp.Value
                });
            }

            Dictionary<Vector2Int, GameObject> chunks = Terrain.GetChunks();
            foreach (var kvp in chunks)
            {
                Vector2Int coord = kvp.Key;
                GameObject chunkObj = kvp.Value;
                ChunkData chunkData = new ChunkData
                {
                    ChunkX = coord.x,
                    ChunkZ = coord.y
                };

                Transform bottomTransform = chunkObj.transform.Find("BottomLayer");
                Transform caveTransform = chunkObj.transform.Find("CaveLayer");
                Transform topTransform = chunkObj.transform.Find("TopLayer");

                if (bottomTransform != null)
                {
                    MeshFilter mf = bottomTransform.GetComponent<MeshFilter>();
                    if (mf != null && mf.mesh != null)
                        chunkData.BottomMesh = MeshToMeshData(mf.mesh);
                }

                if (caveTransform != null)
                {
                    MeshFilter mf = caveTransform.GetComponent<MeshFilter>();
                    if (mf != null && mf.mesh != null)
                        chunkData.CaveMesh = MeshToMeshData(mf.mesh);
                }

                if (topTransform != null)
                {
                    MeshFilter mf = topTransform.GetComponent<MeshFilter>();
                    if (mf != null && mf.mesh != null)
                        chunkData.TopMesh = MeshToMeshData(mf.mesh);
                }

                data.ChunkDatas.Add(chunkData);
            }

            await Task.Run(() =>
            {
                BinaryFormatter formatter = new BinaryFormatter();
                using FileStream stream = new FileStream(_filePath, FileMode.Create);
                using GZipStream gzStream = new GZipStream(stream, CompressionMode.Compress);
                formatter.Serialize(gzStream, data);
            });

            Debug.Log("Saved: " + _filePath);
        }

        MeshData MeshToMeshData(Mesh mesh)
        {
            return new MeshData
            {
                Vertices = mesh.vertices.Select(v => new SerializableVector3(v)).ToArray(),
                Triangles = mesh.triangles
            };
        }

        public async void LoadWorld()
        {
            if (!File.Exists(_filePath))
            {
                Debug.LogError("Save not found");
                return;
            }

            WorldData data = null;
            await Task.Run(() =>
            {
                BinaryFormatter formatter = new BinaryFormatter();
                using FileStream stream = new FileStream(_filePath, FileMode.Open);
                using GZipStream gzStream = new GZipStream(stream, CompressionMode.Decompress);
                data = formatter.Deserialize(gzStream) as WorldData;
            });

            if (data == null)
            {
                Debug.LogError("Serializable data is null");
                return;
            }
            if (Terrain == null)
            {
                Debug.LogError("Terrain is null");
                return;
            }

            StartCoroutine(Terrain.DestroyAllChunksAsync());
            Terrain.SetSeed(data.SeedX, data.SeedZ);
            Terrain.LoadBiomeMap(ConvertBiomeMap(data.BiomeMapEntries));

            foreach (ChunkData chunkData in data.ChunkDatas)
            {
                Vector2Int coord = new Vector2Int(chunkData.ChunkX, chunkData.ChunkZ);
                StartCoroutine(Terrain.RecreateChunkAsync(coord, chunkData));
            }

            Debug.Log("WorldLoaded: " + _filePath);
        }

        Dictionary<Vector2Int, BiomeType> ConvertBiomeMap(List<BiomeMapEntry> entries)
        {
            Dictionary<Vector2Int, BiomeType> map = new Dictionary<Vector2Int, BiomeType>();
            foreach (BiomeMapEntry entry in entries)
            {
                Vector2Int coord = new Vector2Int(entry.ChunkX, entry.ChunkZ);
                map[coord] = (BiomeType)entry.BiomeType;
            }
            return map;
        }*/
    }
}
