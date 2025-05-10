using System.Collections.Generic;
using TerrainGenerator;
using Unity.Entities.UniversalDelegates;
using UnityEngine;

namespace Player
{
    public class PlayerWorldInteractor : MonoBehaviour
    {
        [SerializeField] private float _reachDistance  = 5f;
        [SerializeField] private LayerMask _chunkLayer;
        [SerializeField] private float _triangleSize   = 0.5f;
        [SerializeField] private float _triangleHeight = 0.2f;
        [SerializeField] private Material _addMaterial;

        private Camera _cam;

        void Start()
        {
            _cam = GetComponent<Camera>();
        }

        void Update()
        {
            if (Input.GetMouseButtonDown(0)) TryRemoveTriangle();
            if (Input.GetMouseButtonDown(1)) TryAddVolume();
        }

        void TryRemoveTriangle()
        {
            Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit, _reachDistance, _chunkLayer))
                return;

            int mask  = _chunkLayer.value;
            int layer = hit.collider.gameObject.layer;
            if ((mask & (1 << layer)) == 0)
                return;

            var filter = hit.collider.GetComponent<MeshFilter>();
            var collider = hit.collider.GetComponent<MeshCollider>();
            if (filter == null || collider == null)
                return;

            RemoveTriangleSafe(filter.mesh, hit.triangleIndex);

            collider.sharedMesh = null;
            collider.sharedMesh = filter.mesh;
        }

        void RemoveTriangleSafe(Mesh mesh, int triIndex)
        {
            int[] tris = mesh.triangles;
            int triCount = tris.Length / 3;
            
            if (triIndex < 0 || triIndex >= triCount)
                return;

            int start = triIndex * 3;
            
            if (start + 2 >= tris.Length)
                return;

            var newTris = new List<int>(tris);
            newTris.RemoveRange(start, 3);

            mesh.triangles = newTris.ToArray();
            mesh.RecalculateNormals();
        }

        void TryAddVolume()
        {
            Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
            
            if (!Physics.Raycast(ray, out var hit, _reachDistance, _chunkLayer))
                return;

            Vector3 pos = hit.point + hit.normal * 0.01f;
            Quaternion rot = Quaternion.LookRotation(hit.normal);

            GameObject tri = new("TriangleVolume")
            {
                layer = hit.collider.gameObject.layer
            };

            tri.transform.SetPositionAndRotation(pos, rot);
            
            var mf = tri.AddComponent<MeshFilter>();
            var mr = tri.AddComponent<MeshRenderer>();
            
            mr.material = _addMaterial;
            
            var mc = tri.AddComponent<MeshCollider>();

            Mesh m = new();
            
            float s = _triangleSize;
            float h = _triangleHeight;

            Vector3 v0 = new(0, 0, 0);
            Vector3 v1 = new(s, 0, 0);
            Vector3 v2 = new(0, s, 0);
            Vector3 v3 = new(s/2f, 0, h);

            m.vertices = new Vector3[] { v0, v1, v2, v3 };

            m.triangles = new int[]
            {
                0, 2, 1,
                0, 1, 3,
                1, 2, 3,
                2, 0, 3
            };

            m.RecalculateNormals();
            mf.mesh = m;
            mc.sharedMesh = m;
        }
    }
}