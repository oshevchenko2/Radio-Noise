// This project & code is licensed under the MIT License. See the ./LICENSE file for details.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using System.Collections;
using Unity.Mathematics;
using System.Collections.Concurrent;
using System.Threading;
using FishNet.Object;

namespace TerrainGenerator
{
    public class VoxelTerrain : NetworkBehaviour
    {
        #region Variables
        [SerializeField] private int _chunkSize = 16;
        // Size of one chunk(in meters).
        // Set only in geometric progression with denominator 2 starting with 8.
        // Otherwise - memory leak or unity crush.

        public int WorldSize = 256;
        // World size(in meters).
        // I advise to set it as a multiple of ChunkSize, otherwise there will be joints along the radius where there will only be the bot layer.
        // If the value is negative or less, than ChunkSize - world will simply not be generated and will return an ArgumentOutOfRangeExeption error.

        private readonly int _chunkHeight = 32;
        // I do not recommend changing it.
        // Coz if u set it lower, there will be cuts in height, which are especially visible in the mountains
        // And if u set it higher, there the world will againg be cut off, but already both in lower and higher heights.
        // I think it's unity bug.

        private readonly int _worldVerticalOffset = 0;
        // Set the world higher by a central meter without changing ChunkHeight.
        // I do not recommend changing it, only 4 debuging
        // Coz read comments in "ChunkHeight" var

        [SerializeField] private int _isoLevel = 8;
        // It may look like WorldVerticalOffset, but it's not.
        // IsoLevel sets the threshold density at which the Marching Cubes algorithm decides where the surface passes:
        // points with density below this threshold are considered “inside” the object,
        // those above are considered “outside”, or vice versa depending on the convention.
        // Read https://wikipedia.org/wiki/Marching_cubes

        [SerializeField] private Material _desertMaterial;
        [SerializeField] private Material _plainsMaterial;
        [SerializeField] private Material _forestMaterial;
        [SerializeField] private Material _swampMaterial;
        [SerializeField] private Material _mountainsMaterial;

        // Materials for Biomes
        // ToDo: Make them some textures, coz the ones taken from the internet look like shit
        // ToDo: MOOOOOOOOORE BIOMES! MOOOOOOOOORE MATERIALS! MOOOOOOOOORE SHIT! MOOOOOOOOORE TEXTURES FROM INTERNET!

        [SerializeField] private Material _stoneMaterial;
        [SerializeField] private Material _waterMaterial;

        // Materials 4 minerals & resources
        // ToDo: Add more ores and rocks
        // ToDo: Add more realistic water
        // ToDo: Fix water, coz it appears too low and the height only changes in materials from shader

        [SerializeField] private Mesh _grassMesh;
        [SerializeField] private Material _grassMaterial;

        private const float _grassDensity = 0.1f;
        private const float _grassMaxSlopeAngle = 90f;
        private const int _maxGrassPerChunk = 100;

        private readonly Dictionary<Vector2Int, List<GrassInstance>> _chunkGrassData = new();
        private readonly Dictionary<Vector3Int, GrassInstance> _allGrassInstances = new();

        // All grass preferences

        private Shader _shader;

        //Shader 2 Find, if materials above missing

        private readonly Dictionary<Vector2Int, GameObject> _chunkObjects = new();

        // Dictionary 4 all chunkObj on & 4 scene

        private Dictionary<Vector2Int, BiomeType> _biomeMap = new();

        // Dictionary 4 storage biome map

        private float _seedX, _seedZ;

        // Can I not write comments 4 such obvious things?

        private readonly float _topThickness = 2;
        private readonly float _caveThickness = 20;
        private readonly float _stoneThickness = 2;

        // Layers thicknesses.
        // Do not put less than 2 for(Yeah, here, to avoid confusion, I don't put 4 as for and 2 as to) Top 
        // and less than 10 for caves.
        // and less than 2 for top stones;

        public float GetSeedX() { return _seedX; }
        public float GetSeedZ() { return _seedZ; }

        public Dictionary<Vector2Int, BiomeType> GetBiomeMap() { return _biomeMap; }
        public Dictionary<Vector2Int, GameObject> GetChunks() { return _chunkObjects; }

        public void SetSeed(float x, float z) { _seedX = x; _seedZ = z; }

        public void LoadBiomeMap(Dictionary<Vector2Int, BiomeType> newMap) { _biomeMap = newMap; }

        // From GetSeedX float to LoadBiomeMap void
        // Created and converted as methods & variables 4 WorldSaveSystem.cs

        private static readonly Queue<Mesh> _meshPool = new();
        // Queue<Mesh> that implements a pool of meshes 4 reuse.
        private const int INITIAL_POOL_SIZE = 100;

        private Dictionary<Vector2Int, Dictionary<Vector2Int, GameObject>> _chunkWalls = new();

        private readonly ConcurrentQueue<ChunkGenResult> _readyChunks = new();
        private readonly HashSet<Vector2Int> _generatingChunks = new();

        private readonly SemaphoreSlim _chunkGenSemaphore = new(8);

        private const int MaxChunksPerFrame = 3;

        private readonly Dictionary<Vector2Int, InstancedChunkData> _instancedChunks = new();
        private readonly Dictionary<Vector2Int, ChunkData> _modifiedChunks = new();

        private float _instanceDistance = 128f;

        #endregion

        // MeshPool as protection + optimization from unnecessary use of meshes

        #region Save-Load Sys
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

        // Used 4 WorldSaveSystem.cs
        // Or debugging

        public IEnumerator RecreateChunk(Vector2Int chunkCoord, ChunkData data)
        {
            // This method recreates a chuck obj in the scene using given chunk coords and stored data
            // Read void GenerateChunk() for the logic behind generating chunks

            GameObject chunkObject = new($"Chunk_{chunkCoord.x}_{chunkCoord.y}");
            // A GameObject of a chunk with a readable name is created
            chunkObject.transform.position = new Vector3(chunkCoord.x * _chunkSize, 0, chunkCoord.y * _chunkSize);
            // Puts it in the world on a grid: chunk coordinates x chunk size.
            _chunkObjects[chunkCoord] = chunkObject;
            // Saves the reference to a dictionary so that I can quickly find and overwrite this chunk later on

            float centerX = chunkCoord.x * _chunkSize + _chunkSize / 2f;
            float centerZ = chunkCoord.y * _chunkSize + _chunkSize / 2f;
            // World coordinates of the center of the chunk, needed 2 estimate which biomes “predominate” in the middle of the block.

            Dictionary<BiomeType, float> weights = SampleBiomeWeights(centerX, centerZ);
            var sortedWeights = weights.OrderByDescending(pair => pair.Value).ToList();
            // Sort by descending weight and select the two largest values:
            BiomeType dominantBiome0 = sortedWeights[0].Key; // the main biome
            BiomeType dominantBiome1 = sortedWeights.Count > 1 ? sortedWeights[1].Key : dominantBiome0; // the second “strongest” (or the same if only one)

            int layerId = LayerMask.NameToLayer("Chunk");

            // If creating new layer, use:
            // GameObject layerNameObj = new("LayerName"); // Making container for layerNameObj with readable name
            // layerNameObj.transform.parent = chunkObject.transform; // Bind 2 the root of the chunk
            // layerNameObj.transform.localPosition = Vector3.zero; // and zero-shift it inside
            // MeshRenderer layerNameRend = layerNameObj.AddComponent<MeshRenderer>();
            // layerNameRend.material = layerNameMaterial;
            // MeshFilter layerNameMF = layerNameObj.AddComponent<MeshFilter>();
            // MeshCollider layerNameMC = layerNameObj.AddComponent<MeshCollider>();
            // layerNameMF.mesh = MeshDataToMesh(data.layerNameMesh); // Onverting data saved in ChunkData.cs 2 Mesh
            // !!! DON'T FORGET TO PUT THIS IN CREATECHUNK METHOD

            GameObject bottomObj = new("BottomLayer");
            bottomObj.transform.parent = chunkObject.transform;
            bottomObj.transform.localPosition = Vector3.zero;
            MeshRenderer bottomRend = bottomObj.AddComponent<MeshRenderer>();
            bottomRend.material = _stoneMaterial;
            MeshFilter bottomMF = bottomObj.AddComponent<MeshFilter>();
            bottomMF.mesh = MeshDataToMesh(data.BottomMesh);
            MeshCollider bottomMC = bottomObj.AddComponent<MeshCollider>();
            // Recreating bot layer

            GameObject caveObj = new("CaveLayer");
            caveObj.transform.parent = chunkObject.transform;
            caveObj.transform.localPosition = Vector3.zero;
            MeshRenderer caveRend = caveObj.AddComponent<MeshRenderer>();
            caveRend.material = _stoneMaterial;
            MeshFilter caveMF = caveObj.AddComponent<MeshFilter>();
            caveMF.mesh = MeshDataToMesh(data.CaveMesh);
            MeshCollider caveMC = caveObj.AddComponent<MeshCollider>();
            caveObj.layer = layerId;
            // Recreating caves

            GameObject stoneObj = new("StoneLayer");
            stoneObj.transform.parent = chunkObject.transform;
            stoneObj.transform.localPosition = Vector3.zero;
            var stoneRend = stoneObj.AddComponent<MeshRenderer>();
            stoneRend.material = _stoneMaterial;
            var stoneMF = stoneObj.AddComponent<MeshFilter>();
            stoneMF.mesh = MeshDataToMesh(data.StoneMesh);
            var stoneMC = stoneObj.AddComponent<MeshCollider>();
            stoneObj.layer = layerId;
            // Creating Top Stone

            GameObject topObj = new("TopLayer");
            topObj.transform.parent = chunkObject.transform;
            topObj.transform.localPosition = Vector3.zero;
            MeshRenderer topRend = topObj.AddComponent<MeshRenderer>();
            MeshFilter topMF = topObj.AddComponent<MeshFilter>();

            // Top layer is little bit different
            // But..
            // If the biome is single, we assign one material and weld close vertices 2 remove artifacts
            if (dominantBiome0 == dominantBiome1)
            {
                topRend.material = GetMaterialForBiome(dominantBiome0);
                Mesh tmp = MeshDataToMesh(data.TopMesh);
                topMF.mesh = WeldCloseVertices(tmp, threshold: 0.01f);
            }
            else
            // If the biome is mixed, we assign two materials (two-subset mesh) and use “raw” data.
            {
                Material mat0 = GetMaterialForBiome(dominantBiome0);
                Material mat1 = GetMaterialForBiome(dominantBiome1);
                topRend.materials = new Material[] { mat0, mat1 };
                topMF.mesh = MeshDataToMesh(data.TopMesh);
            }
            // Recreating top

            topObj.layer = layerId;
            if (!topObj.TryGetComponent<MeshCollider>(out var topMC))
            {
                topMC = topObj.AddComponent<MeshCollider>();
            }
            topMC.sharedMesh = topMF.mesh;

            GenerateGrassForChunk(
                chunkCoord: chunkCoord,
                topMesh: topMF.mesh,
                dominantBiome0: dominantBiome0,
                dominantBiome1: dominantBiome1,
                parent: topObj.transform,
                worldPosition: chunkObject.transform.position
            );

            yield return null;
        }

        private Mesh MeshDataToMesh(MeshData meshData)
        // Take a MeshData structure and returns mesh
        {
            Mesh mesh = new()
            {
                vertices = meshData.Vertices.Select(sv => sv.ToVector3()).ToArray(),
                triangles = meshData.Triangles
            };
            mesh.RecalculateNormals();
            return mesh;
        }

        #endregion

        [ObserversRpc(BufferLast = true)]
        private void RpcSetSeeds(float seedX, float seedZ)
        {
            _seedX = seedX;
            _seedZ = seedZ;

            GenerateBiomeMap();

            if (IsClientOnlyInitialized)
            {
                StartCoroutine(DynamicChunkGeneration());
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            if (!IsServerInitialized) return;

            _shader = Shader.Find("HDRenderPipeline/Lit");

            InitializeMeshPool();

            _seedX = UnityEngine.Random.Range(-10000000f, 10000000f);
            _seedZ = UnityEngine.Random.Range(-10000000f, 10000000f);

            Debug.Log($"Seed: {_seedX}{_seedZ}");

            GenerateBiomeMap();

            RpcSetSeeds(_seedX, _seedZ);

            StartCoroutine(GenerateWorld());
            StartCoroutine(DynamicChunkGeneration());
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsClientInitialized)
            {
                if (!IsServerInitialized)
                {
                    GenerateBiomeMap();
                }
                
                StartCoroutine(DynamicChunkGeneration());
            }
        }

        IEnumerator DynamicChunkGeneration()
        {
            float chunkGenRadius = 96f;
            
            int numChunks = WorldSize / _chunkSize;
            
            while (true)
            {
                var playerPos = Camera.main != null ? Camera.main.transform.position : transform.position;
            
                Vector2 playerXZ = new(playerPos.x, playerPos.z);
            
                for (int x = 0; x < numChunks; x++)
                {
                    for (int z = 0; z < numChunks; z++)
                    {
                        Vector2Int coord = new(x, z);
            
                        if (_chunkObjects.ContainsKey(coord) || _generatingChunks.Contains(coord)) continue;
            
                        Vector3 chunkWorldPos = new(x * _chunkSize + _chunkSize / 2f, 0, z * _chunkSize + _chunkSize / 2f);
            
                        float dist = Vector2.Distance(new Vector2(chunkWorldPos.x, chunkWorldPos.z), playerXZ);
            
                        if (dist > chunkGenRadius) continue;
            
                        _generatingChunks.Add(coord);
            
                        _chunkGenSemaphore.Wait();
            
                        new Thread(() =>
                        {
                            try
                            {
                                float centerX = coord.x * _chunkSize + _chunkSize / 2f;
                                float centerZ = coord.y * _chunkSize + _chunkSize / 2f;

                                var weights = SampleBiomeWeights(centerX, centerZ);
                                var sortedWeights = weights.OrderByDescending(pair => pair.Value).ToList();

                                BiomeType dominantBiome0 = sortedWeights[0].Key;
                                BiomeType dominantBiome1 = sortedWeights.Count > 1 ? sortedWeights[1].Key : dominantBiome0;

                                GenerateDensityField(coord,
                                    out float[,,] bottomField,
                                    out float[,,] caveField,
                                    out float[,,] stoneField,
                                    out float[,,] topField,
                                    _chunkHeight);

                                _readyChunks.Enqueue(new ChunkGenResult
                                {
                                    coord = coord,
                                
                                    bottomField = bottomField,
                                    caveField = caveField,
                                    stoneField = stoneField,
                                    topField = topField,
                                
                                    dominantBiome0 = dominantBiome0,
                                    dominantBiome1 = dominantBiome1
                                });
                            }
                            finally
                            {
                                _chunkGenSemaphore.Release();
                            }
                        }).Start();
                    }
                }

                yield return new WaitForSecondsRealtime(0.5f);
            }
        }

        void InitializeMeshPool()
        {
            for (int i = 0; i < INITIAL_POOL_SIZE; i++)
            {
                _meshPool.Enqueue(new Mesh());
                // Creates a new empty Mesh object, ready 2 be filled with vertices and triangles
                // and adds the mesh 2 the end of the queue, providing O(1)-operation and predictable memory control.
            }
        }

        void GenerateBiomeMap()
        {
            int numChunks = WorldSize / _chunkSize;
            // Here we count how many chunks along the X (and Z) axis will fit in the world.
            // If, say, WorldSize = 256 and ChunkSize = 16, we get numChunks = 16
            var biomeJob = new BiomeGenerationJob
            {
                numChunks = numChunks,

                seedX = _seedX,
                seedZ = _seedZ,

                biomeMap = new NativeArray<BiomeType>(numChunks * numChunks, Allocator.TempJob)
                // I choose TempJob coz the array is only needed 4 the duration of the job and must be manually freed after the job is executed
            };

            JobHandle handle = biomeJob.Schedule(numChunks * numChunks, 64);
            // Splits numChunks * numChunks iterations into batches of 64, distributing work among CPU cores
            handle.Complete();
            // Usually it's better to call Complete later, but here I need 2 get the results right away

            for (int i = 0; i < biomeJob.biomeMap.Length; i++)
            {
                int x = i / numChunks;
                int z = i % numChunks;
            
                _biomeMap[new Vector2Int(x, z)] = biomeJob.biomeMap[i];
                // Index "i" is partitioned 2d chunk coordinates
            }

            biomeJob.biomeMap.Dispose();
            // Be sure to call after Complete() 2 avoid leaks (especially with Allocator.TempJob)
        }

        [BurstCompile]
        struct BiomeGenerationJob : IJobParallelFor
        {
            public int numChunks;
            public float seedX, seedZ;
            public NativeArray<BiomeType> biomeMap;
            // Output array where the calculated biome for each index is written.
            public void Execute(int index)
            {
                int x = index / numChunks;
                int z = index % numChunks;

                float elevation = noise.cnoise(new float2((x + seedX) * 0.05f, (z + seedZ) * 0.05f));
                float temperature = noise.cnoise(new float2((x + seedX) * 0.02f, (z + seedZ) * 0.03f));
                float humidity = noise.cnoise(new float2((x + seedX) * 0.03f, (z + seedZ) * 0.03f));

                /*Debug.Log("Chunk index: " + index);
                Debug.Log("Elevation: " + elevation);
                Debug.Log("Temperature: " + temperature);
                Debug.Log("Humidity: " + humidity);
                */
                biomeMap[index] = DetermineBiome(elevation, temperature, humidity);
                // According to the combination of the three noise values, we select one of the BiomeType and store it in the output array.
            }

            readonly BiomeType DetermineBiome(float elevation, float temperature, float humidity)
            // Settings for biome generation rates
            {
                float normElev = (elevation + 1f) * 0.5f;
                float normTemp = (temperature + 1f) * 0.5f;
                float normHum = (humidity + 1f) * 0.5f;

                if (elevation < -0.3f && normHum > 0.7f)
                    return BiomeType.Ocean;

                if (normElev > 0.85f || elevation > 0.6f)
                    return BiomeType.Mountains;

                if (normTemp > 0.65f && normHum < 0.35f)
                    return BiomeType.Desert;

                if (normHum > 0.75f && temperature > -0.2f)
                    return BiomeType.Swamp;

                if (normHum > 0.55f && normTemp > 0.4f)
                    return BiomeType.Forest;

                if (temperature < -0.3f && elevation > 0.2f)
                    return BiomeType.Mountains;

                if (normHum > 0.4f && normTemp > 0.25f)
                    return BiomeType.Plains;

                return (normElev > 0.5f) ? BiomeType.Plains : BiomeType.Ocean;
            }
        }

        IEnumerator GenerateWorld()
        {
            float chunkGenRadius = 96f;

            var chunkCoords = new List<Vector2Int>();

            for (int x = 0; x < WorldSize; x += _chunkSize)
                for (int z = 0; z < WorldSize; z += _chunkSize)
                    chunkCoords.Add(new Vector2Int(x / _chunkSize, z / _chunkSize));

            var playerPos = Camera.main != null ? Camera.main.transform.position : transform.position;

            chunkCoords.Sort((a, b) =>
            {
                Vector2 pa = new(a.x * _chunkSize, a.y * _chunkSize);
                Vector2 pb = new(b.x * _chunkSize, b.y * _chunkSize);
            
                float da = Vector2.SqrMagnitude(pa - new Vector2(playerPos.x, playerPos.z));
                float db = Vector2.SqrMagnitude(pb - new Vector2(playerPos.x, playerPos.z));
            
                return da.CompareTo(db);
            });

            int countThisFrame = 0;

            foreach (var coord in chunkCoords)
            {
                Vector3 chunkWorldPos = new(coord.x * _chunkSize + _chunkSize / 2f, 0, coord.y * _chunkSize + _chunkSize / 2f);

                float dist = Vector3.Distance(chunkWorldPos, Camera.main != null ? Camera.main.transform.position : transform.position);

                if (dist > chunkGenRadius) continue;
                if (_generatingChunks.Contains(coord)) continue;

                _generatingChunks.Add(coord);
                _chunkGenSemaphore.Wait();

                new Thread(() =>
                {
                    try
                    {
                        float centerX = coord.x * _chunkSize + _chunkSize / 2f;
                        float centerZ = coord.y * _chunkSize + _chunkSize / 2f;

                        var weights = SampleBiomeWeights(centerX, centerZ);
                        var sortedWeights = weights.OrderByDescending(pair => pair.Value).ToList();

                        BiomeType dominantBiome0 = sortedWeights[0].Key;
                        BiomeType dominantBiome1 = sortedWeights.Count > 1 ? sortedWeights[1].Key : dominantBiome0;

                        GenerateDensityField(coord,
                            out float[,,] bottomField,
                            out float[,,] caveField,
                            out float[,,] stoneField,
                            out float[,,] topField,
                            _chunkHeight);

                        _readyChunks.Enqueue(new ChunkGenResult
                        {
                            coord = coord,

                            bottomField = bottomField,
                            caveField = caveField,
                            stoneField = stoneField,
                            topField = topField,

                            dominantBiome0 = dominantBiome0,
                            dominantBiome1 = dominantBiome1
                        });
                    }
                    finally
                    {
                        _chunkGenSemaphore.Release();
                    }
                }).Start();

                countThisFrame++;

                if (countThisFrame >= 30)
                {
                    countThisFrame = 0;

                    yield return new WaitForSecondsRealtime(0.5f);
                }
            }

            while (_generatingChunks.Count > 0)
            {
                yield return null;
            }
        }

        private void LateUpdate()
        {
            Camera[] cameras = Camera.allCameras;

            if (cameras == null || cameras.Length == 0)
            {
                cameras = new Camera[1] { null };
            }

            float MinDistanceToCameras(Vector3 chunkPos)
            {
                float minDist = float.MaxValue;

                foreach (var cam in cameras)
                {
                    Vector3 camPos = cam != null ? cam.transform.position : transform.position;

                    float dist = Vector2.Distance(new Vector2(chunkPos.x, chunkPos.z), new Vector2(camPos.x, camPos.z));

                    if (dist < minDist) minDist = dist;
                }
                return minDist;
            }

            var toGameObject = new List<Vector2Int>();
            var toInstance = new List<Vector2Int>();

            foreach (var kvp in _instancedChunks)
            {
                Vector3 pos = kvp.Value.matrix.GetColumn(3);

                if (MinDistanceToCameras(pos) < _instanceDistance * 0.8f) toGameObject.Add(kvp.Key);

            }

            foreach (var kvp in _chunkObjects)
            {
                var go = kvp.Value;

                if (go != null && go.activeSelf && MinDistanceToCameras(go.transform.position) > _instanceDistance)
                {
                    toInstance.Add(kvp.Key);
                }
            }

            foreach (var kvp in _chunkObjects)
            {
                var go = kvp.Value;

                if (go == null) continue;

                float dist = MinDistanceToCameras(go.transform.position);

                if (go.activeSelf && dist > _instanceDistance)
                {
                    go.SetActive(false);
                }
                else if (!go.activeSelf && dist < _instanceDistance * 0.8f)
                {
                    go.SetActive(true);
                }
            }

            int processedToGameObject = 0;

            foreach (var coord in toGameObject)
            {
                if (_instancedChunks.TryGetValue(coord, out var data))
                {
                    if (_chunkObjects.TryGetValue(coord, out var oldGo) && oldGo != null)
                    {
                        oldGo.SetActive(true);
                        oldGo.transform.position = data.matrix.GetColumn(3);

                        _instancedChunks.Remove(coord);

                        processedToGameObject++;

                        if (processedToGameObject >= 1) break;
                        continue;
                    }
                    if (_chunkObjects.ContainsKey(coord)) continue;

                    if (_modifiedChunks.TryGetValue(coord, out var chunkData))
                    {
                        StartCoroutine(RecreateChunk(coord, chunkData));
                    }
                    else
                    {
                        GameObject go = new($"Chunk_{coord.x}_{coord.y}");
                        go.transform.position = data.matrix.GetColumn(3);

                        var mf = go.AddComponent<MeshFilter>();
                        mf.mesh = data.mesh;

                        var mr = go.AddComponent<MeshRenderer>();
                        mr.material = data.material;

                        _chunkObjects[coord] = go;
                    }

                    _instancedChunks.Remove(coord);

                    processedToGameObject++;
                    if (processedToGameObject >= 1) break;
                }
            }
            foreach (var coord in toInstance)
            {
                if (_chunkObjects.TryGetValue(coord, out var go))
                {
                    var mf = go.GetComponent<MeshFilter>();
                    var mr = go.GetComponent<MeshRenderer>();

                    if (mf != null && mr != null)
                    {
                        var data = new InstancedChunkData
                        {
                            mesh = mf.mesh,
                            material = mr.material,
                            matrix = Matrix4x4.TRS(go.transform.position, Quaternion.identity, Vector3.one)
                        };

                        _instancedChunks[coord] = data;
                    }

                    go.SetActive(false);
                }
            }

            int processed = 0;

            while (_readyChunks.TryDequeue(out var result))
            {
                if (_chunkObjects.ContainsKey(result.coord) || _instancedChunks.ContainsKey(result.coord))
                {
                    _generatingChunks.Remove(result.coord);

                    continue;
                }

                if (_modifiedChunks.TryGetValue(result.coord, out var chunkData))
                {
                    StartCoroutine(RecreateChunk(result.coord, chunkData));
                    _generatingChunks.Remove(result.coord);

                    continue;
                }

                float dist = MinDistanceToCameras(new Vector3(result.coord.x * _chunkSize, 0, result.coord.y * _chunkSize));

                if (dist > _instanceDistance)
                {
                    Mesh mesh = GenerateMesh(result.topField);
                    Material mat = GetMaterialForBiome(result.dominantBiome0);

                    var data = new InstancedChunkData
                    {
                        mesh = mesh,
                        material = mat,
                        matrix = Matrix4x4.TRS(new Vector3(result.coord.x * _chunkSize, 0, result.coord.y * _chunkSize), Quaternion.identity, Vector3.one)
                    };

                    _instancedChunks[result.coord] = data;
                    _generatingChunks.Remove(result.coord);

                    continue;
                }

                Vector2Int[] directions = new Vector2Int[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };

                int layerId = LayerMask.NameToLayer("Chunk");
                GameObject chunkObject = new($"Chunk_{result.coord.x}_{result.coord.y}");
                chunkObject.transform.position = new Vector3(result.coord.x * _chunkSize, 0, result.coord.y * _chunkSize);
                _chunkObjects[result.coord] = chunkObject;

                foreach (var dir in directions)
                {
                    Vector2Int neighborCoord = result.coord + dir;

                    if (_chunkWalls.TryGetValue(neighborCoord, out var walls))
                    {
                        Vector2Int oppositeDir = -dir;

                        if (walls.TryGetValue(oppositeDir, out var wall))
                        {
                            GameObject.Destroy(wall);
                            walls.Remove(oppositeDir);
                        }
                    }
                }

                // Bottom
                GameObject bottomObj = new("BottomLayer");
                bottomObj.transform.parent = chunkObject.transform;
                bottomObj.transform.localPosition = Vector3.zero;
                MeshRenderer bottomRend = bottomObj.AddComponent<MeshRenderer>();
                bottomRend.material = _stoneMaterial;
                MeshFilter bottomMF = bottomObj.AddComponent<MeshFilter>();
                bottomMF.mesh = GenerateMesh(result.bottomField);
                MeshCollider bottomMC = bottomObj.AddComponent<MeshCollider>();

                // Cave
                GameObject caveObj = new("CaveLayer");
                caveObj.transform.parent = chunkObject.transform;
                caveObj.transform.localPosition = Vector3.zero;
                MeshRenderer caveRend = caveObj.AddComponent<MeshRenderer>();
                caveRend.material = _stoneMaterial;
                MeshFilter caveMF = caveObj.AddComponent<MeshFilter>();
                caveMF.mesh = GenerateMesh(result.caveField);
                MeshCollider caveMC = caveObj.AddComponent<MeshCollider>();
                caveObj.layer = layerId;

                // Stone
                GameObject stoneObj = new("StoneLayer");
                stoneObj.transform.parent = chunkObject.transform;
                stoneObj.transform.localPosition = Vector3.zero;
                var stoneRend = stoneObj.AddComponent<MeshRenderer>();
                stoneRend.material = _stoneMaterial;
                var stoneMF = stoneObj.AddComponent<MeshFilter>();
                stoneMF.mesh = GenerateMesh(result.stoneField);
                var stoneMC = stoneObj.AddComponent<MeshCollider>();
                stoneObj.layer = layerId;

                // Top
                GameObject topObj = new("TopLayer");
                topObj.transform.parent = chunkObject.transform;
                topObj.transform.localPosition = Vector3.zero;
                MeshRenderer topRend = topObj.AddComponent<MeshRenderer>();
                MeshFilter topMF = topObj.AddComponent<MeshFilter>();

                if (result.dominantBiome0 == result.dominantBiome1)
                {
                    topRend.material = GetMaterialForBiome(result.dominantBiome0);
                    topMF.mesh = GenerateConnectedTopMesh(result.topField);
                }

                else
                {
                    Material mat0 = GetMaterialForBiome(result.dominantBiome0);
                    Material mat1 = GetMaterialForBiome(result.dominantBiome1);
                    topRend.materials = new Material[] { mat0, mat1 };
                    topMF.mesh = GenerateMeshWithTwoMaterials(result.topField, new Vector3(result.coord.x * _chunkSize, 0, result.coord.y * _chunkSize), result.dominantBiome0, result.dominantBiome1);
                }

                topObj.layer = layerId;

                if (!topObj.TryGetComponent<MeshCollider>(out var topMC))
                {
                    topMC = topObj.AddComponent<MeshCollider>();
                }
                topMC.sharedMesh = topMF.mesh;

                GenerateGrassForChunk(
                    chunkCoord: result.coord,
                    topMesh: topMF.mesh,
                    dominantBiome0: result.dominantBiome0,
                    dominantBiome1: result.dominantBiome1,
                    parent: topObj.transform,
                    worldPosition: chunkObject.transform.position
                );

                foreach (var dir in directions)
                {
                    Vector2Int neighborCoord = result.coord + dir;

                    if (!_chunkObjects.ContainsKey(neighborCoord))
                    {
                        CreateChunkWall(result.coord, dir, chunkObject);
                    }
                }

                _generatingChunks.Remove(result.coord);

                processed++;

                if (processed >= MaxChunksPerFrame) break;
            }
        }

        #region Grass
        private void GenerateGrassForChunk(
            Vector2Int chunkCoord,
            Mesh topMesh,
            BiomeType dominantBiome0,
            BiomeType dominantBiome1,
            Transform parent,
            Vector3 worldPosition)
        {
            if (!IsGrassBiome(dominantBiome0) && !IsGrassBiome(dominantBiome1)) return;
            if (_grassMesh == null || _grassMaterial == null) return;

            // Remove old grass if exists
            if (_chunkGrassData.ContainsKey(chunkCoord))
            {
                RemoveGrassFromChunk(chunkCoord);
            }

            Vector3[] vertices = topMesh.vertices;
            Vector3[] normals = topMesh.normals;

            List<GrassInstance> grassInstances = new();
            List<CombineInstance> combineInstances = new();

            GameObject grassParent = new("Grass");
            grassParent.transform.SetParent(parent);
            grassParent.transform.localPosition = Vector3.zero;

            MeshFilter grassFilter = grassParent.AddComponent<MeshFilter>();
            MeshRenderer grassRenderer = grassParent.AddComponent<MeshRenderer>();
            grassRenderer.material = _grassMaterial;

            int grassCount = 0;
            for (int i = 0; i < normals.Length; i++)
            {
                if (grassCount >= _maxGrassPerChunk) break;

                float slopeAngle = Vector3.Angle(normals[i], Vector3.up);
                if (slopeAngle > _grassMaxSlopeAngle) continue;

                if (UnityEngine.Random.value > _grassDensity) continue;

                Vector3 vertex = vertices[i];
                Vector3 position = worldPosition + vertex;

                // Random variations
                position.x += UnityEngine.Random.Range(-0.3f, 0.3f);
                position.z += UnityEngine.Random.Range(-0.3f, 0.3f);
                
                Quaternion rotation = Quaternion.Euler(0, UnityEngine.Random.Range(0, 360f), 0);
                float scale = UnityEngine.Random.Range(0.8f, 1.2f);
                Vector3 scaleVec = new(scale, scale, scale);
                Matrix4x4 matrix = Matrix4x4.TRS(position, rotation, scaleVec);
                
                // Create instance
                GrassInstance instance = new()
                {
                    Position = position,
                    Rotation = rotation,
                    Scale = scaleVec,
                    Matrix = matrix,
                    ChunkCoord = chunkCoord
                };

                Vector3Int positionKey = Vector3Int.zero;
                bool keyIsUnique = false;
                int maxAttempts = 5;
                
                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    positionKey = new Vector3Int(
                        Mathf.RoundToInt(position.x * 100) + attempt,
                        Mathf.RoundToInt(position.y * 100),
                        Mathf.RoundToInt(position.z * 100)
                    );

                    if (!_allGrassInstances.ContainsKey(positionKey))
                    {
                        keyIsUnique = true;

                        break;
                    }
                }

                if (!keyIsUnique) continue;

                // Save instance
                grassInstances.Add(instance);
                _allGrassInstances[positionKey] = instance;
                
                // For combined mesh
                combineInstances.Add(new CombineInstance
                {
                    mesh = _grassMesh,
                    transform = matrix
                });

                grassCount++;
            }

            // Save grass data
            _chunkGrassData[chunkCoord] = grassInstances;
            
            // Create combined mesh
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
        
        private Dictionary<Vector2Int, List<Vector3Int>> _chunkGrassKeys = new();

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
                if (_chunkObjects.TryGetValue(chunkCoord, out var chunkObj))
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
            if (!_chunkObjects.TryGetValue(chunkCoord, out var chunkObj)) return;
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

        private void RemoveGrassFromChunk(Vector2Int chunkCoord)
        {
            if (!_chunkGrassData.TryGetValue(chunkCoord, out var grassInstances)) return;
            
            foreach (var instance in grassInstances)
            {
                Vector3Int positionKey = new(
                    Mathf.RoundToInt(instance.Position.x * 10),
                    Mathf.RoundToInt(instance.Position.y * 10),
                    Mathf.RoundToInt(instance.Position.z * 10)
                );
                _allGrassInstances.Remove(positionKey);
            }

            if (_chunkObjects.TryGetValue(chunkCoord, out var chunkObj))
            {
                Transform grassParent = chunkObj.transform.Find("Grass");
                if (grassParent != null) Destroy(grassParent.gameObject);
            }

            _chunkGrassData.Remove(chunkCoord);
        }
        #endregion

        private void OnRenderObject()
        {
            foreach (var kvp in _instancedChunks)
            {
                var data = kvp.Value;

                if (data.mesh != null && data.material != null)
                {
                    Graphics.DrawMeshInstanced(data.mesh, 0, data.material, new[] { data.matrix });
                }
            }
        }

        public void SetModifiedChunk(Vector2Int coord, ChunkData data)
        {
            _modifiedChunks[coord] = data;
        }

        void GenerateDensityField(Vector2Int chunkCoord,
                                    out float[,,] bottomField,
                                    out float[,,] caveField,
                                    out float[,,] stoneField,
                                    out float[,,] topField,
                                    int chunkHeight)
        {

            int size = _chunkSize + 1;
            // Alas, to avoid the error in Unity, you need to consider one more point on each axis than the number of cubes
            // Due to a peculiarity of the Marching Cubes method.
            // Well, in order not to forget accidentally or not to calculate, thus losing nerves and time 2 debug this error, 
            // I'd rather spend 4 bytes, not such a loss.

            bottomField = new float[size, _chunkHeight + 1, size];
            caveField = new float[size, _chunkHeight + 1, size];
            stoneField = new float[size, _chunkHeight + 1, size];
            topField = new float[size, size, size];
            // topField is 3d only in X and Z with the same size, and in Y size too - it stores “surface” densities down 2 ground level.

            for (int x = 0; x <= _chunkSize; x++)
            {
                for (int y = 0; y <= chunkHeight; y++)
                {
                    for (int z = 0; z <= _chunkSize; z++)
                    {
                        float worldX = x + chunkCoord.x * _chunkSize;
                        float worldY = y + _worldVerticalOffset;
                        float worldZ = z + chunkCoord.y * _chunkSize;

                        var weights = SampleBiomeWeights(worldX, worldZ);
                        // Give the dictionary biome → [0...1]
                        float blendedHeight = 0f;
                        foreach (var kvp in weights)
                        {
                            // KeyValuePair - kvp
                            // BiomeType - b
                            // Weights - w
                            // BiomeSettings - s

                            // Yeah, lol, I'll forget that sooner or later (￣ω￣;)
                            BiomeType b = kvp.Key;
                            float w = kvp.Value;
                            var s = biomeSettings[b];

                            float noise = Mathf.PerlinNoise(
                                (worldX + _seedX) * s.noiseScale,
                                (worldZ + _seedZ) * s.noiseScale);

                            blendedHeight += noise * s.heightMultiplier * w;
                        }

                        float clampedBaseHeight = Mathf.Min(blendedHeight, _chunkSize - 1);
                        // Limits the height so that it does not “look” outside the chunk
                        float density = worldY - clampedBaseHeight;
                        // Difference between the current node height and the reference surface:
                        // negative → inside solid volume,
                        // positive → outside

                        float bottomThreshold = blendedHeight - (_topThickness + _caveThickness);
                        float topThreshold = blendedHeight - _topThickness;

                        //bottomField[x, y, z] = (worldY < bottomThreshold)
                        //  ? density
                        //: 100f;
                        float bottomLimit = _worldVerticalOffset + 0f; // Замените 8f на нужную вам "высоту нижнего слоя"
                        bottomField[x, y, z] = (worldY <= bottomLimit) ? density : 100f;
                        // If the point is below the lower threshold, we record the real density.
                        // Otherwise, set a large positive value (fill “outside” the mesh),
                        // so that Marching Cubes does not draw anything there.

                        const float minStoneDepth = 1f;
                        if (worldY < minStoneDepth)
                        {
                            caveField[x, y, z] = -1f;
                        }
                        // Guarantee a stone “substrate” at source level
                        else if (worldY >= bottomThreshold && worldY < topThreshold)
                        {
                            float3 pos = new(worldX, worldY, worldZ);

                            float noise1 = noise.cnoise(pos * 0.1f);
                            float noise2 = noise.cnoise(pos * 0.3f);
                            float noise3 = noise.cnoise(pos * 0.5f);

                            float fractalNoise = (noise1 + noise2 * 0.5f + noise3 * 0.25f) / 1.75f;
                            float verticalMod = Mathf.Sin(worldY * 0.25f + fractalNoise * Mathf.PI);
                            float volumeNoise = (fractalNoise + verticalMod) * 0.5f;
                            float threshold = Mathf.SmoothStep(0.5f, 0.6f, volumeNoise);

                            if (threshold > 0.45f && threshold < 0.6f
                                && noise.cnoise(pos * 0.08f + new float3(12, 33, 17)) > 0.1f)
                            {
                                float caveDepthMod = Mathf.Lerp(-10f, -3f, (threshold - 0.4f) / 0.2f);
                                caveField[x, y, z] = density + caveDepthMod;
                            }
                            else
                            {
                                caveField[x, y, z] = 100f;
                            }
                            // If the noise allows - record slightly shifted density, otherwise “plug” the field 100f, so that the cave does not cut through.
                        }
                        else
                        {
                            caveField[x, y, z] = 100f;
                        }

                        float stoneLower = topThreshold - _stoneThickness;

                        if (worldY >= stoneLower && worldY < topThreshold)
                        {
                            stoneField[x, y, z] = worldY - clampedBaseHeight;
                        }
                        else
                        {
                            stoneField[x, y, z] = 100f;
                        }

                        if (worldY >= topThreshold && worldY <= blendedHeight)
                        {
                            float modification = 0f;
                            foreach (var kvp in weights)
                            {
                                // Same as SampleBiomeWeights
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
                                        mod = (worldY < blendedHeight - 1f ? -2f : 0f)
                                            + Mathf.PerlinNoise(worldX * 0.1f, worldZ * 0.1f) * 1.5f;
                                        break;
                                    case BiomeType.Mountains:
                                        mod = Mathf.PerlinNoise(worldX * 0.3f, worldZ * 0.3f) * 5f;
                                        break;
                                    case BiomeType.Ocean:
                                        mod = Mathf.PerlinNoise(worldX * 0.2f, worldZ * 0.2f) * 0.2f;
                                        break;
                                }
                                // Noise settings for different biomes
                                // It's important, when adding new

                                modification += mod * w;
                            }

                            topField[x, y, z] = density + modification;
                        }
                        else
                        {
                            topField[x, y, z] = 100f;
                        }
                        // Outside of this zone, we set 100f to prevent Marching Cubes from generating top layer where it is not needed
                    }
                }
            }
        }

        void CreateChunkWall(Vector2Int chunkCoord, Vector2Int direction, GameObject parent)
        {
            float wallThickness = 2f;
            float height = _chunkHeight;
            float size = _chunkSize;
            float halfThick = wallThickness * 0.5f;

            float xOffset = _chunkSize / 2;
            float zOffset = _chunkSize / 2;

            if (direction.x != 0)
            {
                if (direction.x > 0)
                {
                    xOffset = size + halfThick;
                }
                else
                {
                    xOffset = -halfThick;
                }
            }

            if (direction.y != 0)
            {
                if (direction.y > 0)
                {
                    zOffset = size + halfThick;
                }
                else
                {
                    zOffset = -halfThick;
                }
            }

            Vector3 localOffset = new(xOffset, height * 0.5f, zOffset);

            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = $"Wall_{direction.x}_{direction.y}";
            wall.transform.SetParent(parent.transform, false);
            wall.transform.localPosition = localOffset;

            Vector3 localScale = (direction.x != 0)
                ? new Vector3(wallThickness, height, size)
                : new Vector3(size, height, wallThickness);
            wall.transform.localScale = localScale;

            Destroy(wall.GetComponent<MeshFilter>());
            Destroy(wall.GetComponent<MeshRenderer>());

            if (!_chunkWalls.TryGetValue(chunkCoord, out var dict))
            {
                dict = new Dictionary<Vector2Int, GameObject>();
                _chunkWalls[chunkCoord] = dict;
            }
            dict[direction] = wall;
        }

        private Mesh GetMeshFromPool()
        {
            if (_meshPool.Count > 0)
            {
                Mesh mesh = _meshPool.Dequeue();
                mesh.Clear();
                // reset data (vertices, indices) of the previous mesh to start (from scratch).
                return mesh;
            }
            return new Mesh();
            // if the queue is empty, create a new Mesh()
        }

        Mesh GenerateMesh(float[,,] densityField)
        {
            Mesh mesh = GetMeshFromPool();
            // Get empty or cleaned mesh from the pool
            List<Vector3> vertices = new();
            List<int> triangles = new();
            // Temporary collections where geometry will be collected.

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
                        // MarchCube takes the current cube (position + density array) and, according to the existing tables, adds to vertices and triangles the desired triangles that approximate the isosurface.
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
            // Displacements of the cube corners from the pos base position.
            // Example: (0,0,0) - lower left corner, (1,1,1) - upper right corner.

            // Each cube in the 3d grid has 8 vertices. 4 each vertex the density value from the densityField array is determined.
            // Here's specifies the positions of all 8 corners of the cube and an array to store the density values in these corners.
            for (int i = 0; i < 8; i++)
            {
                Vector3 worldPos = pos + cornerPositions[i];
                cubeCorners[i] = densityField[(int)worldPos.x, (int)worldPos.y, (int)worldPos.z];
                if (cubeCorners[i] < _isoLevel) cubeIndex |= 1 << i;
                // 1 << i is a bitwise shift.
                // For example, if i = 3, then 1 << 3 is 00001000. 
                // Bit number 3 is set to 1.
            }
            // The density values at each vertex of the cube are queried.
            // Based on them, cubeIndex is calculated, which describes which corners are inside and which are outside the isosurface.

            if (MarchingCubesTables.TriTable[cubeIndex, 0] == -1) return;
            // If the cube is completely empty or completely filled, no triangles are built.

            Vector3[] edgeVertices = new Vector3[12];
            // array of points of intersection of the surface with the edges of the cube.
            for (int i = 0; i < 12; i++)
            {
                if ((MarchingCubesTables.EdgeTable[cubeIndex] & (1 << i)) == 0)
                    continue;

                int a = MarchingCubesTables.EdgeConnections[i, 0];
                int b = MarchingCubesTables.EdgeConnections[i, 1];
                // a, b are the numbers of two corners of the cube connected by edge i.
                // Example: edge 0 connects corner 0 and corner 1.

                Vector3 pA = pos + cornerPositions[a];
                Vector3 pB = pos + cornerPositions[b];
                // pA, pB - positions of angles a and b in space.

                float vA = cubeCorners[a];
                float vB = cubeCorners[b];
                // vA, vB - densities in these two corners.

                edgeVertices[i] = InterpolateVerts(pA, pB, vA, vB);
            }
            // The points of intersection of the isosurface with the edges of the cube are determined through interpolation between the angles of the cube.

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
            // Based on the points on the edges, triangles are constructed and added to the final vertex and triangle lists.
        }

        Vector3 InterpolateVerts(Vector3 p1, Vector3 p2, float v1, float v2)
        {
            const float eps = 1e-7f;
            // The question probably arises, why do I use my Epsilon instead of Mathf.Epsilon?
            // Well, probably because 1e-7f is more reasonable for comparing coordinates, vectors and angles,
            // and also because in REAL density calculations small fluctuations(noise) are normal, and Mathf.Epsilon is too accurate and small
            // Simply put, if accurate comparisons are needed - Matf.Epsilon
            // For practical tasks, 1e-7f

            if (Mathf.Abs(v1 - _isoLevel) < eps) return p1;
            if (Mathf.Abs(v2 - _isoLevel) < eps) return p2;
            if (Mathf.Abs(v2 - v1) < eps) return (p1 + p2) * 0.5f;
            // v1, v2: These are the values of a scalar field (e.g. density) at the two ends of the cube edge.
            // They determine how far each vertex is above or below a given isosurface level (IsoLevel).

            float t = (_isoLevel - v1) / (v2 - v1);
            // This is an interpolation parameter that determines how far between p1 and p2 is the intersection point of the isosurface with the edge.
            // The value of t ranges from 0 to 1, where 0 corresponds to p1 and 1 corresponds to p2
            t = Mathf.Clamp01(t);
            t = t * t * t * (t * (t * 6f - 15f) + 10f);
            // Why does t multiply by itself several times? Xdddd
            // This formula is a fifth degree polynomial that provides zero first and second derivatives at the ends of the interval [0,1], which eliminates abrupt changes and makes interpolation smoother
            // Read: https://iquilezles.org/articles/smoothsteps/
            return Vector3.Lerp(p1, p2, t);
            // The point between p1 and p2 is found based on t
        }

        Material GetMaterialForBiome(BiomeType biome)
        // We check materials and add them to specific meshes.
        // If there is no material, create a new one
        {
            return biome switch
            {
                BiomeType.Desert => (Material)(_desertMaterial != null ? _desertMaterial : CreateMaterial(Color.yellow)),
                BiomeType.Plains => (Material)(_plainsMaterial != null ? _plainsMaterial : CreateMaterial(Color.green)),
                BiomeType.Forest => (Material)(_forestMaterial != null ? _forestMaterial : CreateMaterial(new Color(0.13f, 0.55f, 0.13f))),
                BiomeType.Swamp => (Material)(_swampMaterial != null ? _swampMaterial : CreateMaterial(new Color(0.3f, 0.5f, 0.3f))),
                BiomeType.Mountains => (Material)(_mountainsMaterial != null ? _mountainsMaterial : CreateMaterial(Color.gray)),
                BiomeType.Ocean => (Material)(_waterMaterial != null ? _waterMaterial : CreateMaterial(Color.white)),
                _ => (Material)CreateMaterial(Color.white),
            };
        }

        Material CreateMaterial(Color color)
        {
            Material mat = new(_shader)
            {
                color = color
            };

            return mat;
        }

        public Dictionary<BiomeType, BiomeSettings> biomeSettings = new()
        {
            { BiomeType.Desert, new BiomeSettings { noiseScale = 0.05f, heightMultiplier = 13f, chunkHeight = 64 } },
            { BiomeType.Plains, new BiomeSettings { noiseScale = 0.01f, heightMultiplier = 15f, chunkHeight = 128 } },
            { BiomeType.Forest, new BiomeSettings { noiseScale = 0.04f, heightMultiplier = 15f, chunkHeight = 96 } },
            { BiomeType.Swamp, new BiomeSettings { noiseScale = 0.03f, heightMultiplier = 12f, chunkHeight = 80 } },
            { BiomeType.Mountains, new BiomeSettings { noiseScale = 0.05f, heightMultiplier = 16f, chunkHeight = 128 } },
            { BiomeType.Ocean, new BiomeSettings { noiseScale = 0.01f, heightMultiplier = 15f, chunkHeight = 128 } }
        };
        // These settings define the landscape generation characteristics for each biome

        Dictionary<BiomeType, float> SampleBiomeWeights(float worldX, float worldZ)
        {
            Vector2 samplePos = new(worldX / _chunkSize, worldZ / _chunkSize);
            // Fractional coordinates of the point in “chunk units”
            Vector2Int baseChunk = new(Mathf.FloorToInt(samplePos.x), Mathf.FloorToInt(samplePos.y));
            // Integer indices of the chunk where the point is located, decrease the fractional part to get the “left-bottom” chunk

            Dictionary<BiomeType, float> weights = new();
            float totalWeight = 0f; // It's needed for further normalization (to make the sum of weights equal to 1)

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    // We traverse all the chunks around the base chunk (including the base chunk itself) by shifting dx, dz ∈ {-1,0,1}
                    Vector2Int neighborChunk = baseChunk + new Vector2Int(dx, dz);
                    if (!_biomeMap.ContainsKey(neighborChunk)) continue;
                    // If the biome for neighborChunk has not been calculated yet, we skip it

                    Vector2 center = new(neighborChunk.x + 0.5f, neighborChunk.y + 0.5f);
                    float dist = Vector2.Distance(samplePos, center);
                    float weight = 1f / (dist * dist + 0.01f);
                    // Inverse square distance weighting
                     
                    BiomeType biome = _biomeMap[neighborChunk];
                    if (!weights.ContainsKey(biome))
                        weights[biome] = 0f;
                    weights[biome] += weight;
                    totalWeight += weight;
                    // Increase totalWeight at the same time
                }
            }

            foreach (var key in weights.Keys.ToList())
                weights[key] /= totalWeight;
            // By dividing each weight by the total sum, we achieve that all weights together give exactly 1.
            // Weights[biome] can then be used directly as a fraction of each biome's contribution.

            return weights;
        }

        Mesh GenerateMeshWithTwoMaterials(float[,,] densityField, Vector3 chunkOrigin, BiomeType dominantBiome0, BiomeType dominantBiome1)
        {
            Mesh mesh = GetMeshFromPool();

            List<Vector3> vertices = new();

            List<int> trianglesSubmesh0 = new();
            List<int> trianglesSubmesh1 = new();

            for (int x = 0; x < _chunkSize; x++)
            {
                for (int y = 0; y < _chunkSize; y++)
                {
                    for (int z = 0; z < _chunkSize; z++)
                    {
                        MarchCubeMulti(new Vector3Int(x, y, z), densityField, vertices, trianglesSubmesh0, trianglesSubmesh1, chunkOrigin, dominantBiome0, dominantBiome1);
                        // A variant of MarchingCubesMulti that immediately determines to which submesh each triangle is assigned (under the first or second material) depending on its position within the mixing zone of the two dominant biomes.
                    }
                }
            }

            mesh.subMeshCount = 2;

            mesh.SetVertices(vertices);
            mesh.SetTriangles(trianglesSubmesh0, 0);
            mesh.SetTriangles(trianglesSubmesh1, 1);
            // Divide triangle indices into two submeshes:
            // 0 submesh is rendered by the first material (dominantBiome0),
            // 1 submesh is rendered by the second material (dominantBiome1).

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
            // Relative coordinates of the 8 corners of the cube

            for (int i = 0; i < 8; i++)
            {
                Vector3 localPos = pos + cornerPositions[i];
                cubeCorners[i] = densityField[(int)localPos.x, (int)localPos.y, (int)localPos.z];
                if (cubeCorners[i] < _isoLevel)
                    cubeIndex |= 1 << i;
            }
            // Is assembled bitwise: 4 each vertex where density < IsoLevel, set bit 1 << i

            if (MarchingCubesTables.TriTable[cubeIndex, 0] == -1)
                return;
            // If 4 a given cubeIndex in the TriTable table is -1 at the beginning, then the cube is either completely “inside” or “outside” the isosurface. 
            // Triangles are not needed - return

            Vector3[] edgeVertices = new Vector3[12];
            for (int i = 0; i < 12; i++)
            {
                if ((MarchingCubesTables.EdgeTable[cubeIndex] & (1 << i)) != 0) // The 12-bit mask shows which edges are intersected by the isosurface
                {
                    int a = MarchingCubesTables.EdgeConnections[i, 0];
                    int b = MarchingCubesTables.EdgeConnections[i, 1];

                    edgeVertices[i] = InterpolateVerts(pos + cornerPositions[a], pos + cornerPositions[b], cubeCorners[a], cubeCorners[b]);
                }
                // For each active edge, take two angles a and b (from EdgeConnections) and call InterpolateVerts to find the intersection point on that edge
            }

            for (int i = 0; MarchingCubesTables.TriTable[cubeIndex, i] != -1; i += 3)
            {
                int vertexIndex = vertices.Count;

                Vector3 v0 = edgeVertices[MarchingCubesTables.TriTable[cubeIndex, i]];
                Vector3 v1 = edgeVertices[MarchingCubesTables.TriTable[cubeIndex, i + 1]];
                Vector3 v2 = edgeVertices[MarchingCubesTables.TriTable[cubeIndex, i + 2]];
                // Interpolated vertices

                Vector3 worldV0 = chunkOrigin + v0;
                Vector3 worldV1 = chunkOrigin + v1;
                Vector3 worldV2 = chunkOrigin + v2;

                Vector3 centroid = (worldV0 + worldV1 + worldV2) / 3f;
                // Centroid of the triangle

                Dictionary<BiomeType, float> triWeights = SampleBiomeWeights(centroid.x, centroid.z);
                // Give us the weights of all the biomes at a point, & we only take the two dominant ones

                float w0 = triWeights.ContainsKey(dominantBiome0) ? triWeights[dominantBiome0] : 0f;
                float w1 = triWeights.ContainsKey(dominantBiome1) ? triWeights[dominantBiome1] : 0f;

                bool assignToFirst = w0 >= w1;
                // If w0 ≥ w1, the triangle goes to submesh 0, otherwise it goes to submesh 1

                vertices.Add(v0);
                vertices.Add(v1);
                vertices.Add(v2);
                // Light variants of peaks

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
                // Index lists for the first and second material
            }
        }

        private Mesh WeldCloseVertices(Mesh mesh, float threshold = 1f)
        {
            Vector3[] oldVerts = mesh.vertices;
            // Array of all vertices of the source mesh.
            int[] oldTris = mesh.triangles;
            // Array of triangle indices, every three numbers in it form one triangle
            List<Vector3> newVerts = new();
            // Store only one copy of each “close” vertex group.
            int[] map = new int[oldVerts.Length];
            // 4 each initial vertex "i" stores it's new index in newVerts

            for (int i = 0; i < oldVerts.Length; i++)
            {
                Vector3 v = oldVerts[i];
                // Search every old vertex v
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
                // If a vertex is found that is not farther away than threshold, consider them as “one” and memorize them
                if (!found)
                {
                    map[i] = newVerts.Count;
                    newVerts.Add(v);
                }
                // If we haven't found a close 1, we add "v" to newVerts & put its map on a new index.
            }
            // newVerts - contains only “digested” vertices.
            // map - knows how to translate old indices into new ones.

            int[] newTris = new int[oldTris.Length];
            // 4 each reference to an old vertex oldTris[i], we substitute its new index from map
            for (int i = 0; i < oldTris.Length; i++)
            {
                newTris[i] = map[oldTris[i]];
            }
            // We get newTris - a complete index array correctly pointing to vertices in newVerts

            Mesh newMesh = new()
            {
                vertices = newVerts.ToArray(),
                triangles = newTris
            };
            // Create a new Mesh, fill it with a list of unique vertices and updated triangles.

            newMesh.RecalculateNormals();

            return newMesh;
        }
        private Mesh GenerateConnectedTopMesh(float[,,] topField)
        {
            Mesh raw = GenerateMesh(topField);
            // First we generate the raw surface of the top layer

            return WeldCloseVertices(raw, threshold: 0.01f);
            // Then we connect vertices that are closer than 0.01 units to each other
        }
    }
}