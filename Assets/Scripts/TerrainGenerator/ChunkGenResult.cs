using UnityEngine;

namespace TerrainGenerator
{
    public class ChunkGenResult
    {
        public Vector2Int coord;
        public float[,,] bottomField;
        public float[,,] caveField;
        public float[,,] stoneField;
        public float[,,] topField;
        public BiomeType dominantBiome0;
        public BiomeType dominantBiome1;
    }
}