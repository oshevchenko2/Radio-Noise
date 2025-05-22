using UnityEngine;

public interface ShapeDataInterface
{
    Vector3 position {get; set;}
    bool isSpawned{get;set;}
}

public interface QuaternionShapeDataInterface : ShapeDataInterface
{
    Quaternion quaternion{get;set;}
}