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
        [System.Serializable]
    public struct SerializableVector3
        {
            public float X;
            public float Y;
            public float Z;

            public SerializableVector3(float _x, float _y, float _z)
            {
                X = _x; Y = _y; Z = _z;
            }

            public SerializableVector3(Vector3 v)
            {
                X = v.x; Y = v.y; Z = v.z;
            }

            public readonly Vector3 ToVector3() => new(X, Y, Z);
        }
}