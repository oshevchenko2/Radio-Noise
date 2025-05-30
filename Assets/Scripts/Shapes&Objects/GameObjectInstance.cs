using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class GameObjectInstance : MonoBehaviour
{
    public Transform Camera;
    public int Index;
    
    void Start()
    {
        StartCoroutine(Wait4());
        ActiveInstances.Add(this);
    }

    void Destroy()
    {
        ActiveInstances.Remove(this);
    }

    public static List<GameObjectInstance> ActiveInstances = new();

    private IEnumerator Wait4()
    {
        while (true)
        {
            yield return new WaitForSeconds(2);
            if (Index >= 0 && Index < Shape.List.Count && 
                Mathf.Abs(Camera.position.x - Shape.List[Index].Position.x) + 
                Mathf.Abs(Camera.position.z - Shape.List[Index].Position.z) > 32)
                {
                    Shape.List[Index].IsSpawned = false;
                    Destroy(gameObject);
                }
        }
    }
}