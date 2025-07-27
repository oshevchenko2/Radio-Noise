using FishNet.Object.Synchronizing;
using UnityEngine;
using FishNet.Object;
using System.Collections.Generic;

namespace Player
{
    public class WorldShapeManager : NetworkBehaviour
    {
        public static WorldShapeManager Instance { get; private set; }

        public readonly SyncDictionary<int, NetworkShapeData> shapes = new();

        private HashSet<int> _createdIds = new();

        private int nextId = 0;
        private readonly Dictionary<int, GameObject> spawnedObjects = new();

        [SerializeField] private GameObject trianglePrefab;
        [SerializeField] private GameObject cubePrefab;
        [SerializeField] private GameObject cylinderPrefab;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else if (Instance != this)
                Destroy(gameObject);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            shapes.OnChange += OnShapesChanged;

            foreach (var kvp in shapes)
                SpawnShape(kvp.Key, kvp.Value);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            shapes.OnChange -= OnShapesChanged;

            foreach (var go in spawnedObjects.Values)
                Destroy(go);
            spawnedObjects.Clear();
            _createdIds.Clear();
        }

        private void OnShapesChanged(
            SyncDictionaryOperation op,
            int key,
            NetworkShapeData newValue,
            bool asServer)
        {
            switch (op)
            {
                case SyncDictionaryOperation.Add:
                    SpawnShape(key, newValue);
                    break;
                case SyncDictionaryOperation.Remove:
                    RemoveLocalShape(key);
                    DespawnShape(key);
                    break;
            }
        }


        private void SpawnShape(int id, NetworkShapeData data)
        {
            if (_createdIds.Contains(id)) return;
            _createdIds.Add(id);

            switch (data.Shape)
            {
                case ShapeType.Cube:
                    new Cube(data.Position);
                    break;
                case ShapeType.Cylinder:
                    new Cylinder(data.Position, data.Rotation);
                    break;
                case ShapeType.Triangle:
                    new Prism(data.Position, data.Rotation);
                    break;
            }
            
            GameObject prefab = GetPrefabForShape(data.Shape);
            if (prefab == null)
                return;

            GameObject go = Instantiate(prefab, data.Position, data.Rotation);

            if (IsServerInitialized)
                Spawn(go);

            spawnedObjects[id] = go;

            var instance = go.GetComponent<GameObjectInstance>();
            if (instance != null)
            {
                instance.PlayerCamera = Camera.main != null ? Camera.main.transform : null;
                instance.Index = id;
            }
        }

        private void RemoveLocalShape(int id)
        {
            Shape.SafeRemoveAt(id);
        }

        private void DespawnShape(int id)
        {
            if (spawnedObjects.TryGetValue(id, out GameObject go))
            {
                Destroy(go);
                spawnedObjects.Remove(id);
            }
        }

        private void ClearLocalShapes()
        {
            Shape.List.Clear();
            _createdIds.Clear();
        }

        private GameObject GetPrefabForShape(ShapeType shape)
        {
            return shape switch
            {
                ShapeType.Triangle => trianglePrefab,
                ShapeType.Cube => cubePrefab,
                ShapeType.Cylinder => cylinderPrefab,
                _ => null,
            };
        }

        [ServerRpc(RequireOwnership = false)]
        public void AddShapeServerRpc(Vector3 position, Quaternion rotation, ShapeType shape)
        {
            int id = nextId++;
            shapes.Add(id, new NetworkShapeData(position, rotation, shape));
        }

        [ServerRpc(RequireOwnership = false)]
        public void RemoveShapeServerRpc(int id)
        {
            if (shapes.ContainsKey(id))
                shapes.Remove(id);
        }
    }
}
