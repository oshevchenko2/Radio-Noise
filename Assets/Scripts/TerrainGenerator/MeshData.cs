using System;
using UnityEngine;

namespace TerrainGenerator
{
    [Serializable]
    public class MeshData
    {
        public SerializableVector3[] Vertices;
        public int[] Triangles;
    }
}