using UnityEngine;
using System.Collections.Generic;

public class PerlinNoiseGenerator : MonoBehaviour
{
	//Important to avoid having floating trees, floating rocks and so on...
	float treeYOffset = -0.1f;
	float rockYOffset = -0.1f;
	
	//How frequent, in terms of Perlin noise, water should appear.
	float waterThreshold = 0.3f;
	
	//Add here the type of possible ground tiles. In the future, if a usable higer ground, such as mountain is to be added
	//here is the place to contain. Since its an enum, never change the order, otherwise this could make previously saved maps
	//to become incompatible with the map generator/editor.
	public enum TileType
	{
		Land,
		Water
	}
	
    public Texture2D GenerateNoiseTexture(int width, int height, NoiseSettings settings)
    {
        Texture2D texture = new Texture2D(width, height);
        float[,] noiseMap = GenerateNoiseMap(width, height, settings);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float value = noiseMap[x, y];
                Color color = new Color(value, value, value);
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        return texture;
    }

    public float[,] GenerateNoiseMap(int width, int height, NoiseSettings settings)
    {
        float[,] noiseMap = new float[width, height];
        System.Random prng = new System.Random(settings.seed);
        Vector2[] octaveOffsets = new Vector2[settings.octaves];

        for (int i = 0; i < settings.octaves; i++)
        {
            float offsetX = prng.Next(-100000, 100000);
            float offsetY = prng.Next(-100000, 100000);
            octaveOffsets[i] = new Vector2(offsetX, offsetY);
        }

        if (settings.scale <= 0)
            settings.scale = 0.0001f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float amplitude = 1;
                float frequency = 1;
                float noiseHeight = 0;

                for (int i = 0; i < settings.octaves; i++)
                {
                    float sampleX = (x / settings.scale) * frequency + octaveOffsets[i].x;
                    float sampleY = (y / settings.scale) * frequency + octaveOffsets[i].y;

                    float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2 - 1;
                    noiseHeight += perlinValue * amplitude;

                    amplitude *= settings.persistence;
                    frequency *= 2;
                }

                noiseMap[x, y] = Mathf.InverseLerp(-1, 1, noiseHeight);
            }
        }

        return noiseMap;
    }
	
	public TileType[,] GenerateLogicalMap(float[,] noiseMap, float waterThreshold)
	{
		int width = noiseMap.GetLength(0);
		int height = noiseMap.GetLength(1);
	
		TileType[,] logicalMap = new TileType[width, height];
	
		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				logicalMap[x, y] = noiseMap[x, y] < waterThreshold ? TileType.Water : TileType.Land;
			}
		}
	
		return logicalMap;
	}
	
	public void GenerateMap(int width, int height, NoiseSettings settings, Transform parent, GameObject groundPrefab, List<GameObject> treePrefabs, List<GameObject> rockPrefabs, GameObject waterPrefab, GameObject shoreSidePrefab, GameObject shoreCornerPrefab, GameObject shoreTinyCornerPrefab, GameObject shorePocketPrefab, GameObject shoreCornerExtendedPrefab, GameObject shoreDoubleSidePrefab, GameObject shoreDoubleTinyCornerPrefab, GameObject pondPrefab, GameObject shoreSideDoubleTinyCornerPrefab, Material outlineMaterial)
	{
		float[,] noiseMap = GenerateNoiseMap(width, height, settings);
		TileType[,] logicalMap = GenerateLogicalMap(noiseMap, waterThreshold);
	
		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				Vector3 position = new Vector3(x, 0, y);
				TileType type = logicalMap[x, y];
	
				//The code above deals with ground (grass) part of the map
				if (type == TileType.Land)
				{
					GameObject.Instantiate(groundPrefab, position, Quaternion.identity, parent);
			
					// Tree/rock decoration
					float value = noiseMap[x, y];
			
					if (value > 0.7f)
					{
						GameObject tree = treePrefabs[Random.Range(0, treePrefabs.Count)];
						Quaternion rot = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
						GameObject treeInstance = Instantiate(tree, position + Vector3.up * treeYOffset, rot, parent);
						
						AddOutline(treeInstance, outlineMaterial);
					}
					else if (value > 0.65f)
					{
						GameObject rock = rockPrefabs[Random.Range(0, rockPrefabs.Count)];
						Quaternion rot = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
						GameObject rockInstance = Instantiate(rock, position + Vector3.up * rockYOffset, rot, parent);
						
						AddOutline(rockInstance, outlineMaterial);
					}
				}
				//The code above deals with the nightmarish water part of the map
				else if (type == TileType.Water)
				{
					bool landN = IsLand(logicalMap, x, y + 1);
					bool landE = IsLand(logicalMap, x + 1, y);
					bool landS = IsLand(logicalMap, x, y - 1);
					bool landW = IsLand(logicalMap, x - 1, y);
					bool landNE = IsLand(logicalMap, x + 1, y + 1);
					bool landSE = IsLand(logicalMap, x + 1, y - 1);
					bool landSW = IsLand(logicalMap, x - 1, y - 1);
					bool landNW = IsLand(logicalMap, x - 1, y + 1);
					
					bool landOrOutOfBoundsN = IsLandOrOutOfBounds(logicalMap, x, y + 1);
					bool landOrOutOfBoundsE = IsLandOrOutOfBounds(logicalMap, x + 1, y);
					bool landOrOutOfBoundsS = IsLandOrOutOfBounds(logicalMap, x, y - 1);
					bool landOrOutOfBoundsW = IsLandOrOutOfBounds(logicalMap, x - 1, y);
					
					bool waterN = IsWater(logicalMap, x, y + 1);
					bool waterE = IsWater(logicalMap, x + 1, y);
					bool waterS = IsWater(logicalMap, x, y - 1);
					bool waterW = IsWater(logicalMap, x - 1, y);
					
					int waterCount = 0;
					if (waterN) waterCount++;
					if (waterE) waterCount++;
					if (waterS) waterCount++;
					if (waterW) waterCount++;
					
					//U-shaped shore
					if (waterCount == 1 && waterN && IsInBounds(logicalMap, x - 1, y) && IsInBounds(logicalMap, x + 1, y))
					{
						GameObject.Instantiate(shorePocketPrefab, position, Quaternion.Euler(0, 180, 0), parent); // North = bottom land
					}
					else if (waterCount == 1 && waterE && IsInBounds(logicalMap, x, y - 1) && IsInBounds(logicalMap, x, y + 1))
					{
						GameObject.Instantiate(shorePocketPrefab, position, Quaternion.Euler(0, 270, 0), parent); // East = left land
					}
					else if (waterCount == 1 && waterS && IsInBounds(logicalMap, x - 1, y) && IsInBounds(logicalMap, x + 1, y))
					{
						GameObject.Instantiate(shorePocketPrefab, position, Quaternion.Euler(0, 0, 0), parent);   // South = top land
					}
					else if (waterCount == 1 && waterW && IsInBounds(logicalMap, x, y - 1) && IsInBounds(logicalMap, x, y + 1))
					{
						GameObject.Instantiate(shorePocketPrefab, position, Quaternion.Euler(0, 90, 0), parent);  // West = right land
					}
					//Pond tile (small lake)
					else if (landOrOutOfBoundsN && landOrOutOfBoundsE && landOrOutOfBoundsS && landOrOutOfBoundsW)
					{
						GameObject.Instantiate(pondPrefab, position, Quaternion.identity, parent);
					}
					// Double-side shore (water on opposite sides, land on others)
					else if (IsWater(logicalMap, x, y + 1) && IsWater(logicalMap, x, y - 1) && IsLand(logicalMap, x - 1, y) && IsLand(logicalMap, x + 1, y))
					{
						// Vertical water channel
						GameObject.Instantiate(shoreDoubleSidePrefab, position, Quaternion.Euler(0, 0, 0), parent);
					}
					else if (IsWater(logicalMap, x - 1, y) && IsWater(logicalMap, x + 1, y) && IsLand(logicalMap, x, y + 1) && IsLand(logicalMap, x, y - 1))
					{
						// Horizontal water channel
						GameObject.Instantiate(shoreDoubleSidePrefab, position, Quaternion.Euler(0, 90, 0), parent);
					}
					// Corner shore placement (2 adjacent land tiles)
					else if (landN && landE)
					{
						if (landSW)
							GameObject.Instantiate(shoreCornerExtendedPrefab, position, Quaternion.Euler(0, 90, 0), parent);
						else
							GameObject.Instantiate(shoreCornerPrefab, position, Quaternion.Euler(0, 90, 0), parent);
					}
					else if (landE && landS)
					{
						if (landNW)
							GameObject.Instantiate(shoreCornerExtendedPrefab, position, Quaternion.Euler(0, 180, 0), parent);
						else
							GameObject.Instantiate(shoreCornerPrefab, position, Quaternion.Euler(0, 180, 0), parent);
					}
					else if (landS && landW)
					{
						if (landNE)
							GameObject.Instantiate(shoreCornerExtendedPrefab, position, Quaternion.Euler(0, 270, 0), parent);
						else
							GameObject.Instantiate(shoreCornerPrefab, position, Quaternion.Euler(0, 270, 0), parent);
					}
					else if (landW && landN)
					{
						if (landSE)
							GameObject.Instantiate(shoreCornerExtendedPrefab, position, Quaternion.Euler(0, 0, 0), parent);
						else
							GameObject.Instantiate(shoreCornerPrefab, position, Quaternion.Euler(0, 0, 0), parent);
					}
					//Double tiny shore tile
					else if (landSW && landSE && !landN && !landE && !landW && !landS)
					{
						GameObject.Instantiate(shoreDoubleTinyCornerPrefab, position, Quaternion.Euler(0, 0, 0), parent);
						if(landNE)
						{
							GameObject.Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 180, 0), parent);
						}
						if(landNW)
						{
							GameObject.Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 90, 0), parent);
						}
					}
					else if (landNW && landSW && !landN && !landE && !landS && !landW)
					{
						GameObject.Instantiate(shoreDoubleTinyCornerPrefab, position, Quaternion.Euler(0, 90, 0), parent);
						if(landSE)
						{
							GameObject.Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 270, 0), parent);
						}
						if(landNE)
						{
							GameObject.Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 180, 0), parent);
						}
					}
					else if (landNW && landNE && !landS && !landE && !landW && !landN)
					{
						GameObject.Instantiate(shoreDoubleTinyCornerPrefab, position, Quaternion.Euler(0, 180, 0), parent);
						if(landSW)
						{
							GameObject.Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 0, 0), parent);
						}
						if(landSE)
						{
							GameObject.Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 270, 0), parent);
						}
					}
					else if (landNE && landSE && !landN && !landS && !landW && !landE)
					{
						GameObject.Instantiate(shoreDoubleTinyCornerPrefab, position, Quaternion.Euler(0, 270, 0), parent);
						if(landNW)
						{
							GameObject.Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 90, 0), parent);
						}
						if(landSW)
						{
							GameObject.Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 0, 0), parent);
						}
					}
					//Tiny shore tile
					else if (landNE && !landN && !landE && !landS && !landW)
					{
						GameObject.Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 180, 0), parent);
						if(landSW)
						{
							GameObject.Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 0, 0), parent);
						}
					}
					else if (landSE && !landS && !landE && !landN && !landW)
					{
						GameObject.Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 270, 0), parent);
						if(landNW)
						{
							GameObject.Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 90, 0), parent);
						}
					}
					else if (landSW && !landS && !landW && !landN && !landE)
					{
						GameObject.Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 0, 0), parent);
						if(landNE)
						{
							GameObject.Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 180, 0), parent);
						}
					}
					else if (landNW && !landN && !landW && !landS && !landE)
					{
						GameObject.Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 90, 0), parent);
						if(landSE)
						{
							GameObject.Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 270, 0), parent);
						}
					}
					// Side shore logic (single land tile + potential diagonal tips). NOTE: Single diagonal tips don't exist in Kenneys Nature Kit, so ideally this should be modeled (by merging the prefabs as stated in the code)
					else if (landN)
					{
					
						if (landSW && landSE && waterS)
						{
							Instantiate(shoreSideDoubleTinyCornerPrefab, position, Quaternion.Euler(0, 0, 0), parent);
						}
						else
						{
							GameObject side = Instantiate(shoreSidePrefab, position, Quaternion.Euler(0, 0, 0), parent);
					
							if (landSW && waterS && !landSE)
							{
								Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 0, 0), side.transform);
							}
							else if (landSE && waterS && !landSW)
							{
								Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 270, 0), side.transform);
							}
						}
					}
					else if (landE)
					{			
						if (landNW && landSW && waterW)
						{
							Instantiate(shoreSideDoubleTinyCornerPrefab, position, Quaternion.Euler(0, 90, 0), parent);
						}
						else
						{
							GameObject side = Instantiate(shoreSidePrefab, position, Quaternion.Euler(0, 90, 0), parent);
					
							if (landNW && waterW && !landSW)
								Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 90, 0), side.transform);
							else if (landSW && waterW && !landNW)
								Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 0, 0), side.transform);
						}
					}
					else if (landS)
					{
						if (landNW && landNE && waterN)
						{
							Instantiate(shoreSideDoubleTinyCornerPrefab, position, Quaternion.Euler(0, 180, 0), parent);
						}
						else
						{
							GameObject side = Instantiate(shoreSidePrefab, position, Quaternion.Euler(0, 180, 0), parent);
					
							if (landNW && waterN && !landNE)
								Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 90, 0), side.transform);
							else if (landNE && waterN && !landNW)
								Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 180, 0), side.transform);
						}
					}
					else if (landW)
					{
						if (landNE && landSE && waterE)
						{
							Instantiate(shoreSideDoubleTinyCornerPrefab, position, Quaternion.Euler(0, 270, 0), parent);
						}
						else
						{
							GameObject side = Instantiate(shoreSidePrefab, position, Quaternion.Euler(0, 270, 0), parent);
					
							if (landNE && waterE && !landSE)
								Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 180, 0), side.transform);
							else if (landSE && waterE && !landNE)
								Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 270, 0), side.transform);
						}
					}
					else
					{
						GameObject.Instantiate(waterPrefab, position, Quaternion.identity, parent);
					}
				}
			}
		}
	}
	
	private bool IsLand(TileType[,] map, int x, int y)
	{
		int width = map.GetLength(0);
		int height = map.GetLength(1);
		if (x < 0 || x >= width || y < 0 || y >= height)
			return false;
		return map[x, y] == TileType.Land;
	}
	
	private bool IsWater(TileType[,] map, int x, int y)
	{
		int width = map.GetLength(0);
		int height = map.GetLength(1);
	
		if (x < 0 || x >= width || y < 0 || y >= height)
			return false;
	
		return map[x, y] == TileType.Water;
	}
	
	private bool IsInBounds(TileType[,] map, int x, int y)
	{
		int width = map.GetLength(0);
		int height = map.GetLength(1);
		return x >= 0 && x < width && y >= 0 && y < height;
	}
	
	bool IsLandOrOutOfBounds(TileType[,] map, int x, int y)
	{
		int width = map.GetLength(0);
		int height = map.GetLength(1);
		if (x < 0 || x >= width || y < 0 || y >= height)
			return true;
		return map[x, y] == TileType.Land;
	}
	
	private void AddOutline(GameObject original, Material outlineMaterial)
	{
		if (outlineMaterial == null) return;
	
		// Duplicate the object as a child
		GameObject outline = Instantiate(original, original.transform.position, original.transform.rotation, original.transform);
		outline.name = original.name + "_Outline";
	
		// Prevent recursive behavior if this script is attached to the prefab
		DestroyImmediate(outline.GetComponent<PerlinNoiseGenerator>());
	
		// Go through all renderers and replace each material with the outlineMaterial
		var renderers = outline.GetComponentsInChildren<MeshRenderer>();
		foreach (var r in renderers)
		{
			// Disable shadow casting/receiving to avoid dark blobs
			r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			r.receiveShadows = false;
	
			// Replace all materials with the outline material
			var mats = r.sharedMaterials;
			for (int i = 0; i < mats.Length; i++)
			{
				mats[i] = outlineMaterial;
			}
			r.sharedMaterials = mats;
		}
	}
}
