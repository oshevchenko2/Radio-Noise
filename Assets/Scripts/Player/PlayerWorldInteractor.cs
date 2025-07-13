
using System;
using System.Collections.Generic;
using System.Linq;
using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    public class PlayerWorldInteractor : NetworkBehaviour
    {
        [SerializeField] private float _reachDistance = 5;
        [SerializeField] private LayerMask _chunkLayer;
        [SerializeField] private float _shapeSize = 1;
        [SerializeField] private Material _addMaterial;

        private ShapeType _shape = ShapeType.Triangle;

        [SerializeField] private Mesh _cubeMesh;
        [SerializeField] private Mesh _cylinderMesh;
        [SerializeField] private Mesh _prismMesh;

        [SerializeField] private GameObjectInstance _cubeGameObject;
        [SerializeField] private GameObjectInstance _cylinderGameObject;
        [SerializeField] private GameObjectInstance _prismGameObject;

        [SerializeField] Dictionary<Type, GameObjectInstance> _shapeMeshes = new();

        private float lastCheckObjectInstance = 0;

        private Camera _cam;
        private int _chunkLayerIndex;

        void Start()
        {
            _shapeMeshes.Add(typeof(Cube), _cubeGameObject);
            _shapeMeshes.Add(typeof(Cylinder), _cylinderGameObject);
            _shapeMeshes.Add(typeof(Prism), _prismGameObject);
        
            _prismMesh = GenerateTriangularPrismMesh(_shapeSize, _shapeSize);
        
            _cam = GetComponent<Camera>();
        
            _chunkLayerIndex = GetFirstLayerFromMask(_chunkLayer);
            // Convert LayerMask 2 a numeric layer index 4 easy invocation
        }

        void Update()
        {
            if (!IsOwner) return;

            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
            {
                _shape = (ShapeType)(((int)_shape + 1) % Enum.GetValues(typeof(ShapeType)).Length);
                Debug.Log($"Current shape: {_shape}");
            }

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                RemoveTriangle();
                Debug.Log($"Removed {_shape} at {transform.position}");
            }

            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                AddVolume();
                Debug.Log($"Added {_shape} at {transform.position}");
            }

            if (Time.time - lastCheckObjectInstance > 3)
                {
                    lastCheckObjectInstance = Time.time;

                    for (int i = 0; i < Shape.List.Count; i++)
                    {
                        if (Shape.IsNear(i, transform.position))
                        {
                            GameObjectInstance go = Instantiate(_shapeMeshes[Shape.List[i].GetType()], Shape.List[i].Position, Quaternion.identity);
                            Shape.List[i].IsSpawned = true;
                            go.Camera = transform;
                            go.Index = i;
                        }
                    }
                }

            // Cubes
            var cubeList = Shape.List.Where(s => s is Cube).Cast<Cube>().ToList();
            var cubeMats = new Matrix4x4[cubeList.Count];
            for (int i = 0; i < cubeList.Count; i++)
                cubeMats[i] = cubeList[i].GetMatrix();
            if (cubeMats.Length > 0)
                Graphics.DrawMeshInstanced(_cubeMesh, 0, _addMaterial, cubeMats);

            // Cylinders
            var cylList = Shape.List.Where(s => s is Cylinder).Cast<Cylinder>().ToList();
            var cylMats = new Matrix4x4[cylList.Count];
            for (int i = 0; i < cylList.Count; i++)
                cylMats[i] = cylList[i].GetMatrix();
            if (cylMats.Length > 0)
                Graphics.DrawMeshInstanced(_cylinderMesh, 0, _addMaterial, cylMats);

            // Prisms
            var prismList = Shape.List.Where(s => s is Prism).Cast<Prism>().ToList();
            var prismMats = new Matrix4x4[prismList.Count];
            for (int i = 0; i < prismList.Count; i++)
                prismMats[i] = prismList[i].GetMatrix();
            if (prismMats.Length > 0)
                Graphics.DrawMeshInstanced(_prismMesh, 0, _addMaterial, prismMats);
        }

        void RemoveTriangle()
        {
            Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            var ray = _cam.ScreenPointToRay(mousePos);
            if (!Physics.Raycast(ray, out var hit, _reachDistance, _chunkLayer))
                return;

            var go = hit.collider.gameObject;
            if (go.name.StartsWith("Volume_"))
            {
                var inst = go.GetComponent<GameObjectInstance>();
                if (inst != null && inst.Index >= 0 && inst.Index < Shape.List.Count)
                {
                    Shape.SafeRemoveAt(inst.Index);
                    
                    foreach (var o in GameObjectInstance.ActiveInstances)
                        if (o.Index > inst.Index)
                            o.Index--;
                    
                    Destroy(go);
                }
                return;
            }

            var mf = hit.collider.GetComponent<MeshFilter>();
            var mc = hit.collider.GetComponent<MeshCollider>();
            if (mf == null || mc == null) return;

            RemoveTriangle(mf.mesh, mc, hit.triangleIndex);
        }

        void RemoveTriangle(Mesh mesh, MeshCollider meshCollider, int triangleIndex)
        {
            int subMesh = FindSubMeshIndex(mesh, triangleIndex);
            if (subMesh < 0) return;

            int count = 0;
            for (int i = 0; i < subMesh; i++)
                count += mesh.GetTriangles(i).Length / 3;
            int triLocalIdx = triangleIndex - count;

            int[] tris = mesh.GetTriangles(subMesh);
            if (triLocalIdx < 0 || triLocalIdx >= tris.Length / 3) return;
            
            int triStart = triLocalIdx * 3;
            int triA = tris[triStart];
            int triB = tris[triStart + 1];
            int triC = tris[triStart + 2];

            List<int> newTris = new(tris);
            newTris.RemoveRange(triStart, 3);
            mesh.SetTriangles(newTris, subMesh);

            Vector3[] verts = mesh.vertices;
            Vector3[] oldPositions = { verts[triA], verts[triB], verts[triC] };
            
            verts[triA] += Vector3.down * 0.5f;
            verts[triB] += Vector3.down * 0.5f;
            verts[triC] += Vector3.down * 0.5f;

            float epsilon = 0.001f;
            for (int i = 0; i < verts.Length; i++)
            {
                for (int j = 0; j < oldPositions.Length; j++)
                {
                    if (Vector3.Distance(verts[i], oldPositions[j]) < epsilon)
                    {
                        verts[i] += Vector3.down * 0.5f;
                    }
                }
            }

            mesh.vertices = verts;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = mesh;
        }

        int FindSubMeshIndex(Mesh mesh, int triangleIndex)
        {
            int count = 0;
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                int c = mesh.GetTriangles(i).Length / 3;
                if (triangleIndex < count + c) return i;
                count += c;
            }
            return -1;
        }
        
        void AddVolume()
        {
            Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            Ray ray = _cam.ScreenPointToRay(mousePos);
            if (!Physics.Raycast(ray, out RaycastHit hit, _reachDistance)) return;

            float cellSize = _shapeSize;
            Vector3 halfExtents = GetShapeHalfExtents();
            Vector3 normal = hit.normal.normalized;

            Vector3 placementOffset = normal * cellSize;
            Vector3 targetPoint = hit.point + placementOffset;

            Vector3Int grid = new(
                Mathf.RoundToInt(targetPoint.x / cellSize),
                Mathf.RoundToInt(targetPoint.y / cellSize),
                Mathf.RoundToInt(targetPoint.z / cellSize)
            );
            Vector3 targetPosition = GridToWorldPosition(grid, cellSize);

            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, normal);

            bool hasCollision = false;
            Collider[] colliders;

            if (_shape == ShapeType.Cylinder)
            {
                colliders = Physics.OverlapCapsule(
                    targetPosition - rotation * Vector3.up * halfExtents.y,
                    targetPosition + rotation * Vector3.up * halfExtents.y,
                    halfExtents.x,
                    _chunkLayer
                );
            }
            else
            {
                colliders = Physics.OverlapBox(
                    targetPosition, 
                    halfExtents, 
                    rotation, 
                    _chunkLayer
                );
            }

            foreach (var col in colliders)
            {
                if (col != hit.collider)
                {
                    hasCollision = true;
                    break;
                }
            }

            if (!hasCollision)
            {
                CreateShapeAt(targetPosition, rotation);
            }

        }

        Vector3Int WorldToGridPosition(Vector3 worldPos, float cellSize)
        {
            return new Vector3Int(
                Mathf.RoundToInt(worldPos.x / cellSize),
                Mathf.RoundToInt(worldPos.y / cellSize),
                Mathf.RoundToInt(worldPos.z / cellSize)
            );
            // Divide the world coordinates by the cell size and round to nearest
        }

        Vector3 GridToWorldPosition(Vector3Int gridPos, float cellSize)
        {
            return new Vector3
            (
                gridPos.x * cellSize,
                gridPos.y * cellSize,
                gridPos.z * cellSize
            );
            // Translate the cell coordinates back to the cell center in world coordinates
        }

        Vector3Int GetGridOffsetDirection(Vector3 normal)
        {
            if (Mathf.Abs(normal.x) >= Mathf.Abs(normal.y) && Mathf.Abs(normal.x) >= Mathf.Abs(normal.z))
                return new Vector3Int(normal.x > 0 ? 1 : -1, 0, 0);
            if (Mathf.Abs(normal.y) >= Mathf.Abs(normal.x) && Mathf.Abs(normal.y) >= Mathf.Abs(normal.z))
                return new Vector3Int(0, normal.y > 0 ? 1 : -1, 0);
            return new Vector3Int(0, 0, normal.z > 0 ? 1 : -1);
        }

        Vector3 GetShapeHalfExtents()
        {
            return _shape switch
            {
                ShapeType.Cube => new Vector3(_shapeSize/2, _shapeSize/2, _shapeSize/2),
                ShapeType.Cylinder => new Vector3(_shapeSize/2, _shapeSize/2, _shapeSize/2),
                ShapeType.Triangle => new Vector3(_shapeSize/2, _shapeSize/4, _shapeSize/2),
                _ => Vector3.one * _shapeSize/2
            };
            
        }

        void CreateShapeAt(Vector3 position, Quaternion rotation)
        {
            switch (_shape)
            {
                case ShapeType.Cube:
                    CreateCubeAt(position, rotation);
                    break;
                case ShapeType.Cylinder:
                    CreateCylinderAt(position, rotation);
                    break;
                case ShapeType.Triangle:
                    CreatePrismAt(position, rotation);
                    break;
            }
        }

        void CreateCubeAt(Vector3 p, Quaternion r)
        {
            //var o = GameObject.CreatePrimitive(PrimitiveType.Cube);

            new Cube(p);

            GameObjectInstance go = Instantiate(_cubeGameObject, p,Quaternion.identity);

            //var go = Instantiate(_cylinderGameObject, p, r);

            ConfigureVolumeObject(go.gameObject, p, r);

            go.Camera = transform;
            go.Index = Cube.List.Count - 1;
            
            // o - primitive cube
            // p - position
            // r - material

            //o.transform.localScale = Vector3.one * _shapeSize;
        }

        void CreateCylinderAt(Vector3 p, Quaternion r)
        {
            //var o = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            //ConfigureVolumeObject(o, p, r);
            // o - primitive cylinder
            // p - position
            // r - material

            new Cylinder(p, r);

            GameObjectInstance go = Instantiate(_cylinderGameObject, p, r);

            go.Camera = transform;
            go.Index = Shape.List.Count - 1;
            
            //o.transform.localScale = new Vector3(_shapeSize, _shapeSize, _shapeSize);
        }

        void CreatePrismAt(Vector3 p, Quaternion r)
        {
            /*var o = new GameObject("Volume_TriangularPrism");
            // Empty prism object
            ConfigureVolumeObject(o, p, r);
            // o - primitive cylinder
            // p - position
            // r - material
            
            var mf = o.AddComponent<MeshFilter>();
            var mr = o.AddComponent<MeshRenderer>();
            mr.material = _addMaterial;
            
            var mc = o.AddComponent<MeshCollider>();
            mc.convex = true;
            
            mf.mesh = GenerateTriangularPrismMesh(_shapeSize, _shapeSize);
            mc.sharedMesh = mf.mesh;
            */
            // mf - MeshFilter
            // mr - MesRenderer
            // mc - MeshCollider

            new Prism(p, r);

            GameObjectInstance go = Instantiate(_prismGameObject, p, r);
            ConfigureVolumeObject(go.gameObject, p, r);

            go.Camera = transform;
            go.Index = Shape.List.Count - 1;
        }

        void ConfigureVolumeObject(GameObject obj, Vector3 position, Quaternion rotation)
        {
            obj.name = $"Volume_{_shape}";
            obj.layer = _chunkLayerIndex;
            obj.transform.SetPositionAndRotation(position, rotation);
            if (obj.TryGetComponent<MeshRenderer>(out var renderer)) renderer.material = _addMaterial;
            
            if (obj.TryGetComponent<Collider>(out var collider)) collider.isTrigger = false;
        }

        Mesh GenerateTriangularPrismMesh(float width, float height)
        {
            float hw = width * 1f;
            float hh = height * 1f;
            // hw - half width of prism obj
            // hh - half height of prism obj

            Vector3[] vertices = new Vector3[]
            {
                new(-hw, -hh, hw),
                new(hw, -hh, hw),
                new(0, -hh, -hw),
                new(-hw, hh, hw),
                new(hw, hh, hw),
                new(0, hh, -hw)
            };
            // 6 Prism vertices

            int[] triangles = new int[]
            {
                0, 2, 1, // Bottom
                3, 4, 5, // Top
                0, 1, 4, 0, 4, 3, // Front
                1, 2, 5, 1, 5, 4, // Right
                2, 0, 3, 2, 3, 5 // Left
            };
            // Prism indexes. 5 Faces

            Mesh mesh = new()
            {
                vertices = vertices,
                triangles = triangles
            };

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            
            return mesh;
        }

        private int GetFirstLayerFromMask(LayerMask mask)
        {
            int layerMask = mask.value;
            if (layerMask == 0) return 0;
            // If no mask - retrun
            
            int layer = 0;
            while ((layerMask & (1 << layer)) == 0) layer++;
            // Shift the bit mask until find first bit set

            return layer;
        }
    }
}