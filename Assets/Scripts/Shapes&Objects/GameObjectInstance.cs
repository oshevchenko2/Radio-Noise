using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Unity.VisualScripting;
using UnityEngine;

public class GameObjectInstance : NetworkBehaviour
{
    public Transform PlayerCamera;
    public int Index;

    void OnEnable()
    {
        Debug.Log($"GameObjectInstance enabled, Index = {Index}, name = {gameObject.name}");
    }

    void OnDisable()
    {
        Debug.Log($"GameObjectInstance disabled, Index = {Index}, name = {gameObject.name}");
    }
    
    void Start()
    {
        PlayerCamera = PlayerCameraRegistry.GetClosestCamera(transform.position);
        StartCoroutine(Wait4());
        ActiveInstances.Add(this);
    }

    void OnDestroy()
    {
        PlayerCameraRegistry.Unregister(PlayerCamera);
        ActiveInstances.Remove(this);
    }

    public static List<GameObjectInstance> ActiveInstances = new();

    private IEnumerator Wait4()
    {
        while (true)
        {
            yield return new WaitForSeconds(2);
            if (Index < 0 || Index >= Shape.List.Count)
                continue;

            Vector3 shapePos = Shape.List[Index].Position;
            Transform closestCam = PlayerCameraRegistry.GetClosestCamera(shapePos);

            if (closestCam == null) continue;

            float dx = Mathf.Abs(closestCam.position.x - shapePos.x);
            float dz = Mathf.Abs(closestCam.position.z - shapePos.z);

            if (dx + dz > 32)
            {
                Shape.List[Index].IsSpawned = false;
                Destroy(gameObject);
            }
        }
    }
}