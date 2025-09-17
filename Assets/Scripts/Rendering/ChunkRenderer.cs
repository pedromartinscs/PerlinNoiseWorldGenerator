using System.Collections.Generic;
using UnityEngine;

public class ChunkRenderer : MonoBehaviour
{
    [Header("Chunk Settings")]
    [Min(4)] public int chunkSize = 32;
    [Min(1)] public int renderRadius = 2;

    [Header("Scene Refs")]
    public Transform cameraTarget;

    [Header("Prefabs & Materials")]
    public GameObject groundPrefab;
    public GameObject waterPrefab;
    public Material grassMaterial; // Forest
    public Material sandMaterial;  // Desert
	
	[Header("Decorations")]
	public GameObject[] forestTreePrefabs;
	public GameObject[] forestVegetationPrefabs;
	public GameObject[] desertTreePrefabs;
	public GameObject[] desertVegetationPrefabs;
	public GameObject[] randomDecorationPrefabs;
	
	[Header("Shorelines (precomputed)")]
	public GameObject[] shorePrefabsByCode;

    private MapData map;

    private readonly Dictionary<Vector2Int, Transform> chunkRoots = new();
    private readonly HashSet<Vector2Int> active = new();
    private readonly List<Vector2Int> toRemove = new();
	
	public float treeYOffset = 0f;
	public float vegetationYOffset = 0f;
	public float desertTreeThreshold = 0.20f;
	public float desertVegetationThreshold = 0.35f; 

    public void Initialize(
        MapData map,
        GameObject groundPrefab,
        GameObject waterPrefab,
        Material grassMaterial,
        Material sandMaterial,
        Transform cameraTarget,
        int chunkSize = 32,
        int renderRadius = 2)
    {
        this.map = map;
        this.groundPrefab = groundPrefab;
        this.waterPrefab = waterPrefab;
        this.grassMaterial = grassMaterial;
        this.sandMaterial = sandMaterial;
        this.cameraTarget = cameraTarget;
        this.chunkSize = chunkSize;
        this.renderRadius = renderRadius;

        foreach (var kv in chunkRoots)
        {
            if (kv.Value) Destroy(kv.Value.gameObject);
        }
        chunkRoots.Clear();
        active.Clear();
    }

    void Update()
    {
        if (map == null || cameraTarget == null) return;

        Vector2Int camChunk = WorldToChunk(cameraTarget.position);

        // A) Clamp the camera chunk to valid range to avoid chasing negative/out-of-range chunks forever
        camChunk = ClampToMap(camChunk);

        UpdateVisibleChunks(camChunk);
    }

    private Vector2Int WorldToChunk(Vector3 worldPos)
    {
        float ts = map.tileSize;
        int tx = Mathf.FloorToInt(worldPos.x / ts);
        int ty = Mathf.FloorToInt(worldPos.z / ts);
        return new Vector2Int(
            Mathf.FloorToInt((float)tx / chunkSize),
            Mathf.FloorToInt((float)ty / chunkSize)
        );
    }

    private Vector2Int ClampToMap(Vector2Int chunk)
    {
        int maxCx = Mathf.Max(0, Mathf.CeilToInt((float)map.width / chunkSize) - 1);
        int maxCy = Mathf.Max(0, Mathf.CeilToInt((float)map.height / chunkSize) - 1);
        return new Vector2Int(
            Mathf.Clamp(chunk.x, 0, maxCx),
            Mathf.Clamp(chunk.y, 0, maxCy)
        );
    }

    private void UpdateVisibleChunks(Vector2Int center)
    {
        HashSet<Vector2Int> required = new();

        for (int dy = -renderRadius; dy <= renderRadius; dy++)
        {
            for (int dx = -renderRadius; dx <= renderRadius; dx++)
            {
                var cc = new Vector2Int(center.x + dx, center.y + dy);
                // Optional: also clamp here to avoid requesting out-of-map chunks
                cc = ClampToMap(cc);

                required.Add(cc);
                if (!active.Contains(cc))
                {
                    BuildChunk(cc);
                }
            }
        }

        toRemove.Clear();
        foreach (var cc in active)
        {
            if (!required.Contains(cc)) toRemove.Add(cc);
        }
        foreach (var dead in toRemove) DestroyChunk(dead);
    }

    private void BuildChunk(Vector2Int chunk)
	{
		// Compute un-clamped tile rect this chunk wants to cover
		int startX = chunk.x * chunkSize;
		int startY = chunk.y * chunkSize;
		int endX   = startX + chunkSize;
		int endY   = startY + chunkSize;
	
		// Clamp to map bounds
		int sx = Mathf.Max(0, startX);
		int sy = Mathf.Max(0, startY);
		int ex = Mathf.Min(endX, map.width);
		int ey = Mathf.Min(endY, map.height);
	
		// If no overlap, skip
		if (sx >= ex || sy >= ey) return;
	
		// Create parent now that we know we render something
		var root = new GameObject($"Chunk_{chunk.x}_{chunk.y}").transform;
		root.SetParent(transform, false);
		chunkRoots[chunk] = root;
	
		float ts = map.tileSize;
	
		for (int y = sy; y < ey; y++)
		{
			for (int x = sx; x < ex; x++)
			{
				Vector3 cellPos = new Vector3(x * ts, 0f, y * ts);
				var cell = map[x, y];
	
				if (cell.tile == TileType.Water)
				{
					var shoreList = map.GetShores(x, y);
	
					if (shorePrefabsByCode != null && shoreList != null && shoreList.Count > 0)
					{
						// Shoreline water tile → place each required piece
						foreach (var s in shoreList)
						{
							if (!s.present) continue;
							if (s.code < 0 || s.code >= shorePrefabsByCode.Length) continue;
	
							var prefab = shorePrefabsByCode[s.code];
							if (!prefab) continue;
	
							Vector3 piecePos = cellPos + new Vector3(s.offset.x * ts, s.yOffset, s.offset.y * ts);
							var rot = Quaternion.Euler(0f, s.rotationY, 0f);
							Instantiate(prefab, piecePos, rot, root);
						}
					}
					else
					{
						// Regular water (no special shore pieces)
						if (waterPrefab) Instantiate(waterPrefab, cellPos, Quaternion.identity, root);
					}
	
					continue; // done with water tiles
				}
				else
				{
					if (groundPrefab)
					{
						var go = Instantiate(groundPrefab, cellPos, Quaternion.identity, root);
						var rend = go.GetComponentInChildren<Renderer>();
						if (rend)
						{
							rend.sharedMaterial = (cell.biome == BiomeType.Desert) ? sandMaterial : grassMaterial;
						}
						PlaceLegacyDecoration(cell, x, y, cellPos, root);
					}
				}
			}
		}
	
		// Only mark active if we actually built something
		active.Add(chunk);
	}
	
	// Deterministic [0,1) from coords + seed + salt (matches earlier R01 you okayed)
	private static float R01(int x, int y, int seed, uint salt)
	{
		unchecked
		{
			uint h = (uint)x;
			h = (h * 0x9E3779B9u) ^ (uint)y;
			h ^= (uint)seed * 0x85EBCA6Bu;
			h ^= salt;
			h ^= h >> 16; h *= 0x7FEB352Du;
			h ^= h >> 15; h *= 0x846CA68Bu;
			h ^= h >> 16;
			return (h & 0x00FFFFFF) / 16777216f; // 2^24
		}
	}
	
	private static GameObject PickDeterministic(GameObject[] pool, int x, int y, int seed, uint salt)
	{
		if (pool == null || pool.Length == 0) return null;
		int idx = Mathf.FloorToInt(R01(x, y, seed, salt) * pool.Length);
		if (idx >= pool.Length) idx = pool.Length - 1; // safety
		return pool[idx];
	}
	
	private static bool IsOdd(int x, int y) => (((x + y) & 1) == 1);
	
	private void PlaceLegacyDecoration(Cell cell, int x, int y, Vector3 cellPos, Transform parent)
	{
		// Only for land tiles; water handled elsewhere
		if (cell.tile != TileType.Land) return;
	
		int seed = map.seed;
		float value = map.GetFeatureValue(x, y);                 // legacy "value"
		int scaled = Mathf.FloorToInt(value * 10000f);
		bool isOdd = (scaled % 2) == 1;
		float rotY  = R01(x, y, seed, 0x27D4EB2Fu) * 360f;       // deterministic yaw
	
		// FOREST
		if (cell.biome == BiomeType.Forest)
		{
			if (value > 0.695f && value < 0.705f && isOdd && randomDecorationPrefabs != null && randomDecorationPrefabs.Length > 0)
			{
				var prefab = PickDeterministic(randomDecorationPrefabs, x, y, seed, 0x9E3779B9u);
				if (prefab)
				{
					var pos = cellPos + Vector3.up * treeYOffset;
					Instantiate(prefab, pos, Quaternion.Euler(0f, rotY, 0f), parent);
				}
				return;
			}
			if (value > 0.7f && forestTreePrefabs != null && forestTreePrefabs.Length > 0)
			{
				var prefab = PickDeterministic(forestTreePrefabs, x, y, seed, 0xC2B2AE35u);
				if (prefab)
				{
					var pos = cellPos + Vector3.up * treeYOffset;
					Instantiate(prefab, pos, Quaternion.Euler(0f, rotY, 0f), parent);
				}
				return;
			}
			if (value > 0.65f && forestVegetationPrefabs != null && forestVegetationPrefabs.Length > 0)
			{
				var prefab = PickDeterministic(forestVegetationPrefabs, x, y, seed, 0x85EBCA6Bu);
				if (prefab)
				{
					var pos = cellPos + Vector3.up * vegetationYOffset;
					Instantiate(prefab, pos, Quaternion.Euler(0f, rotY, 0f), parent);
				}
				return;
			}
			return;
		}
	
		// DESERT
		if (cell.biome == BiomeType.Desert)
		{
			if (value < desertTreeThreshold && desertTreePrefabs != null && desertTreePrefabs.Length > 0)
			{
				if (isOdd)
				{
					var prefab = PickDeterministic(desertTreePrefabs, x, y, seed, 0x165667B1u);
					if (prefab)
					{
						var pos = cellPos + Vector3.up * treeYOffset;
						Instantiate(prefab, pos, Quaternion.Euler(0f, rotY, 0f), parent);
					}
					return;
				}
			}
			else if (value < desertVegetationThreshold && !isOdd && desertVegetationPrefabs != null && desertVegetationPrefabs.Length > 0)
			{
				var prefab = PickDeterministic(desertVegetationPrefabs, x, y, seed, 0x7F4A7C15u);
				if (prefab)
				{
					var pos = cellPos + Vector3.up * vegetationYOffset;
					Instantiate(prefab, pos, Quaternion.Euler(0f, rotY, 0f), parent);
				}
				return;
			}
		}
	}

    private void DestroyChunk(Vector2Int chunk)
    {
        if (chunkRoots.TryGetValue(chunk, out var root) && root)
        {
            Destroy(root.gameObject);
        }
        chunkRoots.Remove(chunk);
        active.Remove(chunk);
    }
}
