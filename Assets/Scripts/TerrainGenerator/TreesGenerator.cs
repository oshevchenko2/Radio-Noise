using System.Collections.Generic;
using System.Linq;
using FishNet.Object;
using UnityEngine;

namespace TerrainGenerator
{
    public class TreesGenerator : NetworkBehaviour
    {
        [SerializeField] private TreePrefabData[] _treePrefabs;
        private const float _treeDensity = 0.01f;
        private const float _treeMaxSlopeAngle = 90;
        private const float _minTreeDistance = 8;

        private const int _maxTreesPlains = 30;
        private const int _maxTreesForest = 50;
        private const int _maxTreesSwamp = 10;

        private readonly Dictionary<Vector2Int, List<GameObject>> _chunkTreeInstances = new();

        private static TreesGenerator _instance;

        void Awake() => _instance = this;

        static public void GenerateTreesForChunk(
            Vector2Int chunkCoord,
            Mesh topMesh,
            BiomeType dominantBiome0,
            BiomeType dominantBiome1,
            Transform parent,
            Vector3 worldPosition)
        {
            Debug.Log($"Generating trees for chunk {chunkCoord}");

            if (!_instance.IsTreeBiome(dominantBiome0) && !_instance.IsTreeBiome(dominantBiome1))
            {
                Debug.Log($"Skipped - not tree biome: {dominantBiome0}/{dominantBiome1}");
                return;
            }
            if (_instance._treePrefabs == null || _instance._treePrefabs.Length == 0)
            {
                Debug.LogError("Tree prefabs not assigned!");
                return;
            }
            if (_instance._chunkTreeInstances.ContainsKey(chunkCoord))
            {
                _instance.RemoveTreesFromChunk(chunkCoord);
                GrassGenerator.RemoveGrassFromChunk(chunkCoord);
            }

            Vector3[] vertices = topMesh.vertices;
            Vector3[] normals = topMesh.normals;

            GameObject treesParent = new("Trees");
            treesParent.transform.SetParent(parent);
            treesParent.transform.localPosition = Vector3.zero;

            int maxTrees = _instance.GetMaxTreesForBiome(dominantBiome0, dominantBiome1);

            List<Vector3> placedPositions = new();

            int treeCount = 0;

            List<int> shuffledIndices = Enumerable.Range(0, vertices.Length).ToList();
            shuffledIndices = shuffledIndices.OrderBy(x => Random.value).ToList();

            foreach (int index in shuffledIndices)
            {
                if (treeCount >= maxTrees) break;

                Vector3 normal = normals[index];
                float slopeAngle = Vector3.Angle(normal, Vector3.up);
                if (slopeAngle > _treeMaxSlopeAngle) continue;

                if (Random.value > _treeDensity) continue;

                Vector3 position = worldPosition + vertices[index];
                position.y += 0f;

                if (placedPositions.Any(p => Vector3.Distance(p, position) < _minTreeDistance))
                    continue;

                TreePrefabData treeData = _instance.GetRandomTreeType();
                GameObject treePrefab = treeData.TreePrefab;
                if (treePrefab == null) continue;

                GameObject treeInstance = Instantiate(
                    treePrefab,
                    position,
                    Quaternion.Euler(0, Random.Range(0, 360f), 0),
                    treesParent.transform
                );

                float scale = Random.Range(0.8f, 1.2f);
                treeInstance.transform.localScale = Vector3.one * scale;

                placedPositions.Add(position);
                treeCount++;
            }

            _instance._chunkTreeInstances[chunkCoord] = treesParent.transform
                .Cast<Transform>()
                .Select(t => t.gameObject)
                .ToList();

            Debug.Log($"Spawned {treeCount} trees in chunk {chunkCoord}");
        }

        private TreePrefabData GetRandomTreeType()
        {
            float totalProbability = _treePrefabs.Sum(t => t.SpawnProbability);
            float randomPoint = UnityEngine.Random.value * totalProbability;

            float currentProbability = 0f;
            foreach (var type in _treePrefabs)
            {
                currentProbability += type.SpawnProbability;
                if (randomPoint <= currentProbability)
                    return type;
            }

            return _treePrefabs[0];
        }

        private int GetMaxTreesForBiome(BiomeType biome1, BiomeType biome2)
        {
            if (biome1 == BiomeType.Forest || biome2 == BiomeType.Forest)
                return _maxTreesForest;
            
            if (biome1 == BiomeType.Swamp || biome2 == BiomeType.Swamp)
                return _maxTreesSwamp;
            
            return _maxTreesPlains;
        }

        private void RemoveTreesFromChunk(Vector2Int chunkCoord)
        {
            if (_chunkTreeInstances.TryGetValue(chunkCoord, out var trees))
            {
                foreach (GameObject tree in trees)
                {
                    if (tree != null) Destroy(tree);
                }
                _chunkTreeInstances.Remove(chunkCoord);
            }
        }

        private bool IsTreeBiome(BiomeType biome)
        {
            return biome == BiomeType.Forest || 
                   biome == BiomeType.Plains ||
                   biome == BiomeType.Swamp;
        }
    }
}