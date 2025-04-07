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