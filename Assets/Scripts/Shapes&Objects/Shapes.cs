using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;
public abstract class  Shape : ShapeDataInterface
{
    public static List<Shape> List { get; set; } = new List<Shape>();

    public Vector3 Position { get; set; }
    public bool IsSpawned {get;set;} = true;

    public virtual Matrix4x4 GetMatrix()
    {
        return Matrix4x4.TRS(Position, Quaternion.identity, Vector3.one);
    }

    public static Matrix4x4[] GetMatrixArr<T>()
    {
        List<Matrix4x4> mat = new();
        for(int i = 0; i < List.Count; i++)
        {
            if(List[i].GetType() == typeof(T))
    
            mat.Add(List[i].GetMatrix());
        }
    
        return mat.ToArray();
    }

    public static bool IsNear(int i, Vector3 pos)
    {
        return Mathf.Abs(pos.x-List[i].Position.x)+Mathf.Abs(pos.z-List[i].Position.z) < 32 && !List[i].IsSpawned;
    }
}
