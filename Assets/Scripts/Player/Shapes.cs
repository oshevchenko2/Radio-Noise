using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;
public abstract class  Shape : ShapeDataInterface
{
    public static List<Shape> List { get; set; } = new List<Shape>();
    public Vector3 position{get;set;}
    public bool isSpawned {get;set;} = true;

    public virtual Matrix4x4 getMatrix(){
        return Matrix4x4.TRS(position, Quaternion.identity, Vector3.one);
    }

    public static Matrix4x4[] getMatrixArr<T>(){
        List<Matrix4x4> mat = new();
        for(int i = 0; i < List.Count; i++)
        {
            if(List[i].GetType() == typeof(T))
            mat.Add(List[i].getMatrix());
        }
        return mat.ToArray();
    }

    public static bool isNear(int i, Vector3 pos){
        return Mathf.Abs(pos.x-List[i].position.x)+Mathf.Abs(pos.z-List[i].position.z) < 32 && !List[i].isSpawned;
    }
}
public class Cube : Shape
{
    public Cube(Vector3 position)
    {
        List.Add(this);
        this.position = position;
    }
}
public class Cylinder : Shape, QuaternionShapeDataInterface
{
    public Quaternion quaternion{get;set;}

    //public Quaternion quaternion = Quaternion.identity;

    public Cylinder(Vector3 position, Quaternion quaternion)
    {
        List.Add(this);
        this.position = position;
        this.quaternion = quaternion;
    }
    public override Matrix4x4 getMatrix(){
        return Matrix4x4.TRS(position, quaternion, Vector3.one);
    }
}

public class Prism : Shape, QuaternionShapeDataInterface
{
    public Quaternion quaternion{get;set;}

    //public Quaternion quaternion = Quaternion.identity;

    public Prism(Vector3 position, Quaternion quaternion)
    {
        List.Add(this);
        this.position = position;
        this.quaternion = quaternion;
    }
    public override Matrix4x4 getMatrix(){
        return Matrix4x4.TRS(position, quaternion, Vector3.one);
    }
}