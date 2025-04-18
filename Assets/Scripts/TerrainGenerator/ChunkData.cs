using System;
using Unity.VisualScripting;
using UnityEngine;

namespace TerrainGenerator
{
    [Serializable]
    public class ChunkData
    {
        public int ChunkX;
        public int ChunkZ;

        public MeshData BottomMesh;
        public MeshData CaveMesh;
        public MeshData TopMesh;

        internal int chunkX;
    }
}