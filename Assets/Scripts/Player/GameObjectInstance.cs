using System.Collections;
using UnityEngine;

public class GameObjectInstance : MonoBehaviour
{
    public Transform Camera;
    public int Index;
    void Start()
    {
        StartCoroutine(wait4());
    }

    private IEnumerator wait4()
    {
        while(true){
            yield return new WaitForSeconds(3);
            if(Mathf.Abs(Camera.position.x-Cube.List[Index].position.x)+Mathf.Abs(Camera.position.z-Cube.List[Index].position.z) > 32)
            {
                Cube.List[Index].isSpawned = false;
                Destroy(gameObject);
            }
        }
    }
}