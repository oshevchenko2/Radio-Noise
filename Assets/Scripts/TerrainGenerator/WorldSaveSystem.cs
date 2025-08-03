using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using FishNet.Object;
using UnityEngine;

namespace TerrainGenerator
{
    [Serializable]
    public class WorldSaveSystem : NetworkBehaviour
    {
        public VoxelTerrain Terrain;
        private string _filePath;

        private void Awake()
        {
            _filePath = Path.Combine(Application.persistentDataPath, "world.radionoise");
        }

        private void Update()
        {
            if (!IsOwner) return;

            if (Input.GetKeyDown(KeyCode.F3))
            {
                SaveWorld();
            }
            else if (Input.GetKeyDown(KeyCode.F9))
            {
                LoadWorld();
            }
            else if (Input.GetKeyDown(KeyCode.F1))
            {
                StartCoroutine(Terrain.DestroyAllChunks());
            }
            else return;
        }

        public async void SaveWorld()
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
                Transform stoneTransform = chunkObj.transform.Find("StoneLayer");
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

                if (stoneTransform != null)
                {
                    MeshFilter mf = stoneTransform.GetComponent<MeshFilter>();
                    if (mf != null && mf.mesh != null)
                        chunkData.StoneMesh = MeshToMeshData(mf.mesh);
                }

                if (topTransform != null)
                    {
                        MeshFilter mf = topTransform.GetComponent<MeshFilter>();
                        if (mf != null && mf.mesh != null)
                            chunkData.TopMesh = MeshToMeshData(mf.mesh);
                    }

                Data.ChunkDatas.Add(chunkData);
            }
            
            byte[] dataBytes = ObjectToByteArray(Data);
            await File.WriteAllBytesAsync(_filePath, dataBytes);
            
            Debug.Log("Saved to " + _filePath);
            Debug.Log(ObjectToByteArray(Data));
        }

        private MeshData MeshToMeshData(Mesh mesh)
        {
            MeshData mData = new()
            {
                Vertices = mesh.vertices.Select(sv => new SerializableVector3(sv)).ToArray(),
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

            WorldData data = ByteArrayToObject<WorldData>(File.ReadAllBytes(_filePath));
            Debug.Log(File.ReadAllBytes(_filePath));

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

            StartCoroutine(Terrain.DestroyAllChunks());
            
            Terrain.SetSeed(data.SeedX, data.SeedZ);
            Terrain.LoadBiomeMap(ConvertBiomeMap(data.BiomeMapEntries));
            
            foreach (ChunkData chunkData in data.ChunkDatas)
            {
                Vector2Int coord = new(chunkData.ChunkX, chunkData.ChunkZ);

                StartCoroutine(Terrain.RecreateChunk(coord, chunkData));
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

        public static byte[] ObjectToByteArray(object obj) 
        {
            using MemoryStream ms = new();
            using (GZipStream zs = new(ms, CompressionMode.Compress, true))
            {
                BinaryFormatter bf = new();
                bf.Serialize(zs, obj);
            }
            return ms.ToArray();
        }

        public static T ByteArrayToObject<T>(byte[] data)
        {
            using MemoryStream ms = new(data);
            using GZipStream zs = new(ms, CompressionMode.Decompress, true);
            BinaryFormatter bf = new();
            return (T)bf.Deserialize(zs);
        }
    }
}