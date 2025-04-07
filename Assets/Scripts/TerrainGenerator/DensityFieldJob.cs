using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

[BurstCompile]
public struct DensityFieldJob : IJobParallelFor
{
    public int ChunkSize;
    public float WorldVerticalOffset;
    public float SeedX;
    public float SeedZ;
    public float NoiseScale;
    public float HeightMultiplier;
    public float TopThickness;
    public float CaveThickness;
    public int ChunkX;
    public int ChunkZ;

    [WriteOnly]
    public NativeArray<float> DensityField;

    public void Execute(int index)
    {
        int fieldSize = ChunkSize;
        int x = index % fieldSize;
        int y = (index / fieldSize) % fieldSize;
        int z = index / (fieldSize * fieldSize);

        float worldX = x + ChunkX * (fieldSize - 1);
        float worldY = y + WorldVerticalOffset;
        float worldZ = z + ChunkZ * (fieldSize - 1);

        float elevationNoise = Mathf.PerlinNoise((worldX + SeedX) * NoiseScale, (worldZ + SeedZ) * NoiseScale);
        float baseHeight = elevationNoise * HeightMultiplier;

        float bottomThreshold = baseHeight - (TopThickness + CaveThickness);
        float density = worldY - baseHeight;

        if (worldY < bottomThreshold)
            DensityField[index] = density;
        else
            DensityField[index] = 200f;
    }
}