using System.Collections;
using FishNet.Object;
using TerrainGenerator;
using UnityEngine;

public class PlayerTeleport : NetworkBehaviour
{
    [SerializeField] private VoxelTerrain _terrain;
    
    [SerializeField] private float rayLength = 100f;
    [SerializeField] private float yOffset = 0.5f;

    [SerializeField] private LayerMask groundLayer;

    [SerializeField] private GameObject _loadingScreen;

    [SerializeField] private GameObject _player;

    private static int _serverSpawnCount = 0;

    void Awake()
    {
        ResetPosition();
    }

    void ResetPosition()
    {
        if (_terrain == null) return;

        Vector3 randomPosition = new(Random.Range(0, _terrain.WorldSize), 30, Random.Range(0, _terrain.WorldSize));

        if (_player.TryGetComponent<CharacterController>(out var controller))
        {
            //controller.enabled = false;
            transform.position = randomPosition;
            //controller.enabled = true;
        }
        else
        {
            transform.position = randomPosition;
        }
    }

    public override void OnStartClient()
    {
        if (_serverSpawnCount > 0)
        {
            ResetPosition();
        }

        StartCoroutine(TeleportWhenGroundReady());

        _serverSpawnCount++;
    }

    private IEnumerator TeleportWhenGroundReady()
    {
        if (!IsOwner) yield return null;

        if (_terrain == null)
        {
            yield break;
        }

        bool teleported = false;

        while (!teleported)
        {
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, rayLength, groundLayer))
            {
                Vector3 newPosition = hit.point;
                newPosition.y += yOffset;

                if (_player.TryGetComponent<CharacterController>(out var controller2))
                {
                    controller2.enabled = false;

                    transform.position = newPosition;

                    controller2.enabled = true;
                }
                else
                {
                    transform.position = newPosition;
                }

                teleported = true;

                if (_loadingScreen != null)
                {
                    yield return new WaitForSeconds(1f);
                    _loadingScreen.SetActive(false);
                }
            }
            else
            {
                yield return new WaitForSeconds(0.1f);
            }
        }
    }
}