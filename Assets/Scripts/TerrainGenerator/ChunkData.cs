// This project & code is licensed under the MIT License. See the ./LICENSE file for details.
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
        public MeshData StoneMesh;
        public MeshData TopMesh;

        internal int chunkX;
    }
}