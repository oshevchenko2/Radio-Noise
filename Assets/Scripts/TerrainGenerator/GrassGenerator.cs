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
        private const float _minGrassDistance = 0.5f;
        private const int _maxGrassPerChunk = 100;
        private const float _maxPositionDistanceFromChunk = 200f;

        private readonly Dictionary<Vector2Int, List<GrassInstance>> _chunkGrassData = new();
        private readonly Dictionary<Vector3Int, GrassInstance> _allGrassInstances = new();
        private readonly Dictionary<Vector2Int, List<Vector3Int>> _chunkGrassKeys = new();

        private static GrassGenerator _instance;

        void Awake() => _instance = this;

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
                RemoveGrassFromChunk(chunkCoord);

            Vector3[] vertices = topMesh.vertices;
            Vector3[] normals = topMesh.normals;

            List<GrassInstance> grassInstances = new();
            List<CombineInstance> combineInstances = new();
            GameObject grassParent = new GameObject("Grass");
            grassParent.transform.SetParent(parent);
            grassParent.transform.position = worldPosition;

            MeshFilter grassFilter = grassParent.AddComponent<MeshFilter>();
            MeshRenderer grassRenderer = grassParent.AddComponent<MeshRenderer>();
            grassRenderer.material = _instance._grassMaterial;

            Transform chunkTransform = null;
            if (VoxelTerrain.ChunkObjects.TryGetValue(chunkCoord, out var chunkObj))
                chunkTransform = chunkObj.transform;

            List<Vector3> placedPositions = new();
            int grassCount = 0;

            Quaternion parentRotation = parent != null ? parent.rotation : Quaternion.identity;
            Vector3 parentScale = parent != null ? parent.lossyScale : Vector3.one;

            List<int> shuffledIndices = Enumerable.Range(0, vertices.Length).OrderBy(x => Random.value).ToList();

            foreach (int i in shuffledIndices)
            {
                if (grassCount >= _maxGrassPerChunk) break;
                if (Random.value > _grassDensity) continue;

                Vector3 v = vertices[i];

                Vector3 candidateA = worldPosition + v;
                Vector3 candidateB = worldPosition + (parentRotation * Vector3.Scale(v, parentScale));
                Vector3 candidateC = candidateA;
                if (chunkTransform != null) candidateC = chunkTransform.TransformPoint(v);

                Vector3 chosenPos;
                float dA = Vector3.Distance(candidateA, worldPosition);
                float dB = Vector3.Distance(candidateB, worldPosition);
                float dC = Vector3.Distance(candidateC, worldPosition);

                if (dA <= _maxPositionDistanceFromChunk || dB <= _maxPositionDistanceFromChunk || dC <= _maxPositionDistanceFromChunk)
                {
                    float min = Mathf.Min(dA, dB, dC);
                    chosenPos = min == dA ? candidateA : (min == dB ? candidateB : candidateC);
                }
                else
                {
                    float min = Mathf.Min(dA, dB, dC);
                    chosenPos = min == dA ? candidateA : (min == dB ? candidateB : candidateC);
                }

                Vector3 normal;
                if (chunkTransform != null)
                    normal = chunkTransform.TransformDirection(normals[i]);
                else
                    normal = parentRotation * normals[i];
                normal.Normalize();

                float slopeAngle = Vector3.Angle(normal, Vector3.up);
                if (slopeAngle > _grassMaxSlopeAngle) continue;

                chosenPos.y += 0.05f;

                if (placedPositions.Any(p => Vector3.Distance(p, chosenPos) < _minGrassDistance)) continue;

                chosenPos.x += Random.Range(-0.3f, 0.3f);
                chosenPos.z += Random.Range(-0.3f, 0.3f);

                Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                float scale = Random.Range(0.8f, 1.2f);
                Vector3 scaleVec = new(scale, scale, scale);
                Matrix4x4 matrix = Matrix4x4.TRS(chosenPos, rotation, scaleVec);

                GrassInstance instance = new()
                {
                    Position = chosenPos,
                    Rotation = rotation,
                    Scale = scaleVec,
                    Matrix = matrix,
                    ChunkCoord = chunkCoord
                };

                Vector3Int positionKey = new(
                    Mathf.RoundToInt(chosenPos.x * 100),
                    Mathf.RoundToInt(chosenPos.y * 100),
                    Mathf.RoundToInt(chosenPos.z * 100)
                );

                int attempt = 0;
                while (_instance._allGrassInstances.ContainsKey(positionKey) && attempt < 5)
                {
                    positionKey.y += 1;
                    attempt++;
                }

                if (attempt >= 5) continue;

                grassInstances.Add(instance);
                _instance._allGrassInstances[positionKey] = instance;
                placedPositions.Add(chosenPos);

                if (!_instance._chunkGrassKeys.ContainsKey(chunkCoord))
                    _instance._chunkGrassKeys[chunkCoord] = new List<Vector3Int>();
                _instance._chunkGrassKeys[chunkCoord].Add(positionKey);

                combineInstances.Add(new CombineInstance
                {
                    mesh = _instance._grassMesh,
                    transform = matrix
                });

                grassCount++;
            }

            _instance._chunkGrassData[chunkCoord] = grassInstances;

            if (combineInstances.Count > 0)
            {
                Mesh combinedMesh = new();
                combinedMesh.CombineMeshes(combineInstances.ToArray(), true);
                grassFilter.mesh = combinedMesh;
                combinedMesh.RecalculateBounds();
            }
            else
            {
                Object.Destroy(grassParent);
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
                        chunkKeys.Remove(key);
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
                RebuildGrassMesh(chunkCoord);
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
                Object.Destroy(grassParent.gameObject);
                Despawn(grassParent.gameObject);
            }
        }

        static public void RemoveGrassFromChunk(Vector2Int chunkCoord)
        {
            if (!_instance._chunkGrassData.TryGetValue(chunkCoord, out var grassInstances)) return;

            foreach (var instance in grassInstances)
            {
                Vector3Int positionKey = new(
                    Mathf.RoundToInt(instance.Position.x * 100),
                    Mathf.RoundToInt(instance.Position.y * 100),
                    Mathf.RoundToInt(instance.Position.z * 100)
                );
                _instance._allGrassInstances.Remove(positionKey);
            }

            if (VoxelTerrain.ChunkObjects.TryGetValue(chunkCoord, out var chunkObj))
            {
                Transform grassParent = chunkObj.transform.Find("Grass");
                if (grassParent != null)
                    Object.Destroy(grassParent.gameObject);
            }

            _instance._chunkGrassData.Remove(chunkCoord);
            if (_instance._chunkGrassKeys.ContainsKey(chunkCoord))
                _instance._chunkGrassKeys.Remove(chunkCoord);
        }
    }
}
