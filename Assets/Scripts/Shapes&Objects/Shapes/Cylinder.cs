using UnityEngine;

public class Cylinder : Shape, IQuaternionShapeData
{
    public Quaternion Quaternion { get; set; }

    public Cylinder(Vector3 position, Quaternion quaternion)
    {
        List.Add(this);
        this.Position = position;
        this.Quaternion = quaternion;
    }
    public override Matrix4x4 GetMatrix()
    {
        return Matrix4x4.TRS(Position, Quaternion, Vector3.one);
    }
}