using UnityEngine;

namespace TerrainGenerator
{
    public class ChunkGenResult
    {
        public Vector2Int Coord;
        public float[,,] BottomField;
        public float[,,] CaveField;
        public float[,,] StoneField;
        public float[,,] TopField;
        public BiomeType DominantBiome0;
        public BiomeType DominantBiome1;
    }
}