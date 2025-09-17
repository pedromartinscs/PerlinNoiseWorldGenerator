using System.Collections.Generic;
using UnityEngine;

// Enums remain in PerlinNoiseGenerator.cs

[System.Serializable]
public struct Cell
{
    public TileType tile;   // Land/Water
    public BiomeType biome; // Forest/Desert
}

[System.Serializable]
public struct ShoreInfo
{
    public bool present;
    public short code;          // index into shorePrefabsByCode
    public float rotationY;     // degrees
    public Vector2 offset;      // tile units (x->world X, y->world Z)
    public float yOffset;       // lift
}

[System.Serializable]
public struct DecoInfo
{
    public bool present;
    public short code;          // biome-specific prefab index (renderer maps via arrays)
    public float rotationY;     // degrees
    public float scale;         // uniform
    public Vector2 offset;      // tile units
    public float yOffset;       // lift
} 

[System.Serializable]
public class MapData
{
    public readonly int width;
    public readonly int height;
    public readonly float tileSize;
    public readonly int seed;

    public readonly Cell[,] cells;

    // MULTI-PIECE per tile:
    public readonly List<ShoreInfo>[,] shores;
    public readonly List<DecoInfo>[,]  decorations;
	
	public readonly float[,] featureValue;

    public MapData(int width, int height, float tileSize = 1f, int seed = 0)
    {
        this.width = width;
        this.height = height;
        this.tileSize = tileSize;
        this.seed = seed;
		
		featureValue = new float[width, height];

        cells = new Cell[width, height];

        shores = new List<ShoreInfo>[width, height];
        decorations = new List<DecoInfo>[width, height];

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            shores[x, y] = new List<ShoreInfo>(2);
            decorations[x, y] = new List<DecoInfo>(1);
        }
    }

    public Cell this[int x, int y]
    {
        get => cells[x, y];
        set => cells[x, y] = value;
    }

    // Shores helpers
    public List<ShoreInfo> GetShores(int x, int y) => shores[x, y];
    public void SetShores(int x, int y, List<ShoreInfo> items)
    {
        var list = shores[x, y];
        list.Clear();
        if (items != null && items.Count > 0) list.AddRange(items);
    }

    // Decorations helpers
    public List<DecoInfo> GetDecos(int x, int y) => decorations[x, y];
    public void SetDecos(int x, int y, List<DecoInfo> items)
    {
        var list = decorations[x, y];
        list.Clear();
        if (items != null && items.Count > 0) list.AddRange(items);
    }
	
	public float GetFeatureValue(int x, int y) => featureValue[x, y];
	public void  SetFeatureValue(int x, int y, float v) => featureValue[x, y] = v;
}
