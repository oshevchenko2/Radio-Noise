using System.Collections;
using TerrainGenerator;
using UnityEngine;

public class PlayerTeleport : MonoBehaviour
{
    [SerializeField] private VoxelTerrain _terrain;
    
    [SerializeField] private float rayLength = 100f;
    [SerializeField] private float yOffset = 0.5f;
    [SerializeField] private LayerMask groundLayer;

    void Start()
    {
        Debug.Log("[PlayerTeleport] Start: Coroutine started");
        StartCoroutine(TeleportWhenGroundReady());
    }

    private IEnumerator TeleportWhenGroundReady()
    {
        //Vector3 randomPosition = new(Random.Range(0, _terrain.WorldSize), 100f, Random.Range(0, _terrain.WorldSize));
        //Debug.Log($"[PlayerTeleport] Trying position: {randomPosition}, rayLength: {rayLength}, groundLayer: {groundLayer}");
        bool teleported = false;

        while (!teleported)
        {
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, rayLength, groundLayer))
            {
                Debug.Log($"[PlayerTeleport] Raycast hit at {hit.point}");
                Vector3 newPosition = hit.point;
                newPosition.y += yOffset;

                if (TryGetComponent<CharacterController>(out var controller))
                {
                    controller.enabled = false;
                    transform.position = newPosition;
                    controller.enabled = true;
                }
                else
                {
                    transform.position = newPosition;
                }

                Debug.Log($"[PlayerTeleport] Teleported to {newPosition}");
                teleported = true;
            }
            else
            {
                Debug.Log("[PlayerTeleport] Raycast did not hit ground. Retrying in 2 seconds.");
                yield return new WaitForSeconds(0.1f);
            }
        }
    }
}