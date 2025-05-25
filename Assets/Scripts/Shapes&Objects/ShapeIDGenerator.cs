using UnityEngine;

public static class ShapeIdGenerator
{
    private static int _nextId = 1;
    public static int GetNextId() => _nextId++;
}