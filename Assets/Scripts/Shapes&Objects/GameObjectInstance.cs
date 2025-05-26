using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameObjectInstance : MonoBehaviour
{
    public Transform Camera;
    public int Index;
    
    void Start()
    {
        StartCoroutine(Wait4());
    }

    public static List<GameObjectInstance> ActiveInstances = new();

    private IEnumerator Wait4()
    {
        while (true)
        {
            yield return new WaitForSeconds(2);
            try
            {
                if (Index >= 0 && Index < Shape.List.Count && 
                Mathf.Abs(Camera.position.x - Shape.List[Index].Position.x) + 
                Mathf.Abs(Camera.position.z - Shape.List[Index].Position.z) > 32)
                {
                    Shape.List[Index].IsSpawned = false;
                    Destroy(gameObject);
                }
            }
            catch
            {
                Destroy(gameObject);
            }
        }
    }
}