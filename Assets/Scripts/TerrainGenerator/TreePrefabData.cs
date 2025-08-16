using UnityEngine;
using System;

namespace TerrainGenerator
{
    [Serializable]
    public class TreePrefabData
    {
        public GameObject TreePrefab;
        public float SpawnProbability = 1f;
    }
}