using System.Collections.Generic;
using UnityEngine;

public static class PlayerCameraRegistry
{
    public static readonly List<Transform> PlayerCameras = new();

    public static void Register(Transform cam)
    {
        if (cam != null && !PlayerCameras.Contains(cam))
            PlayerCameras.Add(cam);
    }

    public static void Unregister(Transform cam)
    {
        if (cam != null)
            PlayerCameras.Remove(cam);
    }

    public static Transform GetClosestCamera(Vector3 toPosition)
    {
        Transform closest = null;
        float minDistance = float.MaxValue;

        foreach (var cam in PlayerCameras)
        {
            if (cam == null) continue;
            float dist = Vector3.Distance(toPosition, cam.position);
            if (dist < minDistance)
            {
                minDistance = dist;
                closest = cam;
            }
        }

        return closest;
    }
}
