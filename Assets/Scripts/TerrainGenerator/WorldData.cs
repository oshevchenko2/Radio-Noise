using System;
using System.Collections.Generic;
using UnityEngine;

namespace TerrainGenerator
{
    [Serializable]
    public class WorldData
    {
        public float SeedX;
        public float SeedZ;

        public List<BiomeMapEntry> BiomeMapEntries;
        public List<ChunkData> ChunkDatas;
    }
}