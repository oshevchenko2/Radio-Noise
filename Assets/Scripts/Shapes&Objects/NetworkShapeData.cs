using System;
using UnityEngine;

[Serializable] public struct NetworkShapeData
{
    public Vector3 Position;
    public Quaternion Rotation;
    public ShapeType Shape;

    public NetworkShapeData(Vector3 position, Quaternion rotation, ShapeType shape)
    {
        Position = position;
        Rotation = rotation;
        Shape = shape;
    }
}
