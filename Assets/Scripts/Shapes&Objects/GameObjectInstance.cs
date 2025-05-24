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
            yield return new WaitForSeconds(3);
            if (Mathf.Abs(Camera.position.x - Cube.List[Index].Position.x) + Mathf.Abs(Camera.position.z - Cube.List[Index].Position.z) > 32)
            {
                Cube.List[Index].IsSpawned = false;
                Destroy(gameObject);
            }
        }
    }
    
    private void OnEnable()
    {
        ActiveInstances.Add(this);
    }

    private void OnDisable()
    {
        ActiveInstances.Remove(this);
    }
}