using System.Collections.Generic;
using System.Linq;
using FishNet.Object;
using UnityEngine;

namespace TerrainGenerator
{
    public class GrassGenerator : NetworkBehaviour
    {
        [SerializeField] private Mesh _grassMesh;
        [SerializeField] private Material _grassMaterial;

        private const float _grassDensity = 0.1f;
        private const float _grassMaxSlopeAngle = 90f;
        private const int _maxGrassPerChunk = 100;

        private readonly Dictionary<Vector2Int, List<GrassInstance>> _chunkGrassData = new();
        private readonly Dictionary<Vector3Int, GrassInstance> _allGrassInstances = new();
        private readonly Dictionary<Vector2Int, List<Vector3Int>> _chunkGrassKeys = new();

        private static GrassGenerator _instance;

        void Awake() => _instance = this;

        private static Vector3Int GetPositionKey(Vector3 pos, int offset = 0)
        {
            return new Vector3Int(
                Mathf.RoundToInt(pos.x * 100) + offset,
                Mathf.RoundToInt(pos.y * 100),
                Mathf.RoundToInt(pos.z * 100)
            );
        }

        public static void GenerateGrassForChunk(
            Vector2Int chunkCoord,
            Mesh topMesh,
            BiomeType dominantBiome0,
            BiomeType dominantBiome1,
            Transform parent,
            Vector3 worldPosition)
        {
            if (!_instance.IsGrassBiome(dominantBiome0) && !_instance.IsGrassBiome(dominantBiome1)) return;
            if (_instance._grassMesh == null || _instance._grassMaterial == null) return;

            if (_instance._chunkGrassData.ContainsKey(chunkCoord))
            {
                RemoveGrassFromChunk(chunkCoord);
            }

            Vector3[] vertices = topMesh.vertices;
            Vector3[] normals = topMesh.normals;

            List<GrassInstance> grassInstances = new();
            List<CombineInstance> combineInstances = new();
            List<Vector3Int> grassKeys = new();

            GameObject grassParent = new("Grass");
            grassParent.transform.SetParent(parent);
            grassParent.transform.localPosition = Vector3.zero;

            MeshFilter grassFilter = grassParent.AddComponent<MeshFilter>();
            MeshRenderer grassRenderer = grassParent.AddComponent<MeshRenderer>();
            grassRenderer.material = _instance._grassMaterial;

            int grassCount = 0;
            for (int i = 0; i < normals.Length; i++)
            {
                if (grassCount >= _maxGrassPerChunk) break;

                float slopeAngle = Vector3.Angle(normals[i], Vector3.up);
                if (slopeAngle > _grassMaxSlopeAngle) continue;

                if (UnityEngine.Random.value > _grassDensity) continue;

                Vector3 vertex = vertices[i];
                Vector3 position = vertex;

                position.x += UnityEngine.Random.Range(-0.3f, 0.3f);
                position.z += UnityEngine.Random.Range(-0.3f, 0.3f);

                Quaternion rotation = Quaternion.Euler(0, UnityEngine.Random.Range(0, 360f), 0);
                float scale = UnityEngine.Random.Range(0.8f, 1.2f);
                Vector3 scaleVec = new(scale, scale, scale);
                Matrix4x4 matrix = Matrix4x4.TRS(position, rotation, scaleVec);

                Vector3Int positionKey = Vector3Int.zero;
                bool keyIsUnique = false;
                int maxAttempts = 5;

                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    positionKey = GetPositionKey(position, attempt);

                    if (!_instance._allGrassInstances.ContainsKey(positionKey))
                    {
                        keyIsUnique = true;
                        break;
                    }
                }

                if (!keyIsUnique) continue;

                GrassInstance instance = new()
                {
                    Position = position,
                    Rotation = rotation,
                    Scale = scaleVec,
                    Matrix = matrix,
                    ChunkCoord = chunkCoord
                };

                grassInstances.Add(instance);
                grassKeys.Add(positionKey);
                _instance._allGrassInstances[positionKey] = instance;

                combineInstances.Add(new CombineInstance
                {
                    mesh = _instance._grassMesh,
                    transform = matrix
                });

                grassCount++;
            }

            _instance._chunkGrassData[chunkCoord] = grassInstances;
            _instance._chunkGrassKeys[chunkCoord] = grassKeys;

            if (combineInstances.Count > 0)
            {
                Mesh combinedMesh = new();
                combinedMesh.CombineMeshes(combineInstances.ToArray(), true);
                grassFilter.mesh = combinedMesh;
            }
            else
            {
                Destroy(grassParent);
            }
        }

        public struct GrassInstance
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
            public Matrix4x4 Matrix;
            public Vector2Int ChunkCoord;
        }

        private bool IsGrassBiome(BiomeType biome)
        {
            return biome switch
            {
                BiomeType.Plains or BiomeType.Forest => true,
                _ => false
            };
        }

        public void RemoveGrassInArea(Vector3 position, float radius)
        {
            if (!IsServerInitialized) return;

            float sqrRadius = radius * radius;
            HashSet<Vector2Int> affectedChunks = new();
            List<Vector3Int> keysToRemove = new();

            foreach (var kvp in _allGrassInstances)
            {
                float sqrDistance = (kvp.Value.Position - position).sqrMagnitude;
                if (sqrDistance <= sqrRadius)
                {
                    keysToRemove.Add(kvp.Key);
                    affectedChunks.Add(kvp.Value.ChunkCoord);
                }
            }

            foreach (var key in keysToRemove)
            {
                if (_allGrassInstances.TryGetValue(key, out GrassInstance instance))
                {
                    if (_chunkGrassKeys.TryGetValue(instance.ChunkCoord, out var chunkKeys))
                    {
                        chunkKeys.Remove(key);
                    }

                    _allGrassInstances.Remove(key);
                }
            }

            foreach (var chunkCoord in affectedChunks)
            {
                if (VoxelTerrain.ChunkObjects.TryGetValue(chunkCoord, out var chunkObj))
                {
                    RebuildGrassMesh(chunkCoord);
                    RebuildGrassMeshObserversRpc(chunkCoord);
                }
            }
        }

        [ObserversRpc]
        private void RebuildGrassMeshObserversRpc(Vector2Int chunkCoord)
        {
            if (!IsServerInitialized)
            {
                RebuildGrassMesh(chunkCoord);
            }
        }

        private void RebuildGrassMesh(Vector2Int chunkCoord)
        {
            if (!VoxelTerrain.ChunkObjects.TryGetValue(chunkCoord, out var chunkObj)) return;
            if (!_chunkGrassKeys.TryGetValue(chunkCoord, out var grassKeys)) return;

            Transform grassParent = chunkObj.transform.Find("Grass");
            if (grassParent == null) return;

            List<CombineInstance> combineInstances = new();

            foreach (var key in grassKeys)
            {
                if (_allGrassInstances.TryGetValue(key, out var instance))
                {
                    combineInstances.Add(new CombineInstance
                    {
                        mesh = _grassMesh,
                        transform = instance.Matrix
                    });
                }
            }

            MeshFilter grassFilter = grassParent.GetComponent<MeshFilter>();
            if (grassFilter == null) return;

            if (combineInstances.Count > 0)
            {
                Mesh combinedMesh = new();
                combinedMesh.CombineMeshes(combineInstances.ToArray(), true);
                grassFilter.mesh = combinedMesh;
            }
            else
            {
                Destroy(grassParent.gameObject);
            }
        }

        public static void RemoveGrassFromChunk(Vector2Int chunkCoord)
        {
            if (!_instance._chunkGrassData.TryGetValue(chunkCoord, out var grassInstances)) return;

            if (_instance._chunkGrassKeys.TryGetValue(chunkCoord, out var grassKeys))
            {
                foreach (var key in grassKeys)
                {
                    _instance._allGrassInstances.Remove(key);
                }
                _instance._chunkGrassKeys.Remove(chunkCoord);
            }

            if (VoxelTerrain.ChunkObjects.TryGetValue(chunkCoord, out var chunkObj))
            {
                Transform grassParent = chunkObj.transform.Find("Grass");
                if (grassParent != null) Destroy(grassParent.gameObject);
            }

            _instance._chunkGrassData.Remove(chunkCoord);
        }
    }
}
