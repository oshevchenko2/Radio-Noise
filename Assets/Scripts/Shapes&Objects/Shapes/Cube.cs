using UnityEngine;

public class Cube : Shape
{
    public Cube(Vector3 position)
    {
        List.Add(this);
        this.Position = position;
    }
}