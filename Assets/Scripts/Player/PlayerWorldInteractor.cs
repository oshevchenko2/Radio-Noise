using System;
using System.Collections.Generic;
using UnityEngine;

namespace Player
{
    public class PlayerWorldInteractor : MonoBehaviour
    {
        [SerializeField] private float _reachDistance = 5f;
        [SerializeField] private LayerMask _chunkLayer;
        [SerializeField] private float _shapeSize = 0.5f;
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
            if (Input.GetKeyDown(KeyCode.Tab))
                _shape = (ShapeType)(((int)_shape + 1) % Enum.GetValues(typeof(ShapeType)).Length);
            // Switch shape type
            if (Input.GetMouseButtonDown(0)) RemoveTriangle();
            if (Input.GetMouseButtonDown(1)) AddVolume();

            if(Time.time - lastCheckObjectInstance > 3){
                lastCheckObjectInstance = Time.time;

                for(int i = 0; i < Shape.List.Count; i++)
                {
                    if(Shape.isNear(i, transform.position)){
                        GameObjectInstance go = Instantiate(_shapeMeshes[Shape.List[i].GetType()], Shape.List[i].position, Quaternion.identity);
                        go.Camera = transform;
                        go.Index = i;
                    }
                } 
            }
            
            Graphics.DrawMeshInstanced(_cubeMesh, 0, _addMaterial, Shape.getMatrixArr<Cube>());            
            Graphics.DrawMeshInstanced(_cylinderMesh, 0, _addMaterial, Shape.getMatrixArr<Cylinder>());            
            Graphics.DrawMeshInstanced(_prismMesh, 0, _addMaterial, Shape.getMatrixArr<Prism>());            
        }

        void RemoveTriangle()
        {
            var ray = _cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit, _reachDistance, _chunkLayer)) return;
            // If ray does'nt hit the layer at the _reachDistance - return
            var go = hit.collider.gameObject;
            if (go.name.StartsWith("Volume_")) Destroy(go);
            // If hit obj with prefix Volume_ - destroy it!
            
            var mf = hit.collider.GetComponent<MeshFilter>();
            var mc = hit.collider.GetComponent<MeshCollider>();
            if (mf == null || mc == null) return;
            
            RemoveTriangle(mf.mesh, mc, hit.triangleIndex);
        }

        void RemoveTriangle(Mesh mesh, MeshCollider meshCollider, int globalTriIndex)
        {
            int sub = FindSubMeshIndex(mesh, globalTriIndex);
            // Determine in which submesh we have triangle
            if (sub < 0) return;
            // If not found - return
            
            var tris = new List<int>(mesh.GetTriangles(sub));
            // Creating list of submesh triangle index
            int before = 0;
            for (int i = 0; i < sub; i++) before += mesh.GetTriangles(i).Length / 3;
            // Count the total number of triangles in earlier submeshes

            int local = globalTriIndex - before;
            int idx = local * 3;
            if (idx < 0 || idx + 2 >= tris.Count) return;
            // Check that the index is in the valid range
            
            tris.RemoveRange(idx, 3);
            // Removes 3 indexes(1 triangle)
            
            mesh.SetTriangles(tris, sub);
            mesh.RecalculateNormals();
            
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = mesh;
                // Refresh collider and set mesh again
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
            Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, _reachDistance, _chunkLayer)) return;

            float cellSize = _shapeSize;

            Vector3Int offsetDirection = GetGridOffsetDirection(hit.normal);
            Vector3Int hitGridPos = WorldToGridPosition(hit.point, cellSize);
            Vector3Int targetGridPos = hitGridPos + offsetDirection;
            
            Vector3 targetPosition = GridToWorldPosition(targetGridPos, cellSize);

            targetPosition -= hit.normal * (cellSize * 0.5f);

            Vector3 halfExtents = GetShapeHalfExtents();
            if (Physics.CheckBox(targetPosition, halfExtents, Quaternion.identity, _chunkLayer))
                return;

            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
            // Rotate the shape so that its upward direction is along the surface normal
            CreateShapeAt(targetPosition, rotation);
        }

        Vector3Int WorldToGridPosition(Vector3 worldPos, float cellSize)
        {
            return new Vector3Int
            (
                Mathf.FloorToInt(worldPos.x / cellSize),
                Mathf.FloorToInt(worldPos.y / cellSize),
                Mathf.FloorToInt(worldPos.z / cellSize)
            );
            // Divide the world coordinates by the cell size and round down
        }

        Vector3 GridToWorldPosition(Vector3Int gridPos, float cellSize)
        {
            return new Vector3
            (
                (gridPos.x + 0.5f) * cellSize,
                (gridPos.y + 0.5f) * cellSize,
                (gridPos.z + 0.5f) * cellSize
            );
            // Translate the cell coordinates back to the cell center in world coordinates
        }

        Vector3Int GetGridOffsetDirection(Vector3 normal)
        {
            normal = normal.normalized;
            Vector3Int dir = Vector3Int.zero;

            float max = Mathf.Max(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));

            if (Mathf.Abs(normal.x) == max)
                dir.x = normal.x > 0 ? 1 : -1;
            else if (Mathf.Abs(normal.y) == max)
                dir.y = normal.y > 0 ? 1 : -1;
            else
                dir.z = normal.z > 0 ? 1 : -1;

            return dir;
        }

        Vector3 GetShapeHalfExtents()
        {
            float size = _shapeSize * 0.5f * 0.95f;
            return _shape switch
            {
                ShapeType.Cube => new Vector3(size, size, size),
                ShapeType.Cylinder => new Vector3(size, size, size),
                ShapeType.Triangle => new Vector3(size, size, size),
                _ => Vector3.one * size
            };
            // We return halfExtents with a small "reserve" so that the colliders do not stick
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
            //ConfigureVolumeObject(o, p, r);
            
            new Cube(p);
            GameObjectInstance go = Instantiate(_cubeGameObject, p,Quaternion.identity);
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
            GameObjectInstance go = Instantiate(_cylinderGameObject, p,r);
            go.Camera = transform;
            go.Index = Cylinder.List.Count - 1;
            
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
            GameObjectInstance go = Instantiate(_prismGameObject, p,r);
            go.Camera = transform;
            go.Index = Prism.List.Count - 1;
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
            float hw = width * 0.5f;
            float hh = height * 0.5f;
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