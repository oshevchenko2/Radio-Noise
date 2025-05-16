// This project & code is licensed under the MIT License. See the ./LICENSE file for details.
using System;
using UnityEngine;

namespace TerrainGenerator
{
    [Serializable]
    public class BiomeMapEntry
    {
        public int ChunkX;
        public int ChunkZ;
        public int BiomeType;
    }
}