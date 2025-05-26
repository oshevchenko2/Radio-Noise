using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class Shape : IShapeDataInterface
{
    public static List<Shape> List { get; private set; } = new List<Shape>();
    private static Matrix4x4[] _cachedMatrices;

    public Vector3 Position { get; set; }
    public bool IsSpawned { get; set; } = true;
    public bool IsRenderingActive { get; set; } = true;

    public static event Action OnShapeListChanged;

    public virtual Matrix4x4 GetMatrix()
    {
        return IsRenderingActive ? Matrix4x4.TRS(Position, Quaternion.identity, Vector3.one) : Matrix4x4.zero;
    }

    public static Matrix4x4[] GetMatrixArr<T>() where T : Shape
    {
        if (_cachedMatrices != null) return _cachedMatrices;

        List<Matrix4x4> matrices = new();
        for (int i = 0; i < List.Count; i++)
        {
            if (List[i] is T shape && shape.IsSpawned) matrices.Add(shape.GetMatrix());
        }

        _cachedMatrices = matrices.ToArray();

        return _cachedMatrices;
    }

    public static bool IsNear(int i, Vector3 pos)
    {
        return Mathf.Abs(pos.x - List[i].Position.x) + Mathf.Abs(pos.z - List[i].Position.z) < 32 && !List[i].IsSpawned;
    }

    public static void SafeRemoveAt(int index)
    {
        if (index >= 0 && index < List.Count)
        {
            List.RemoveAt(index);
            OnShapeListChanged?.Invoke();
        }
    }
}