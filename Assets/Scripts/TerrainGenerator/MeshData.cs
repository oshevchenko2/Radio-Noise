using System;

namespace TerrainGenerator
{
    [Serializable]
    public struct MeshData
    {
        public SerializableVector3[] Vertices;
        public int[] Triangles;
        public SerializableVector3[] Normals;
    }
}