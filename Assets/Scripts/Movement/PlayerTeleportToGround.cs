using TerrainGenerator;
using UnityEngine;

public class PlayerTeleport : MonoBehaviour
{
    [SerializeField] private VoxelTerrain _terrain;
    
    public float rayLength = 100f;
    public float yOffset = 0.5f;
    public LayerMask groundLayer;

    void Start()
    {
        Vector3 randomPosition = new(Random.Range(0, _terrain.WorldSize), 100f, Random.Range(_terrain.WorldSize, 0));

        if (Physics.Raycast(randomPosition, Vector3.down, out RaycastHit hit, rayLength, groundLayer))
        {
            Vector3 newPosition = hit.point;
            newPosition.y += yOffset;

            transform.position = newPosition;

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
        }
    }
}