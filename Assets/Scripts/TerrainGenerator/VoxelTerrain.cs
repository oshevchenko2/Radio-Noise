using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using UnityEngine.SearchService;
using System.Collections;
using Unity.Mathematics;
using Unity.Entities.UniversalDelegates;
using UnityEngine.SceneManagement;
using System;

namespace TerrainGenerator
{
    public class VoxelTerrain : MonoBehaviour
    {
        public int ChunkSize = 16;

        public int WorldSize = 256;

        private int ChunkHeight = 32;

        public float WorldVerticalOffset = 0f;

        public float TerrainScale = 10f;
        public float IsoLevel = 0f;

        public Material DesertMaterial;
        public Material PlainsMaterial;
        public Material ForestMaterial;
        public Material SwampMaterial;
        public Material MountainsMaterial;

        public Material StoneMaterial;

        private readonly Dictionary<Vector2Int, GameObject> _chunkObjects = new();
        private Dictionary<Vector2Int, BiomeType> _biomeMap = new();
        private float _seedX, _seedZ;

        private readonly float _topThickness = 2;
        private readonly float _caveThickness = 10f;
        public float GetSeedX() { return _seedX; }
        public float GetSeedZ() { return _seedZ; }
        public Dictionary<Vector2Int, BiomeType> GetBiomeMap() { return _biomeMap; }
        public Dictionary<Vector2Int, GameObject> GetChunks() { return _chunkObjects; }
        public void SetSeed(float x, float z) { _seedX = x; _seedZ = z; }
        public void LoadBiomeMap(Dictionary<Vector2Int, BiomeType> newMap) { _biomeMap = newMap; }

        private static readonly Queue<Mesh> _meshPool = new();
        private const int INITIAL_POOL_SIZE = 50;

        public IEnumerator DestroyAllChunks()
        {
            while (_chunkObjects.Count > 0)
            {
                var key = _chunkObjects.Keys.First();
                Destroy(_chunkObjects[key]);
                _chunkObjects.Remove(key);
            }
            
            yield return null;
        }

        public IEnumerator RecreateChunk(Vector2Int chunkCoord, ChunkData data)
        {
            GameObject chunkObject = new($"Chunk_{chunkCoord.x}_{chunkCoord.y}");
            chunkObject.transform.position = new Vector3(chunkCoord.x * ChunkSize, 0, chunkCoord.y * ChunkSize);
            _chunkObjects[chunkCoord] = chunkObject;

            float centerX = chunkCoord.x * ChunkSize + ChunkSize / 2f;
            float centerZ = chunkCoord.y * ChunkSize + ChunkSize / 2f;
            Dictionary<BiomeType, float> weights = SampleBiomeWeights(centerX, centerZ);
            var sortedWeights = weights.OrderByDescending(pair => pair.Value).ToList();
            BiomeType dominantBiome0 = sortedWeights[0].Key;
            BiomeType dominantBiome1 = sortedWeights.Count > 1 ? sortedWeights[1].Key : dominantBiome0;

            GameObject bottomObj = new("BottomLayer");
            bottomObj.transform.parent = chunkObject.transform;
            bottomObj.transform.localPosition = Vector3.zero;
            MeshRenderer bottomRend = bottomObj.AddComponent<MeshRenderer>();
            bottomRend.material = StoneMaterial;
            MeshFilter bottomMF = bottomObj.AddComponent<MeshFilter>();
            bottomMF.mesh = MeshDataToMesh(data.BottomMesh);

            GameObject caveObj = new("CaveLayer");
            caveObj.transform.parent = chunkObject.transform;
            caveObj.transform.localPosition = Vector3.zero;
            MeshRenderer caveRend = caveObj.AddComponent<MeshRenderer>();
            caveRend.material = StoneMaterial;
            MeshFilter caveMF = caveObj.AddComponent<MeshFilter>();
            bottomMF.mesh = MeshDataToMesh(data.CaveMesh);

            GameObject topObj = new("TopLayer");
            topObj.transform.parent = chunkObject.transform;
            topObj.transform.localPosition = Vector3.zero;
            MeshRenderer topRend = topObj.AddComponent<MeshRenderer>();
            MeshFilter topMF = topObj.AddComponent<MeshFilter>();
            
            if (dominantBiome0 == dominantBiome1)
            {
                topRend.material = GetMaterialForBiome(dominantBiome0);
                Mesh tmp = MeshDataToMesh(data.TopMesh);
                topMF.mesh = WeldCloseVertices(tmp, threshold: 0.01f);
            }
            else
            {
                Material mat0 = GetMaterialForBiome(dominantBiome0);
                Material mat1 = GetMaterialForBiome(dominantBiome1);
                topRend.materials = new Material[] { mat0, mat1 };
                topMF.mesh = MeshDataToMesh(data.TopMesh);
            }

            yield return null;
        }

        private Mesh MeshDataToMesh(MeshData meshData)
        {
            Mesh mesh = new()
            {
                vertices = meshData.Vertices.Select(sv => sv.ToVector3()).ToArray(),
                triangles = meshData.Triangles
            };
            mesh.RecalculateNormals();
            return mesh;
        }

        void Start()
        {
            InitializeMeshPool();

            _seedX = UnityEngine.Random.Range(-10000f, 10000f);
            _seedZ = UnityEngine.Random.Range(-10000f, 10000f);

            GenerateBiomeMap();
            GenerateWorld();
        }

        private void Update()
        {
            if(Input.GetKeyDown(KeyCode.F6))
            {
                SceneManager.LoadSceneAsync(0);       
            }
        }

        void InitializeMeshPool()
        {
            for (int i = 0; i < INITIAL_POOL_SIZE; i++)
            {
                _meshPool.Enqueue(new Mesh());
            }
        }

        [BurstCompile]
        struct BiomeGenerationJob : IJobParallelFor
        {
            public int numChunks;
            public float seedX, seedZ;
            public NativeArray<BiomeType> biomeMap;

            public void Execute(int index)
            {
                int x = index / numChunks;
                int z = index % numChunks;
                
                float elevation = noise.cnoise(new float2((x + seedX) * 0.05f, (z + seedZ) * 0.05f));
                float temperature = noise.cnoise(new float2((x + seedX) * 0.03f, (z + seedZ) * 0.03f));
                float humidity = noise.cnoise(new float2((x + seedX) * 0.03f, (z + seedZ) * 0.03f));
                
                Debug.Log("Chunk index: " + index);
                Debug.Log("Elevation: " + elevation);
                Debug.Log("Temperature: " + temperature);
                Debug.Log("Humidity: " + humidity);

                biomeMap[index] = DetermineBiome(elevation, temperature, humidity);
            }

            readonly BiomeType DetermineBiome(float elevation, float temperature, float humidity)
            {
                if (elevation > 0.8f)
                return BiomeType.Mountains;

                if (humidity < 0.3f && temperature > 0.6f)
                    return BiomeType.Desert;

                if (humidity > 0.7f && temperature > 0.5f)
                    return BiomeType.Swamp;

                if (humidity > 0.5f && temperature > 0.4f)
                    return BiomeType.Forest;

                return BiomeType.Plains;
            }
        }

        void GenerateBiomeMap()
        {
            int numChunks = WorldSize / ChunkSize;
            var biomeJob = new BiomeGenerationJob
            {
                numChunks = numChunks,
                seedX = _seedX,
                seedZ = _seedZ,
                biomeMap = new NativeArray<BiomeType>(numChunks * numChunks, Allocator.TempJob)
            };

            JobHandle handle = biomeJob.Schedule(numChunks * numChunks, 64);
            handle.Complete();

            for (int i = 0; i < biomeJob.biomeMap.Length; i++)
            {
                int x = i / numChunks;
                int z = i % numChunks;
                _biomeMap[new Vector2Int(x, z)] = biomeJob.biomeMap[i];
            }

            biomeJob.biomeMap.Dispose();
        }

        void GenerateWorld()
        {
            for (int x = 0; x < WorldSize; x += ChunkSize)
            {
                for (int z = 0; z < WorldSize; z += ChunkSize)
                {
                    Vector2Int chunkCoord = new(x / ChunkSize, z / ChunkSize);
                    GenerateChunk(chunkCoord);
                }
            }
        }

        void GenerateChunk(Vector2Int chunkCoord)
        {
            float centerX = chunkCoord.x * ChunkSize + ChunkSize / 2f;
            float centerZ = chunkCoord.y * ChunkSize + ChunkSize / 2f;
            Dictionary<BiomeType, float> weights = SampleBiomeWeights(centerX, centerZ);
            var sortedWeights = weights.OrderByDescending(pair => pair.Value).ToList();
            BiomeType dominantBiome0 = sortedWeights[0].Key;
            BiomeType dominantBiome1 = sortedWeights.Count > 1 ? sortedWeights[1].Key : dominantBiome0;
        
            GenerateDensityField(chunkCoord, out float[,,] bottomField, out float[,,] caveField, out float[,,] topField, ChunkHeight);

            GameObject chunkObject = new($"Chunk_{chunkCoord.x}_{chunkCoord.y}");
            chunkObject.transform.position = new Vector3(chunkCoord.x * ChunkSize, 0, chunkCoord.y * ChunkSize);
            _chunkObjects[chunkCoord] = chunkObject;

            GameObject bottomObj = new("BottomLayer");
            bottomObj.transform.parent = chunkObject.transform;
            bottomObj.transform.localPosition = Vector3.zero;
            MeshRenderer bottomRend = bottomObj.AddComponent<MeshRenderer>();
            bottomRend.material = StoneMaterial;
            MeshFilter bottomMF = bottomObj.AddComponent<MeshFilter>();
            bottomMF.mesh = GenerateMesh(bottomField);

            GameObject caveObj = new("CaveLayer");
            caveObj.transform.parent = chunkObject.transform;
            caveObj.transform.localPosition = Vector3.zero;
            MeshRenderer caveRend = caveObj.AddComponent<MeshRenderer>();
            caveRend.material = StoneMaterial;
            MeshFilter caveMF = caveObj.AddComponent<MeshFilter>();
            caveMF.mesh = GenerateMesh(caveField);

            GameObject topObj = new("TopLayer");
            topObj.transform.parent = chunkObject.transform;
            topObj.transform.localPosition = Vector3.zero;
            MeshRenderer topRend = topObj.AddComponent<MeshRenderer>();
            MeshFilter topMF = topObj.AddComponent<MeshFilter>();
            
            if (dominantBiome0 == dominantBiome1)
            {
                topRend.material = GetMaterialForBiome(dominantBiome0);
                topMF.mesh = GenerateConnectedTopMesh(topField);
            }
            else
            {
                Material mat0 = GetMaterialForBiome(dominantBiome0);
                Material mat1 = GetMaterialForBiome(dominantBiome1);
                topRend.materials = new Material[] { mat0, mat1 };
                topMF.mesh = GenerateMeshWithTwoMaterials(topField, new Vector3(chunkCoord.x * ChunkSize, 0, chunkCoord.y * ChunkSize), dominantBiome0, dominantBiome1);
            }
        }

        void GenerateDensityField(Vector2Int chunkCoord, 
                                    out float[,,] bottomField, 
                                    out float[,,] caveField, 
                                    out float[,,] topField,
                                    int chunkHeight)
        {
            bottomField = new float[ChunkSize + 1, ChunkHeight + 1, ChunkSize + 1];
            caveField = new float[ChunkSize + 1, ChunkHeight + 1, ChunkSize + 1];
            topField = new float[ChunkSize + 1, ChunkSize + 1, ChunkSize + 1];
            
            BiomeType biome00 = GetBiomeAt(chunkCoord.x, chunkCoord.y);
            BiomeType biome10 = GetBiomeAt(chunkCoord.x + 1, chunkCoord.y);
            BiomeType biome01 = GetBiomeAt(chunkCoord.x, chunkCoord.y + 1);
            BiomeType biome11 = GetBiomeAt(chunkCoord.x + 1, chunkCoord.y + 1);

            for (int x = 0; x <= ChunkSize; x++)
            {
                for (int y = 0; y <= chunkHeight; y++)
                {
                    for (int z = 0; z <= ChunkSize; z++)
                    {
                        float worldX = x + chunkCoord.x * ChunkSize;
                        float worldY = y + WorldVerticalOffset;
                        float worldZ = z + chunkCoord.y * ChunkSize;

                        float tX = (float)x / ChunkSize;
                        float tZ = (float)z / ChunkSize;

                        BiomeType blendedBiome = BlendBiomes(biome00, biome10, biome01, biome11, tX, tZ);
                        BiomeSettings settings = biomeSettings[blendedBiome];

                        float elevationNoise = Mathf.PerlinNoise((worldX + _seedX) * settings.noiseScale, (worldZ + _seedZ) * settings.noiseScale);
                        float baseHeight = elevationNoise * settings.heightMultiplier;
                        
                        float clampedBaseHeight = Mathf.Min(baseHeight, ChunkSize - 1);

                        float density = worldY - clampedBaseHeight;

                        float bottomThreshold = baseHeight - (_topThickness + _caveThickness);
                        float topThreshold = baseHeight - _topThickness;

                        if (worldY < bottomThreshold)
                            bottomField[x, y, z] = density;
                        else
                            bottomField[x, y, z] = 100f;

                        if (worldY >= bottomThreshold && worldY < topThreshold)
                        {
                            float caveNoise = Mathf.PerlinNoise(worldX * 0.05f, worldY * 0.05f) *
                                            Mathf.PerlinNoise(worldZ * 0.05f, worldY * 0.05f);
                            float caveMod = (caveNoise < 0.25f) ? -6f : 0f;
                            caveField[x, y, z] = density + caveMod;
                        }
                        else
                            caveField[x, y, z] = 100f;

                        if (worldY >= topThreshold && worldY <= baseHeight)
                        {
                            float modification = 0f;
                            var weights = SampleBiomeWeights(worldX, worldZ);

                            foreach (var kvp in weights)
                            {
                                BiomeType b = kvp.Key;
                                float w = kvp.Value;
                                float mod = 0f;

                                switch (b)
                                {
                                    case BiomeType.Plains:
                                        mod = Mathf.PerlinNoise(worldX * 0.2f, worldZ * 0.2f) * 0.2f;
                                        break;
                                    case BiomeType.Desert:
                                        mod = -Mathf.PerlinNoise(worldX * 0.15f, worldZ * 0.15f) * 4f;
                                        break;
                                    case BiomeType.Forest:
                                        mod = Mathf.PerlinNoise(worldX * 0.25f, worldZ * 0.25f) * 3f;
                                        break;
                                    case BiomeType.Swamp:
                                        mod = (worldY < baseHeight - 1f ? -2f : 0f)
                                            + Mathf.PerlinNoise(worldX * 0.1f, worldZ * 0.1f) * 1.5f;
                                        break;
                                    case BiomeType.Mountains:
                                        mod = Mathf.PerlinNoise(worldX * 0.4f, worldZ * 0.4f) * 6f;
                                        break;
                                }

                                modification += mod * w;
                            }

                            topField[x, y, z] = density + modification;
                        }
                        else
                            topField[x, y, z] = 100f;
                    }
                }
            }
        }

        private Mesh GetMeshFromPool()
        {
            if (_meshPool.Count > 0)
            {
                Mesh mesh = _meshPool.Dequeue();
                mesh.Clear();
                return mesh;
            }
            return new Mesh();
        }

        private void ReleaseMeshToPool(Mesh mesh)
        {
            mesh.Clear();
            _meshPool.Enqueue(mesh);
        }

        Mesh GenerateMesh(float[,,] densityField)
        {
            Mesh mesh = GetMeshFromPool();
            List<Vector3> vertices = new();
            List<int> triangles = new();

            int sizeX = densityField.GetLength(0);
            int sizeY = densityField.GetLength(1);
            int sizeZ = densityField.GetLength(2);

            for (int x = 0; x < sizeX - 1; x++)
            {
                for (int y = 0; y < sizeY - 1; y++)
                {
                    for (int z = 0; z < sizeZ - 1; z++)
                    {
                        MarchCube(new Vector3Int(x, y, z), densityField, vertices, triangles);
                    }
                }
            }

            mesh.subMeshCount = 1;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();

            return mesh;
        }

        void MarchCube(Vector3Int pos, float[,,] densityField, List<Vector3> vertices, List<int> triangles)
        {
            int cubeIndex = 0;
            float[] cubeCorners = new float[8];
            Vector3[] cornerPositions = { new(0, 0, 0), new(1, 0, 0), new(1, 0, 1), new(0, 0, 1),
                                          new(0, 1, 0), new(1, 1, 0), new(1, 1, 1), new(0, 1, 1) };

            for (int i = 0; i < 8; i++)
            {
                Vector3 worldPos = pos + cornerPositions[i];
                cubeCorners[i] = densityField[(int)worldPos.x, (int)worldPos.y, (int)worldPos.z];
                if (cubeCorners[i] < IsoLevel) cubeIndex |= 1 << i;
            }

            if (MarchingCubesTables.TriTable[cubeIndex, 0] == -1) return;

            Vector3[] edgeVertices = new Vector3[12];
            for (int i = 0; i < 12; i++)
            {
                if ((MarchingCubesTables.EdgeTable[cubeIndex] & (1 << i)) == 0)
                    continue;

                int a = MarchingCubesTables.EdgeConnections[i, 0];
                int b = MarchingCubesTables.EdgeConnections[i, 1];
                Vector3 pA = pos + cornerPositions[a];
                Vector3 pB = pos + cornerPositions[b];
                float vA = cubeCorners[a];
                float vB = cubeCorners[b];

                bool isVertical = Mathf.Approximately(pA.x, pB.x) && Mathf.Approximately(pA.z, pB.z);

                if (isVertical)
                    edgeVertices[i] = InterpolateTopVerts(pA, pB, vA, vB);
                else
                    edgeVertices[i] = InterpolateVerts(pA, pB, vA, vB);
            }

            for (int i = 0; MarchingCubesTables.TriTable[cubeIndex, i] != -1; i += 3)
            {
                int vi = vertices.Count;
                vertices.Add(edgeVertices[MarchingCubesTables.TriTable[cubeIndex, i]]);
                vertices.Add(edgeVertices[MarchingCubesTables.TriTable[cubeIndex, i + 1]]);
                vertices.Add(edgeVertices[MarchingCubesTables.TriTable[cubeIndex, i + 2]]);
                triangles.Add(vi);
                triangles.Add(vi + 1);
                triangles.Add(vi + 2);
            }
        }

        Vector3 InterpolateVerts(Vector3 p1, Vector3 p2, float v1, float v2)
        {
            const float eps = 1e-7f;
            if (Mathf.Abs(v1 - IsoLevel) < eps) return p1;
            if (Mathf.Abs(v2 - IsoLevel) < eps) return p2;
            if (Mathf.Abs(v2 - v1) < eps) return (p1 + p2) * 0.5f;
            
            float t = Mathf.Clamp01((IsoLevel - v1) / (v2 - v1));
            t = t * t * (3f - 2f*t);
            return Vector3.Lerp(p1, p2, t);
        }

        private Vector3 InterpolateTopVerts(Vector3 p1, Vector3 p2, float v1, float v2)
        {
            const float eps = 1e-7f;
            if (Mathf.Abs(v1 - IsoLevel) < eps) return p1;
            if (Mathf.Abs(v2 - IsoLevel) < eps) return p2;
            if (Mathf.Abs(v2 - v1) < eps) return (p1 + p2) * 0.5f;

            float t = (IsoLevel - v1) / (v2 - v1);
            t = Mathf.Clamp01(t);
            t = t * t * (3f - 2f * t);
            return Vector3.Lerp(p1, p2, t);
        }

        Material GetMaterialForBiome(BiomeType biome)
        {
            return biome switch
            {
                BiomeType.Desert => (Material)(DesertMaterial != null ? DesertMaterial : CreateMaterial(Color.yellow)),
                BiomeType.Plains => (Material)(PlainsMaterial != null ? PlainsMaterial : CreateMaterial(Color.green)),
                BiomeType.Forest => (Material)(ForestMaterial != null ? ForestMaterial : CreateMaterial(new Color(0.13f, 0.55f, 0.13f))),
                BiomeType.Swamp => (Material)(SwampMaterial != null ? SwampMaterial : CreateMaterial(new Color(0.3f, 0.5f, 0.3f))),
                BiomeType.Mountains => (Material)(MountainsMaterial != null ? MountainsMaterial : CreateMaterial(Color.gray)),
                _ => (Material)CreateMaterial(Color.white),
            };
        }

        Material CreateMaterial(Color color)
        {
            Material mat = new(Shader.Find("Standard"))
            {
                color = color
            };

            return mat;
        }

        BiomeType GetBiomeAt(int chunkX, int chunkZ)
        {
            Vector2Int coord = new(chunkX, chunkZ);
            if (_biomeMap.ContainsKey(coord)) return _biomeMap[coord];

            return BiomeType.Plains;
        }

        BiomeType BlendBiomes(BiomeType b00, BiomeType b10, BiomeType b01, BiomeType b11, float tx, float tz)
        {
            Dictionary<BiomeType, float> weights = new();

            void AddWeight(BiomeType biome, float w)
            {
                if (!weights.ContainsKey(biome)) weights[biome] = 0f;
                weights[biome] += w;
            }

            AddWeight(b00, (1 - tx) * (1 - tz));
            AddWeight(b10, tx * (1 - tz));
            AddWeight(b01, (1 - tx) * tz);
            AddWeight(b11, tx * tz);

            return weights.OrderByDescending(pair => pair.Value).First().Key;
        }

        public Dictionary<BiomeType, BiomeSettings> biomeSettings = new()
        {
            { BiomeType.Desert, new BiomeSettings { noiseScale = 0.05f, heightMultiplier = 13f, chunkHeight = 64 } },
            { BiomeType.Plains, new BiomeSettings { noiseScale = 0.01f, heightMultiplier = 15f, chunkHeight = 128 } },
            { BiomeType.Forest, new BiomeSettings { noiseScale = 0.04f, heightMultiplier = 17f, chunkHeight = 96 } },
            { BiomeType.Swamp, new BiomeSettings { noiseScale = 0.03f, heightMultiplier = 12f, chunkHeight = 80 } },
            { BiomeType.Mountains, new BiomeSettings { noiseScale = 0.05f, heightMultiplier = 40f, chunkHeight = 256 } }
        };

        Dictionary<BiomeType, float> SampleBiomeWeights(float worldX, float worldZ)
        {
            Vector2 samplePos = new(worldX / ChunkSize, worldZ / ChunkSize);
            Vector2Int baseChunk = new(Mathf.FloorToInt(samplePos.x), Mathf.FloorToInt(samplePos.y));

            Dictionary<BiomeType, float> weights = new();
            float totalWeight = 0f;

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    Vector2Int neighborChunk = baseChunk + new Vector2Int(dx, dz);
                    if (!_biomeMap.ContainsKey(neighborChunk)) continue;

                    Vector2 center = new(neighborChunk.x + 0.5f, neighborChunk.y + 0.5f);
                    float dist = Vector2.Distance(samplePos, center);

                    float weight = Mathf.Clamp01(1f - dist);
                    BiomeType biome = _biomeMap[neighborChunk];

                    if (weights.ContainsKey(biome))
                        weights[biome] += weight;
                    else
                        weights[biome] = weight;

                    totalWeight += weight;
                }
            }

            foreach (var key in weights.Keys.ToList())
            {
                weights[key] /= totalWeight;
            }

            return weights;
        }
        
        Mesh GenerateMeshWithTwoMaterials(float[,,] densityField, Vector3 chunkOrigin, BiomeType dominantBiome0, BiomeType dominantBiome1)
        {
            Mesh mesh = GetMeshFromPool();

            List<Vector3> vertices = new();

            List<int> trianglesSubmesh0 = new();
            List<int> trianglesSubmesh1 = new();

            for(int x = 0; x < ChunkSize; x++)
            {
                for (int y = 0; y < ChunkSize; y++)
                {
                    for (int z = 0; z < ChunkSize; z++)
                    {
                        MarchCubeMulti(new Vector3Int(x, y, z), densityField, vertices, trianglesSubmesh0, trianglesSubmesh1, chunkOrigin, dominantBiome0, dominantBiome1);
                    }
                }
            }

            mesh.subMeshCount = 2;

            mesh.SetVertices(vertices);
            mesh.SetTriangles(trianglesSubmesh0, 0);
            mesh.SetTriangles(trianglesSubmesh1, 1);
            mesh.RecalculateNormals();

            return mesh;
        }

        void MarchCubeMulti(Vector3Int pos, float[,,] densityField, List<Vector3> vertices, List<int> tris0, List<int> tris1, Vector3 chunkOrigin, BiomeType dominantBiome0, BiomeType dominantBiome1)
        {
            int cubeIndex = 0;
            float[] cubeCorners = new float[8];
            
            Vector3[] cornerPositions = new Vector3[]
            {
                new(0,0,0), new(1,0,0), new(1,0,1), new(0,0,1),
                new(0,1,0), new(1,1,0), new(1,1,1), new(0,1,1)
            };

            for (int i = 0; i < 8; i++)
            {
                Vector3 localPos = pos + cornerPositions[i];
                cubeCorners[i] = densityField[(int)localPos.x, (int)localPos.y, (int)localPos.z];
                if (cubeCorners[i] < IsoLevel)
                    cubeIndex |= 1 << i;
            }

            if (MarchingCubesTables.TriTable[cubeIndex, 0] == -1)
                return;

            Vector3[] edgeVertices = new Vector3[12];
            for (int i = 0; i < 12; i++)
            {
                if ((MarchingCubesTables.EdgeTable[cubeIndex] & (1 << i)) != 0)
                {
                    int a = MarchingCubesTables.EdgeConnections[i, 0];
                    int b = MarchingCubesTables.EdgeConnections[i, 1];
                    
                    edgeVertices[i] = InterpolateVerts(pos + cornerPositions[a], pos + cornerPositions[b], cubeCorners[a], cubeCorners[b]);
                }
            }

            for (int i = 0; MarchingCubesTables.TriTable[cubeIndex, i] != -1; i += 3)
            {
                int vertexIndex = vertices.Count;
                
                Vector3 v0 = edgeVertices[MarchingCubesTables.TriTable[cubeIndex, i]];
                Vector3 v1 = edgeVertices[MarchingCubesTables.TriTable[cubeIndex, i + 1]];
                Vector3 v2 = edgeVertices[MarchingCubesTables.TriTable[cubeIndex, i + 2]];

                Vector3 worldV0 = chunkOrigin + v0;
                Vector3 worldV1 = chunkOrigin + v1;
                Vector3 worldV2 = chunkOrigin + v2;
                Vector3 centroid = (worldV0 + worldV1 + worldV2) / 3f;

                Dictionary<BiomeType, float> triWeights = SampleBiomeWeights(centroid.x, centroid.z);
                
                float w0 = triWeights.ContainsKey(dominantBiome0) ? triWeights[dominantBiome0] : 0f;
                float w1 = triWeights.ContainsKey(dominantBiome1) ? triWeights[dominantBiome1] : 0f;
                bool assignToFirst = w0 >= w1;

                vertices.Add(v0);
                vertices.Add(v1);
                vertices.Add(v2);

                if (assignToFirst)
                {
                    tris0.Add(vertexIndex);
                    tris0.Add(vertexIndex + 1);
                    tris0.Add(vertexIndex + 2);
                }
                else
                {
                    tris1.Add(vertexIndex);
                    tris1.Add(vertexIndex + 1);
                    tris1.Add(vertexIndex + 2);
                }
            }
        }
        private Mesh WeldCloseVertices(Mesh mesh, float threshold = 1f)
        {
            Vector3[] oldVerts = mesh.vertices;
            int[] oldTris = mesh.triangles;

            List<Vector3> newVerts = new List<Vector3>();
            int[] map = new int[oldVerts.Length];

            for (int i = 0; i < oldVerts.Length; i++)
            {
                Vector3 v = oldVerts[i];
                bool found = false;
                for (int j = 0; j < newVerts.Count; j++)
                {
                    if (Vector3.Distance(newVerts[j], v) <= threshold)
                    {
                        map[i] = j;
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    map[i] = newVerts.Count;
                    newVerts.Add(v);
                }
            }

            int[] newTris = new int[oldTris.Length];
            for (int i = 0; i < oldTris.Length; i++)
            {
                newTris[i] = map[oldTris[i]];
            }

            Mesh newMesh = new()
            {
                vertices = newVerts.ToArray(),
                triangles = newTris
            };
            newMesh.RecalculateNormals();
            return newMesh;
        }
        private Mesh GenerateConnectedTopMesh(float[,,] topField)
        {
            Mesh raw = GenerateMesh(topField);
            return WeldCloseVertices(raw, threshold: 0.01f);
        }
    }
}