using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.Serialization.Formatters.Binary;
using Unity.Entities;
using UnityEngine;

namespace TerrainGenerator
{
    public class WorldSaveSystem : MonoBehaviour
    {
        public VoxelTerrain Terrain;
        private string _filePath;

        private void Awake()
        {
            _filePath = Path.Combine(Application.persistentDataPath, "world.radionoise");
        }

        public void SaveWorld()
        {
            if(Terrain == null)
            {
                Debug.LogError("0xFFFFF");
                return;
            }

            WorldData Data = new()
            {
                SeedX = Terrain.GetSeedX(),
                SeedZ = Terrain.GetSeedZ(),
                BiomeMapEntries = new List<BiomeMapEntry>(),
                ChunkDatas = new List<ChunkData>()
            };

            Dictionary<Vector2Int, BiomeType> biomeMap = Terrain.GetBiomeMap();
            foreach (var kvp in biomeMap)
            {
                BiomeMapEntry entry = new()
                {
                    ChunkX = kvp.Key.x,
                    ChunkZ = kvp.Key.y,
                    BiomeType = (int)kvp.Value
                };
                Data.BiomeMapEntries.Add(entry);
            }

            Dictionary<Vector2Int, GameObject> chunks = Terrain.GetChunks();
            foreach (var kvp in chunks)
            {
                Vector2Int coord = kvp.Key;
                GameObject chunkObj = kvp.Value;
                ChunkData chunkData = new()
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
                Data.ChunkDatas.Add(chunkData);
            }

            BinaryFormatter formatter = new();
            using FileStream stream = new(_filePath, FileMode.Create);
            formatter.Serialize(stream, Data);
            Debug.Log("Saved to " + _filePath);
        }

        private MeshData MeshToMeshData(Mesh mesh)
        {
            MeshData mData = new()
            {
                Vertices = mesh.vertices,
                Triangles = mesh.triangles
            };
            return mData;
        }

        public void LoadWorld()
        {
            if (!File.Exists(_filePath))
            {
                Debug.LogError("0xAAAFF");
                return;
            }

            BinaryFormatter formatter = new();
            WorldData data;
            using (FileStream stream = new(_filePath, FileMode.Open))
            {
                data = formatter.Deserialize(stream) as WorldData;
            }

            if (data == null)
            {
                Debug.LogError("0xAAAAF");
                return;
            }

            if (Terrain == null)
            {
                Debug.LogError("0xFFFFF");
                return;
            }

            Terrain.SetSeed(data.SeedX, data.SeedZ);
            Terrain.LoadBiomeMap(ConvertBiomeMap(data.BiomeMapEntries));

            Terrain.DestroyAllChunks();

            foreach (ChunkData chunkData in data.ChunkDatas)
            {
                Vector2Int coord = new(chunkData.ChunkX, chunkData.ChunkZ);
                Terrain.RecreateChunk(coord, chunkData);
            }

            Debug.Log("World loaded from " + _filePath);
        }

        private Dictionary<Vector2Int, BiomeType> ConvertBiomeMap(List<BiomeMapEntry> entries)
        {
            Dictionary<Vector2Int, BiomeType> map = new();
            foreach (BiomeMapEntry entry in entries)
            {
                Vector2Int coord = new(entry.ChunkX, entry.ChunkZ);
                map[coord] = (BiomeType)entry.BiomeType;
            }
            return map;
        }
    }
}