using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

public enum BiomeType { Forest = 0, Desert = 1 }

public enum TileType { Land = 0, Water = 1}

public class PerlinNoiseGenerator : MonoBehaviour
{
	//Important to avoid having floating trees, floating rocks and so on...
	float treeYOffset = -0.1f;
	float vegetationYOffset = -0.05f;
	
	//How frequent, in terms of Perlin noise, water should appear.
	float waterThreshold = 0.3f;
	
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
	
	private BiomeType[,] BuildBiomeMap(
    int width, int height, NoiseSettings settings, TileType[,] logicalMap,
    bool enableForest, bool enableDesert)
	{	
		// Tunables
		const int DESERT_WATER_BUFFER_TILES = 2; // 1 = immediate neighbors (incl. diagonals); set 2 for a thicker ring
	
		var tempSettings  = new NoiseSettings { seed = settings.seed + 10011, scale = Mathf.Max(28f, settings.scale * 0.8f), octaves = 3, persistence = 0.55f };
		var moistSettings = new NoiseSettings { seed = settings.seed + 20021, scale = Mathf.Max(28f, settings.scale * 0.8f), octaves = 3, persistence = 0.55f };
	
		float[,] temp  = GenerateNoiseMap(width, height, tempSettings);
		float[,] moist = GenerateNoiseMap(width, height, moistSettings);
		int[,] wdist   = ComputeWaterDistance(logicalMap, eightNeighbors: true); // ⬅️ diagonals count
	
		var biome = new BiomeType[width, height];
		
		//Be carefull here when adding new biomes to the world generator, to avoid breaking the logic
		if (!enableForest && enableDesert)
		{
			for (int x = 0; x < width; x++)
			for (int y = 0; y < height; y++)
			{
				if (logicalMap[x, y] == TileType.Water)
				{
					biome[x, y] = BiomeType.Forest; // Water stays forest for shoreline system
				}
				else
				{
					biome[x, y] = BiomeType.Desert;
				}
			}
			return biome;
		}
	
		for (int x = 0; x < width; x++)
		for (int y = 0; y < height; y++)
		{
			if (logicalMap[x, y] == TileType.Water)
			{
				biome[x, y] = BiomeType.Forest; // water “belongs” to Forest for shoreline palette
				continue;
			}
	
			// Force a Forest ring around any water (incl. diagonals)
			if (wdist[x, y] <= DESERT_WATER_BUFFER_TILES)
			{
				biome[x, y] = BiomeType.Forest;
				continue;
			}
	
			// Hot & dry → Desert (relaxed formula)
			float dryness = temp[x, y] * (1f - moist[x, y]);
			bool desertCandidate = (dryness > 0.42f) || (temp[x, y] > 0.58f && moist[x, y] < 0.48f);
	
			biome[x, y] = (desertCandidate && enableDesert) ? BiomeType.Desert : BiomeType.Forest;
		}
	
		return biome;
	}
	
	public void GenerateMap(int width, int height, NoiseSettings settings, Transform parent, GameObject groundPrefab, List<GameObject> forestTreePrefabs, List<GameObject> forestVegetationPrefabs, GameObject waterPrefab, GameObject shoreSidePrefab, GameObject shoreCornerPrefab, GameObject shoreTinyCornerPrefab, GameObject shorePocketPrefab, GameObject shoreCornerExtendedPrefab, GameObject shoreDoubleSidePrefab, GameObject shoreDoubleTinyCornerPrefab, GameObject pondPrefab, GameObject shoreSideDoubleTinyCornerPrefab, bool enableForest, bool enableDesert, Material sandMaterial, GameObject biomeOverlayPrefab, Material grassToSandMat, Material sandToGrassMat, List<GameObject> desertTreePrefabs, List<GameObject> desertVegetationPrefabs, Material grassMaterial, bool isRareWater, List<GameObject> randomDecorationPrefabs)
	{
		NoiseSettings terrainSettings = new NoiseSettings
		{
			seed = settings.seed + 1,
			scale = Mathf.Max(35f, settings.scale),
			octaves = 3,
			persistence = Mathf.Clamp(settings.persistence, 0.4f, 0.75f)
		};
		
		waterThreshold = isRareWater ? 0.1f : 0.3f;

		float[,] terrainNoiseMap = GenerateNoiseMap(width, height, terrainSettings);
		TileType[,] logicalMap = GenerateLogicalMap(terrainNoiseMap, waterThreshold);
		BiomeType[,] biomeMap = BuildBiomeMap(width, height, settings, logicalMap, enableForest, enableDesert);
		float desertTreeThreshold = 0.05f;
		float desertVegetationThreshold = 0.1f;

		//This line serves to adjust probability of finding palms and cactus in desert when desert is not a commom biome, so it doesn't look too empty
		if (enableDesert && enableForest)
		{
			desertTreeThreshold += 0.1f;
			desertVegetationThreshold += 0.1f;
		}
		
		float[,] decorationNoiseMap = GenerateNoiseMap(width, height, settings);

	
		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				Vector3 position = new Vector3(x, 0, y);
				TileType type = logicalMap[x, y];
	
				//The code above deals with ground part of the map
				if (type == TileType.Land)
				{
					var groundInstance = GameObject.Instantiate(groundPrefab, position, Quaternion.identity, parent);
					
					//Here we check which biome the land tile is (default is forest)
					if (biomeMap[x, y] == BiomeType.Desert && sandMaterial != null)
					{
						OverrideAllMaterials(groundInstance, sandMaterial);
					}
					
			
					// map decoration
					float value = decorationNoiseMap[x, y];
					int scaled = Mathf.FloorToInt(value * 10000);
					bool isOdd = scaled % 2 == 1;
					
					if(biomeMap[x, y] == BiomeType.Forest)
					{
						if(value > 0.695f && value < 0.705f && isOdd)
						{
							GameObject prefab = randomDecorationPrefabs[Random.Range(0, randomDecorationPrefabs.Count)];
							Quaternion rot = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
							GameObject decorationInstance = Instantiate(prefab, position + Vector3.up * treeYOffset, rot, parent);
							
							//AddOutline(decorationInstance, outlineMaterial);
						}
						else if (value > 0.7f && forestTreePrefabs.Count > 0)
						{
							GameObject prefab = forestTreePrefabs[Random.Range(0, forestTreePrefabs.Count)];
							Quaternion rot = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
							GameObject treeInstance = Instantiate(prefab, position + Vector3.up * treeYOffset, rot, parent);
							
							//AddOutline(treeInstance, outlineMaterial);
						}
						else if (value > 0.65f && forestVegetationPrefabs.Count > 0)
						{
							GameObject prefab = forestVegetationPrefabs[Random.Range(0, forestVegetationPrefabs.Count)];
							Quaternion rot = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
							GameObject vegetationInstance = Instantiate(prefab, position + Vector3.up * vegetationYOffset, rot, parent);
							
							//AddOutline(vegetationInstance, outlineMaterial);
						}
					}
					else if (biomeMap[x, y] == BiomeType.Desert)
					{	
						if (value < desertTreeThreshold && desertTreePrefabs.Count > 0)
						{
							if(isOdd)
							{
								GameObject prefab = desertTreePrefabs[Random.Range(0, desertTreePrefabs.Count)];
								Quaternion rot = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
								GameObject treeInstance = Instantiate(prefab, position + Vector3.up * treeYOffset, rot, parent);
								
								//AddOutline(treeInstance, outlineMaterial);
							}
						}
						else if (value < desertVegetationThreshold && !isOdd && desertVegetationPrefabs.Count > 0)
						{
							GameObject prefab = desertVegetationPrefabs[Random.Range(0, desertVegetationPrefabs.Count)];
							Quaternion rot = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
							GameObject vegetationInstance = Instantiate(prefab, position + Vector3.up * vegetationYOffset, rot, parent);
							
							//AddOutline(vegetationInstance, outlineMaterial);
						}
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
						GameObject _tmp_shorePocketPrefab = GameObject.Instantiate(shorePocketPrefab, position, Quaternion.Euler(0, 180, 0), parent);
						SwapShoreMaterialIfNeeded(_tmp_shorePocketPrefab, enableForest, enableDesert, grassMaterial, sandMaterial); // North = bottom land
					}
					else if (waterCount == 1 && waterE && IsInBounds(logicalMap, x, y - 1) && IsInBounds(logicalMap, x, y + 1))
					{
						GameObject _tmp_shorePocketPrefab = GameObject.Instantiate(shorePocketPrefab, position, Quaternion.Euler(0, 270, 0), parent);
						SwapShoreMaterialIfNeeded(_tmp_shorePocketPrefab, enableForest, enableDesert, grassMaterial, sandMaterial); // East = left land
					}
					else if (waterCount == 1 && waterS && IsInBounds(logicalMap, x - 1, y) && IsInBounds(logicalMap, x + 1, y))
					{
						GameObject _tmp_shorePocketPrefab = GameObject.Instantiate(shorePocketPrefab, position, Quaternion.Euler(0, 0, 0), parent);
						SwapShoreMaterialIfNeeded(_tmp_shorePocketPrefab, enableForest, enableDesert, grassMaterial, sandMaterial);   // South = top land
					}
					else if (waterCount == 1 && waterW && IsInBounds(logicalMap, x, y - 1) && IsInBounds(logicalMap, x, y + 1))
					{
						GameObject _tmp_shorePocketPrefab = GameObject.Instantiate(shorePocketPrefab, position, Quaternion.Euler(0, 90, 0), parent);
						SwapShoreMaterialIfNeeded(_tmp_shorePocketPrefab, enableForest, enableDesert, grassMaterial, sandMaterial);  // West = right land
					}
					//Pond tile (small lake)
					else if (landOrOutOfBoundsN && landOrOutOfBoundsE && landOrOutOfBoundsS && landOrOutOfBoundsW)
					{
						GameObject _tmp_pond = GameObject.Instantiate(pondPrefab, position, Quaternion.identity, parent);
						SwapShoreMaterialIfNeeded(_tmp_pond, enableForest, enableDesert, grassMaterial, sandMaterial);
					}
					// Double-side shore (water on opposite sides, land on others)
					else if (IsWater(logicalMap, x, y + 1) && IsWater(logicalMap, x, y - 1) && IsLand(logicalMap, x - 1, y) && IsLand(logicalMap, x + 1, y))
					{
						// Vertical water channel
						GameObject _tmp_shoreDoubleSidePrefab = GameObject.Instantiate(shoreDoubleSidePrefab, position, Quaternion.Euler(0, 0, 0), parent);
						SwapShoreMaterialIfNeeded(_tmp_shoreDoubleSidePrefab, enableForest, enableDesert, grassMaterial, sandMaterial);
					}
					else if (IsWater(logicalMap, x - 1, y) && IsWater(logicalMap, x + 1, y) && IsLand(logicalMap, x, y + 1) && IsLand(logicalMap, x, y - 1))
					{
						// Horizontal water channel
						GameObject _tmp_shoreDoubleSidePrefab = GameObject.Instantiate(shoreDoubleSidePrefab, position, Quaternion.Euler(0, 90, 0), parent);
						SwapShoreMaterialIfNeeded(_tmp_shoreDoubleSidePrefab, enableForest, enableDesert, grassMaterial, sandMaterial);
					}
					// Corner shore placement (2 adjacent land tiles)
					else if (landN && landE)
					{
						if (landSW)
						{
							GameObject _tmp_shoreCornerExtendedPrefab = GameObject.Instantiate(shoreCornerExtendedPrefab, position, Quaternion.Euler(0, 90, 0), parent);
							SwapShoreMaterialIfNeeded(_tmp_shoreCornerExtendedPrefab, enableForest, enableDesert, grassMaterial, sandMaterial);
						}
						else{
							GameObject _tmp_shoreCornerPrefab = GameObject.Instantiate(shoreCornerPrefab, position, Quaternion.Euler(0, 90, 0), parent);
							SwapShoreMaterialIfNeeded(_tmp_shoreCornerPrefab, enableForest, enableDesert, grassMaterial, sandMaterial);
						}
					}
					else if (landE && landS)
					{
						if (landNW)
						{
							GameObject _tmp_shoreCornerExtendedPrefab = GameObject.Instantiate(shoreCornerExtendedPrefab, position, Quaternion.Euler(0, 180, 0), parent);
							SwapShoreMaterialIfNeeded(_tmp_shoreCornerExtendedPrefab, enableForest, enableDesert, grassMaterial, sandMaterial);
						}
						else
						{
							GameObject _tmp_shoreCornerPrefab = GameObject.Instantiate(shoreCornerPrefab, position, Quaternion.Euler(0, 180, 0), parent);
							SwapShoreMaterialIfNeeded(_tmp_shoreCornerPrefab, enableForest, enableDesert, grassMaterial, sandMaterial);
						}
					}
					else if (landS && landW)
					{
						if (landNE)
						{
							GameObject _tmp_shoreCornerExtendedPrefab = GameObject.Instantiate(shoreCornerExtendedPrefab, position, Quaternion.Euler(0, 270, 0), parent);
							SwapShoreMaterialIfNeeded(_tmp_shoreCornerExtendedPrefab, enableForest, enableDesert, grassMaterial, sandMaterial);
						}
						else
						{
							GameObject _tmp_shoreCornerPrefab = GameObject.Instantiate(shoreCornerPrefab, position, Quaternion.Euler(0, 270, 0), parent);
							SwapShoreMaterialIfNeeded(_tmp_shoreCornerPrefab, enableForest, enableDesert, grassMaterial, sandMaterial);
						}
					}
					else if (landW && landN)
					{
						if (landSE)
						{
							GameObject _tmp_shoreCornerExtendedPrefab = GameObject.Instantiate(shoreCornerExtendedPrefab, position, Quaternion.Euler(0, 0, 0), parent);
							SwapShoreMaterialIfNeeded(_tmp_shoreCornerExtendedPrefab, enableForest, enableDesert, grassMaterial, sandMaterial);
						}
						else
						{
							GameObject _tmp_shoreCornerPrefab = GameObject.Instantiate(shoreCornerPrefab, position, Quaternion.Euler(0, 0, 0), parent);
							SwapShoreMaterialIfNeeded(_tmp_shoreCornerPrefab, enableForest, enableDesert, grassMaterial, sandMaterial);
						}
					}
					//Double tiny shore tile
					else if (landSW && landSE && !landN && !landE && !landW && !landS)
					{
						GameObject _tmp_shoreDoubleTinyCornerPrefab = GameObject.Instantiate(shoreDoubleTinyCornerPrefab, position, Quaternion.Euler(0, 0, 0), parent);
						SwapShoreMaterialIfNeeded(_tmp_shoreDoubleTinyCornerPrefab, enableForest, enableDesert, grassMaterial, sandMaterial);
						if(landNE)
						{
							GameObject _tmp_shoreTinyCornerPrefab = GameObject.Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 180, 0), parent);
							SwapShoreMaterialIfNeeded(_tmp_shoreTinyCornerPrefab, enableForest, enableDesert, grassMaterial, sandMaterial);
						}
						if(landNW)
						{
							GameObject _tmp_shoreTinyCornerPrefab = GameObject.Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 90, 0), parent);
							SwapShoreMaterialIfNeeded(_tmp_shoreTinyCornerPrefab, enableForest, enableDesert, grassMaterial, sandMaterial);
						}
					}
					else if (landNW && landSW && !landN && !landE && !landS && !landW)
					{
						GameObject _tmp_shoreDoubleTinyCornerPrefab = GameObject.Instantiate(shoreDoubleTinyCornerPrefab, position, Quaternion.Euler(0, 90, 0), parent);
						SwapShoreMaterialIfNeeded(_tmp_shoreDoubleTinyCornerPrefab, enableForest, enableDesert, grassMaterial, sandMaterial);
						if(landSE)
						{
							GameObject _tmp_shoreTinyCornerPrefab = GameObject.Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 270, 0), parent);
							SwapShoreMaterialIfNeeded(_tmp_shoreTinyCornerPrefab, enableForest, enableDesert, grassMaterial, sandMaterial);
						}
						if(landNE)
						{
							GameObject _tmp_shoreTinyCornerPrefab = GameObject.Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 180, 0), parent);
							SwapShoreMaterialIfNeeded(_tmp_shoreTinyCornerPrefab, enableForest, enableDesert, grassMaterial, sandMaterial);
						}
					}
					else if (landNW && landNE && !landS && !landE && !landW && !landN)
					{
						GameObject _tmp_shoreDoubleTinyCornerPrefab = GameObject.Instantiate(shoreDoubleTinyCornerPrefab, position, Quaternion.Euler(0, 180, 0), parent);
						SwapShoreMaterialIfNeeded(_tmp_shoreDoubleTinyCornerPrefab, enableForest, enableDesert, grassMaterial, sandMaterial);
						if(landSW)
						{
							GameObject _tmp_shoreTinyCornerPrefab = GameObject.Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 0, 0), parent);
							SwapShoreMaterialIfNeeded(_tmp_shoreTinyCornerPrefab, enableForest, enableDesert, grassMaterial, sandMaterial);
						}
						if(landSE)
						{
							GameObject _tmp_shoreTinyCornerPrefab = GameObject.Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 270, 0), parent);
							SwapShoreMaterialIfNeeded(_tmp_shoreTinyCornerPrefab, enableForest, enableDesert, grassMaterial, sandMaterial);
						}
					}
					else if (landNE && landSE && !landN && !landS && !landW && !landE)
					{
						GameObject _tmp_shoreDoubleTinyCornerPrefab = GameObject.Instantiate(shoreDoubleTinyCornerPrefab, position, Quaternion.Euler(0, 270, 0), parent);
						SwapShoreMaterialIfNeeded(_tmp_shoreDoubleTinyCornerPrefab, enableForest, enableDesert, grassMaterial, sandMaterial);
						if(landNW)
						{
							GameObject _tmp_shoreTinyCornerPrefab = GameObject.Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 90, 0), parent);
							SwapShoreMaterialIfNeeded(_tmp_shoreTinyCornerPrefab, enableForest, enableDesert, grassMaterial, sandMaterial);
						}
						if(landSW)
						{
							GameObject _tmp_shoreTinyCornerPrefab = GameObject.Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 0, 0), parent);
							SwapShoreMaterialIfNeeded(_tmp_shoreTinyCornerPrefab, enableForest, enableDesert, grassMaterial, sandMaterial);
						}
					}
					//Tiny shore tile
					else if (landNE && !landN && !landE && !landS && !landW)
					{
						GameObject _tmp_shoreTinyCornerPrefab = GameObject.Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 180, 0), parent);
						SwapShoreMaterialIfNeeded(_tmp_shoreTinyCornerPrefab, enableForest, enableDesert, grassMaterial, sandMaterial);
						if(landSW)
						{
							GameObject _tmp_shoreTinyCornerPrefab2 = GameObject.Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 0, 0), parent);
							SwapShoreMaterialIfNeeded(_tmp_shoreTinyCornerPrefab2, enableForest, enableDesert, grassMaterial, sandMaterial);
						}
					}
					else if (landSE && !landS && !landE && !landN && !landW)
					{
						GameObject _tmp_shoreTinyCornerPrefab = GameObject.Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 270, 0), parent);
						SwapShoreMaterialIfNeeded(_tmp_shoreTinyCornerPrefab, enableForest, enableDesert, grassMaterial, sandMaterial);
						if(landNW)
						{
							GameObject _tmp_shoreTinyCornerPrefab2 = GameObject.Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 90, 0), parent);
							SwapShoreMaterialIfNeeded(_tmp_shoreTinyCornerPrefab2, enableForest, enableDesert, grassMaterial, sandMaterial);
						}
					}
					else if (landSW && !landS && !landW && !landN && !landE)
					{
						GameObject _tmp_shoreTinyCornerPrefab = GameObject.Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 0, 0), parent);
						SwapShoreMaterialIfNeeded(_tmp_shoreTinyCornerPrefab, enableForest, enableDesert, grassMaterial, sandMaterial);
						if(landNE)
						{
							GameObject _tmp_shoreTinyCornerPrefab2 = GameObject.Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 180, 0), parent);
							SwapShoreMaterialIfNeeded(_tmp_shoreTinyCornerPrefab2, enableForest, enableDesert, grassMaterial, sandMaterial);
						}
					}
					else if (landNW && !landN && !landW && !landS && !landE)
					{
						GameObject _tmp_shoreTinyCornerPrefab = GameObject.Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 90, 0), parent);
						SwapShoreMaterialIfNeeded(_tmp_shoreTinyCornerPrefab, enableForest, enableDesert, grassMaterial, sandMaterial);
						if(landSE)
						{
							GameObject _tmp_shoreTinyCornerPrefab2 = GameObject.Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 270, 0), parent);
							SwapShoreMaterialIfNeeded(_tmp_shoreTinyCornerPrefab2, enableForest, enableDesert, grassMaterial, sandMaterial);
						}
					}
					// Side shore logic (single land tile + potential diagonal tips). NOTE: Single diagonal tips don't exist in Kenneys Nature Kit, so ideally this should be modeled (by merging the prefabs as stated in the code)
					else if (landN)
					{
					
						if (landSW && landSE && waterS)
						{
							GameObject _tmp_shoreSideDoubleTinyCornerPrefab = Instantiate(shoreSideDoubleTinyCornerPrefab, position, Quaternion.Euler(0, 0, 0), parent);
							SwapShoreMaterialIfNeeded(_tmp_shoreSideDoubleTinyCornerPrefab, enableForest, enableDesert, grassMaterial, sandMaterial);
						}
						else
						{
							GameObject side = Instantiate(shoreSidePrefab, position, Quaternion.Euler(0, 0, 0), parent);
							SwapShoreMaterialIfNeeded(side, enableForest, enableDesert, grassMaterial, sandMaterial);
					
							if (landSW && waterS && !landSE)
							{
								GameObject _tmp_shoreTinyCornerPrefab = Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 0, 0), side.transform);
								SwapShoreMaterialIfNeeded(_tmp_shoreTinyCornerPrefab, enableForest, enableDesert, grassMaterial, sandMaterial);
							}
							else if (landSE && waterS && !landSW)
							{
								GameObject _tmp_shoreTinyCornerPrefab = Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 270, 0), side.transform);
								SwapShoreMaterialIfNeeded(_tmp_shoreTinyCornerPrefab, enableForest, enableDesert, grassMaterial, sandMaterial);
							}
						}
					}
					else if (landE)
					{			
						if (landNW && landSW && waterW)
						{
							GameObject _tmp_shoreSideDoubleTinyCornerPrefab = Instantiate(shoreSideDoubleTinyCornerPrefab, position, Quaternion.Euler(0, 90, 0), parent);
							SwapShoreMaterialIfNeeded(_tmp_shoreSideDoubleTinyCornerPrefab, enableForest, enableDesert, grassMaterial, sandMaterial);
						}
						else
						{
							GameObject side = Instantiate(shoreSidePrefab, position, Quaternion.Euler(0, 90, 0), parent);
							SwapShoreMaterialIfNeeded(side, enableForest, enableDesert, grassMaterial, sandMaterial);
					
							if (landNW && waterW && !landSW)
							{
								GameObject _tmp_shoreTinyCornerPrefab = Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 90, 0), side.transform);
								SwapShoreMaterialIfNeeded(_tmp_shoreTinyCornerPrefab, enableForest, enableDesert, grassMaterial, sandMaterial);
							}
							else if (landSW && waterW && !landNW)
							{
								GameObject _tmp_shoreTinyCornerPrefab = Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 0, 0), side.transform);
								SwapShoreMaterialIfNeeded(_tmp_shoreTinyCornerPrefab, enableForest, enableDesert, grassMaterial, sandMaterial);
							}
						}
					}
					else if (landS)
					{
						if (landNW && landNE && waterN)
						{
							GameObject _tmp_shoreSideDoubleTinyCornerPrefab = Instantiate(shoreSideDoubleTinyCornerPrefab, position, Quaternion.Euler(0, 180, 0), parent);
							SwapShoreMaterialIfNeeded(_tmp_shoreSideDoubleTinyCornerPrefab, enableForest, enableDesert, grassMaterial, sandMaterial);
						}
						else
						{
							GameObject side = Instantiate(shoreSidePrefab, position, Quaternion.Euler(0, 180, 0), parent);
							SwapShoreMaterialIfNeeded(side, enableForest, enableDesert, grassMaterial, sandMaterial);
					
							if (landNW && waterN && !landNE)
							{
								GameObject _tmp_shoreTinyCornerPrefab = Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 90, 0), side.transform);
								SwapShoreMaterialIfNeeded(_tmp_shoreTinyCornerPrefab, enableForest, enableDesert, grassMaterial, sandMaterial);
							}
							else if (landNE && waterN && !landNW)
							{
								GameObject _tmp_shoreTinyCornerPrefab = Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 180, 0), side.transform);
								SwapShoreMaterialIfNeeded(_tmp_shoreTinyCornerPrefab, enableForest, enableDesert, grassMaterial, sandMaterial);
							}
						}
					}
					else if (landW)
					{
						if (landNE && landSE && waterE)
						{
							GameObject _tmp_shoreSideDoubleTinyCornerPrefab = Instantiate(shoreSideDoubleTinyCornerPrefab, position, Quaternion.Euler(0, 270, 0), parent);
							SwapShoreMaterialIfNeeded(_tmp_shoreSideDoubleTinyCornerPrefab, enableForest, enableDesert, grassMaterial, sandMaterial);
						}
						else
						{
							GameObject side = Instantiate(shoreSidePrefab, position, Quaternion.Euler(0, 270, 0), parent);
							SwapShoreMaterialIfNeeded(side, enableForest, enableDesert, grassMaterial, sandMaterial);
					
							if (landNE && waterE && !landSE)
							{
								GameObject _tmp_shoreTinyCornerPrefab = Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 180, 0), side.transform);
								SwapShoreMaterialIfNeeded(_tmp_shoreTinyCornerPrefab, enableForest, enableDesert, grassMaterial, sandMaterial);
							}
							else if (landSE && waterE && !landNE)
							{							
								GameObject _tmp_shoreTinyCornerPrefab = Instantiate(shoreTinyCornerPrefab, position, Quaternion.Euler(0, 270, 0), side.transform);
								SwapShoreMaterialIfNeeded(_tmp_shoreTinyCornerPrefab, enableForest, enableDesert, grassMaterial, sandMaterial);
							}
						}
					}
					else
					{
						GameObject.Instantiate(waterPrefab, position, Quaternion.identity, parent);
					}
				}
			}
		}
		
		const float TILE_SIZE = 1f; // adjust if your cell size is different
		SpawnBiomeBorderOverlays(biomeMap, TILE_SIZE, parent, biomeOverlayPrefab, grassToSandMat, sandToGrassMat, enableForest, enableDesert);
	}
	
	public MapData BuildMap(int width, int height, NoiseSettings settings,
							bool enableForest, bool enableDesert, bool isRareWater)
	{
		// --- Legacy terrain tweaks (same as old GenerateMap) ---
		NoiseSettings terrainSettings = new NoiseSettings
		{
			seed        = settings.seed + 1,
			scale       = Mathf.Max(35f, settings.scale),
			octaves     = 3,
			persistence = Mathf.Clamp(settings.persistence, 0.40f, 0.75f)
		};
	
		float waterThreshold = isRareWater ? 0.10f : 0.30f;
	
		// A) Terrain classification uses the tweaked settings
		float[,] terrainNoiseMap = GenerateNoiseMap(width, height, terrainSettings);
		TileType[,] logicalMap   = GenerateLogicalMap(terrainNoiseMap, waterThreshold);
	
		// Biomes still use the raw UI settings (as before)
		BiomeType[,] biomeMap = BuildBiomeMap(width, height, settings, logicalMap, enableForest, enableDesert);
	
		// B) Decorations use the raw UI settings (legacy 'decorationNoiseMap')
		float[,] decorationNoiseMap = GenerateNoiseMap(width, height, settings);
	
		// Map container (seed = UI seed for deterministic RNG elsewhere)
		var map = new MapData(width, height, 1f, settings.seed);
	
		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				map[x, y] = new Cell
				{
					tile  = logicalMap[x, y],
					biome = biomeMap[x, y]
				};
	
				// Store the exact per-tile value the legacy rules used for decorations
				map.SetFeatureValue(x, y, Mathf.Clamp01(decorationNoiseMap[x, y]));
			}
		}
	
		// Precompute shores using the same legacy logic (already ported)
		PrecomputeShores(map, logicalMap);
	
		// Decorations are placed at render time with legacy rules (no precompute here)
		return map;
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
	
	private void PrecomputeShores(MapData map, TileType[,] logicalMap)
	{
		int w = map.width, h = map.height;
	
		for (int y = 0; y < h; y++)
		for (int x = 0; x < w; x++)
		{
			if (logicalMap[x, y] == TileType.Water)
			{
				// Use your Step-2 function:
				var pieces = EvaluateShore(x, y, logicalMap); // List<ShoreInfo>
				map.SetShores(x, y, pieces);                 // store 0..N pieces
			}
			else
			{
				// Land: ensure empty
				map.SetShores(x, y, null);
			}
		}
	}
	
	private List<ShoreInfo> EvaluateShore(int x, int y, TileType[,] logicalMap)
	{
		var pieces = new List<ShoreInfo>(3);
	
		// Only shore-evaluate WATER tiles. Land tiles return empty (renderer handles land normally).
		if (logicalMap[x, y] != TileType.Water)
			return pieces; // empty
	
		// ---- Neighbor probes (keep your original orientation: N = y+1, S = y-1) ----
		bool landN  = IsLand(logicalMap, x,     y + 1);
		bool landE  = IsLand(logicalMap, x + 1, y    );
		bool landS  = IsLand(logicalMap, x,     y - 1);
		bool landW  = IsLand(logicalMap, x - 1, y    );
		bool landNE = IsLand(logicalMap, x + 1, y + 1);
		bool landSE = IsLand(logicalMap, x + 1, y - 1);
		bool landSW = IsLand(logicalMap, x - 1, y - 1);
		bool landNW = IsLand(logicalMap, x - 1, y + 1);
	
		bool landOrOutOfBoundsN = IsLandOrOutOfBounds(logicalMap, x,     y + 1);
		bool landOrOutOfBoundsE = IsLandOrOutOfBounds(logicalMap, x + 1, y    );
		bool landOrOutOfBoundsS = IsLandOrOutOfBounds(logicalMap, x,     y - 1);
		bool landOrOutOfBoundsW = IsLandOrOutOfBounds(logicalMap, x - 1, y    );
	
		bool waterN = IsWater(logicalMap, x,     y + 1);
		bool waterE = IsWater(logicalMap, x + 1, y    );
		bool waterS = IsWater(logicalMap, x,     y - 1);
		bool waterW = IsWater(logicalMap, x - 1, y    );
	
		int waterCount = 0;
		if (waterN) waterCount++;
		if (waterE) waterCount++;
		if (waterS) waterCount++;
		if (waterW) waterCount++;
	
		// ---- Prefab codes (index into ChunkRenderer.shorePrefabsByCode) ----
		const short CODE_POND                  = 0;
		const short CODE_SHORE_POCKET          = 1;
		const short CODE_DOUBLE_SIDE           = 2;
		const short CODE_CORNER                = 3;
		const short CODE_CORNER_EXT            = 4;
		const short CODE_DOUBLE_TINY           = 5;
		const short CODE_TINY                  = 6;
		const short CODE_SIDE                  = 7;
		const short CODE_SIDE_DOUBLE_TINY      = 8;
	
		// Small helper for adding a piece (rotation only; no offsets used in your legacy shore code)
		void Add(short code, float rotY)
		{
			pieces.Add(new ShoreInfo {
				present   = true,
				code      = code,
				rotationY = rotY,
				offset    = Vector2.zero,
				yOffset   = 0f
			});
		}
	
		// ---- U-shaped shore (pocket) ----
		if (waterCount == 1 && waterN && IsInBounds(logicalMap, x - 1, y) && IsInBounds(logicalMap, x + 1, y))
		{
			Add(CODE_SHORE_POCKET, 180f); // North = bottom land
			return pieces;
		}
		else if (waterCount == 1 && waterE && IsInBounds(logicalMap, x, y - 1) && IsInBounds(logicalMap, x, y + 1))
		{
			Add(CODE_SHORE_POCKET, 270f); // East = left land
			return pieces;
		}
		else if (waterCount == 1 && waterS && IsInBounds(logicalMap, x - 1, y) && IsInBounds(logicalMap, x + 1, y))
		{
			Add(CODE_SHORE_POCKET, 0f);   // South = top land
			return pieces;
		}
		else if (waterCount == 1 && waterW && IsInBounds(logicalMap, x, y - 1) && IsInBounds(logicalMap, x, y + 1))
		{
			Add(CODE_SHORE_POCKET, 90f);  // West = right land
			return pieces;
		}
	
		// ---- Pond (small lake) ----
		if (landOrOutOfBoundsN && landOrOutOfBoundsE && landOrOutOfBoundsS && landOrOutOfBoundsW)
		{
			Add(CODE_POND, 0f);
			return pieces;
		}
	
		// ---- Double-side shore (water opposite sides, land on others) ----
		if (waterN && waterS && landW && landE)
		{
			Add(CODE_DOUBLE_SIDE, 0f);    // vertical channel
			return pieces;
		}
		if (waterW && waterE && landN && landS)
		{
			Add(CODE_DOUBLE_SIDE, 90f);   // horizontal channel
			return pieces;
		}
	
		// ---- Corner shore placement (2 adjacent land tiles) ----
		if (landN && landE)
		{
			if (landSW) Add(CODE_CORNER_EXT, 90f);
			else        Add(CODE_CORNER,     90f);
			return pieces;
		}
		if (landE && landS)
		{
			if (landNW) Add(CODE_CORNER_EXT, 180f);
			else        Add(CODE_CORNER,     180f);
			return pieces;
		}
		if (landS && landW)
		{
			if (landNE) Add(CODE_CORNER_EXT, 270f);
			else        Add(CODE_CORNER,     270f);
			return pieces;
		}
		if (landW && landN)
		{
			if (landSE) Add(CODE_CORNER_EXT, 0f);
			else        Add(CODE_CORNER,     0f);
			return pieces;
		}
	
		// ---- Double tiny shore tile (plus optional extra tinies) ----
		if (landSW && landSE && !landN && !landE && !landW && !landS)
		{
			Add(CODE_DOUBLE_TINY, 0f);
			if (landNE) Add(CODE_TINY, 180f);
			if (landNW) Add(CODE_TINY, 90f);
			return pieces;
		}
		if (landNW && landSW && !landN && !landE && !landS && !landW)
		{
			Add(CODE_DOUBLE_TINY, 90f);
			if (landSE) Add(CODE_TINY, 270f);
			if (landNE) Add(CODE_TINY, 180f);
			return pieces;
		}
		if (landNW && landNE && !landS && !landE && !landW && !landN)
		{
			Add(CODE_DOUBLE_TINY, 180f);
			if (landSW) Add(CODE_TINY, 0f);
			if (landSE) Add(CODE_TINY, 270f);
			return pieces;
		}
		if (landNE && landSE && !landN && !landS && !landW && !landE)
		{
			Add(CODE_DOUBLE_TINY, 270f);
			if (landNW) Add(CODE_TINY, 90f);
			if (landSW) Add(CODE_TINY, 0f);
			return pieces;
		}
	
		// ---- Tiny shore tile (single diagonal; optional opposite extra) ----
		if (landNE && !landN && !landE && !landS && !landW)
		{
			Add(CODE_TINY, 180f);
			if (landSW) Add(CODE_TINY, 0f);
			return pieces;
		}
		if (landSE && !landS && !landE && !landN && !landW)
		{
			Add(CODE_TINY, 270f);
			if (landNW) Add(CODE_TINY, 90f);
			return pieces;
		}
		if (landSW && !landS && !landW && !landN && !landE)
		{
			Add(CODE_TINY, 0f);
			if (landNE) Add(CODE_TINY, 180f);
			return pieces;
		}
		if (landNW && !landN && !landW && !landS && !landE)
		{
			Add(CODE_TINY, 90f);
			if (landSE) Add(CODE_TINY, 270f);
			return pieces;
		}
	
		// ---- Side shore logic (single cardinal land + potential diagonal tips) ----
		if (landN)
		{
			if (landSW && landSE && waterS)
			{
				Add(CODE_SIDE_DOUBLE_TINY, 0f);
			}
			else
			{
				Add(CODE_SIDE, 0f);
				if (landSW && waterS && !landSE)      Add(CODE_TINY, 0f);
				else if (landSE && waterS && !landSW) Add(CODE_TINY, 270f);
			}
			return pieces;
		}
		if (landE)
		{
			if (landNW && landSW && waterW)
			{
				Add(CODE_SIDE_DOUBLE_TINY, 90f);
			}
			else
			{
				Add(CODE_SIDE, 90f);
				if (landNW && waterW && !landSW)      Add(CODE_TINY, 90f);
				else if (landSW && waterW && !landNW) Add(CODE_TINY, 0f);
			}
			return pieces;
		}
		if (landS)
		{
			if (landNW && landNE && waterN)
			{
				Add(CODE_SIDE_DOUBLE_TINY, 180f);
			}
			else
			{
				Add(CODE_SIDE, 180f);
				if (landNW && waterN && !landNE)      Add(CODE_TINY, 90f);
				else if (landNE && waterN && !landNW) Add(CODE_TINY, 180f);
			}
			return pieces;
		}
		if (landW)
		{
			if (landNE && landSE && waterE)
			{
				Add(CODE_SIDE_DOUBLE_TINY, 270f);
			}
			else
			{
				Add(CODE_SIDE, 270f);
				if (landNE && waterE && !landSE)      Add(CODE_TINY, 180f);
				else if (landSE && waterE && !landNE) Add(CODE_TINY, 270f);
			}
			return pieces;
		}
	
		// ---- Default: regular water (no shore piece) ----
		// (Renderer will spawn base water tile when list is empty.)
		return pieces;
	}
	
	//Function to generate, deterministically, "random" numbers for the orientation of objects so the map has consistency
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
	
	private void PrecomputeDecorations(MapData map)
	{
		// simple defaults — tweak later in UI if you want
		const float forestDensity = 0.15f;
		const float desertDensity = 0.05f;
		const float jitterMaxX = 0.30f;
		const float jitterMaxZ = 0.30f;
		const float yOffset = 0f;               // raise if your props clip into ground
		const float scaleMin = 0.9f;
		const float scaleMax = 1.2f;
		const int   maxPerTile = 1;             // keep cheap for now
	
		int w = map.width, h = map.height;
	
		for (int y = 0; y < h; y++)
		for (int x = 0; x < w; x++)
		{
			var cell = map[x, y];
			if (cell.tile != TileType.Land)
			{
				map.SetDecos(x, y, null); // none on water
				continue;
			}
	
			float density = (cell.biome == BiomeType.Forest) ? forestDensity : desertDensity;
			var list = map.GetDecos(x, y);
			list.Clear();
	
			// Decide how many to place (0..maxPerTile)
			float rPlace = R01(x, y, map.seed, 0xA53F9E2Du);
			if (rPlace >= density) { /* leave empty */ map.SetDecos(x, y, list); continue; }
	
			int count = 1; // could do: int count = 1 + (R01(...)<extraProb ? 1 : 0);
			count = Mathf.Min(count, maxPerTile);
	
			for (int i = 0; i < count; i++)
			{
				float rx = (R01(x, y, map.seed, 0x85EBCA6Bu) * 2f - 1f) * jitterMaxX;
				float rz = (R01(x, y, map.seed, 0x165667B1u) * 2f - 1f) * jitterMaxZ;
				float yaw = R01(x, y, map.seed, 0x27D4EB2Fu) * 360f;
				float scl = Mathf.Lerp(scaleMin, scaleMax, R01(x, y, map.seed, 0xC2B2AE35u));
	
				// choose prefab index deterministically; the renderer will pick the array by biome
				short code = (short)Mathf.RoundToInt(R01(x, y, map.seed, 0x9E3779B9u) * 1000f); // big range now, clamp in renderer
	
				list.Add(new DecoInfo
				{
					present = true,
					code = code,
					rotationY = yaw,
					scale = scl,
					offset = new Vector2(rx, rz),
					yOffset = yOffset
				});
			}
	
			map.SetDecos(x, y, list);
		}
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
	
	private int[,] ComputeWaterDistance(TileType[,] map, bool eightNeighbors = true)
	{
		int w = map.GetLength(0), h = map.GetLength(1);
		int[,] dist = new int[w, h];
		const int INF = 1_000_000;
	
		var q = new System.Collections.Generic.Queue<Vector2Int>();
		for (int x = 0; x < w; x++)
		for (int y = 0; y < h; y++)
		{
			if (map[x, y] == TileType.Water)
			{
				dist[x, y] = 0;
				q.Enqueue(new Vector2Int(x, y));
			}
			else dist[x, y] = INF;
		}
	
		int[] dx4 = { 1, -1, 0, 0 };
		int[] dy4 = { 0, 0, 1, -1 };
		int[] dx8 = { 1, -1, 0, 0,  1,  1, -1, -1 };
		int[] dy8 = { 0,  0, 1, -1,  1, -1,  1, -1 };
	
		var dx = eightNeighbors ? dx8 : dx4;
		var dy = eightNeighbors ? dy8 : dy4;
	
		while (q.Count > 0)
		{
			var p = q.Dequeue();
			int d = dist[p.x, p.y] + 1;
	
			for (int k = 0; k < dx.Length; k++)
			{
				int nx = p.x + dx[k], ny = p.y + dy[k];
				if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
				if (dist[nx, ny] <= d) continue;
				dist[nx, ny] = d;
				q.Enqueue(new Vector2Int(nx, ny));
			}
		}
	
		return dist;
	}
	
	private void SpawnBiomeBorderOverlays(
    BiomeType[,] biomeMap, float tileSize, Transform parent,
    GameObject overlayPrefab, Material grassToSandMat, Material sandToGrassMat, bool enableForest, bool enableDesert)
	{
		if (overlayPrefab == null || parent == null || biomeMap == null) return;
		
		if (!enableForest || !enableDesert) return;
	
		int w = biomeMap.GetLength(0), h = biomeMap.GetLength(1);
	
		// Neighbor directions (E, W, N, S) in grid
		Vector2Int[] dirs = { new(1,0), new(-1,0), new(0,1), new(0,-1) };
		// World "right" vectors toward neighbor (same order)
		Vector3[] rightDirs = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
	
		const float thicknessFrac = 0.45f;   // band width across the border (0.20–0.35 looks good)
		const float yLift        = 0.02f;    // lift to avoid z-fighting
		const float centerBias   = 0.0f;     // -0.5..+0.5 push band into Forest(-) or Desert(+)
	
		float half = tileSize * 0.5f;
		// float thickness = tileSize * thicknessFrac; // local X (across)
		// float length    = tileSize;                 // local Y (along)
		const float endBleedFrac = 0.05f;                // how much of 'thickness' to extend past each end (0.45–0.65 works)
		float thickness = tileSize * thicknessFrac;      // across the border (you already have this)
		float length    = tileSize + thickness * endBleedFrac * 2f;
	
		for (int x = 0; x < w; x++)
		for (int y = 0; y < h; y++)
		{
			var here = biomeMap[x, y];
	
			for (int d = 0; d < 4; d++)
			{
				int nx = x + dirs[d].x, ny = y + dirs[d].y;
				if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
	
				var there = biomeMap[nx, ny];
				if (here == there) continue;
				if ((int)here > (int)there) continue; // place once per edge
	
				Vector3 right   = rightDirs[d];              // toward neighbor (Desert if here=Forest)
				Vector3 forward = Vector3.Cross(Vector3.up, right); // along the border
				Vector3 pos     = new Vector3(x * tileSize, yLift, y * tileSize) + right * half;
	
				// Center the strip on the border; bias nudges it to one side if desired
				pos += right * (centerBias * thickness * 0.5f);
	
				// Make quad: local Z = up, local Y = forward, local X = right
				Quaternion rot = Quaternion.LookRotation(Vector3.up, forward);
	
				var go = Instantiate(overlayPrefab, pos, rot, parent);
				go.transform.localScale = new Vector3(thickness, length, 1f); // X=across, Y=along
	
				var mr = go.GetComponent<MeshRenderer>();
				if (mr)
				{
					mr.shadowCastingMode = ShadowCastingMode.Off;
					mr.receiveShadows = false;
	
					bool grassToSand = (here == BiomeType.Forest && there == BiomeType.Desert);
					mr.sharedMaterial = grassToSand ? grassToSandMat : sandToGrassMat;
				}
			}
		}
	}
	
	void SwapShoreMaterialIfNeeded(GameObject tile, bool enableForest, bool enableDesert, Material grassMaterial, Material sandMaterial)
	{
		if (!enableForest && enableDesert)
		{
			MeshRenderer renderer = tile.GetComponent<MeshRenderer>();
			if (renderer != null)
			{
				Material[] materials = renderer.materials;
				bool updated = false;
	
				for (int i = 0; i < materials.Length; i++)
				{
					if (materials[i] != null && materials[i].name.StartsWith(grassMaterial.name))
					{
						materials[i] = sandMaterial;
						updated = true;
					}
				}
	
				if (updated)
				{
					renderer.materials = materials;
				}
			}
		}
	}
	
	private void OverrideAllMaterials(GameObject go, Material mat)
	{
		if (go == null || mat == null) return;
		var renderers = go.GetComponentsInChildren<MeshRenderer>(true);
		for (int r = 0; r < renderers.Length; r++)
		{
			var mr = renderers[r];
			var mats = mr.sharedMaterials;
			for (int i = 0; i < mats.Length; i++) mats[i] = mat;
			mr.sharedMaterials = mats;
		}
	}
}
