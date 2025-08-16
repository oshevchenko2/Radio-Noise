using UnityEngine;
using System;
using System.Collections.Generic;

namespace TerrainGenerator
{
    [Serializable]
    public class TreePrefabData
    {
        public GameObject TreePrefab;
        public float SpawnProbability = 1f;
    }
}