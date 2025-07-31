using UnityEngine;
using System.Collections.Generic;

public class PerlinNoiseGenerator : MonoBehaviour
{
	//Important to avoid having floating trees, floating rocks and so on...
	float treeYOffset = -0.1f;
	float rockYOffset = -0.1f;
	
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
	
	public void GenerateMap(int width, int height, NoiseSettings settings, Transform parent, GameObject groundPrefab, List<GameObject> treePrefabs, List<GameObject> rockPrefabs)
	{
		float[,] noiseMap = GenerateNoiseMap(width, height, settings);
	
		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				float value = noiseMap[x, y];
				Vector3 position = new Vector3(x, 0, y);
	
				// Place ground tile
				GameObject.Instantiate(groundPrefab, position, Quaternion.identity, parent);
	
				// Place objects based on noise value
				if (value > 0.7f)
				{
					GameObject tree = treePrefabs[Random.Range(0, treePrefabs.Count)];
					Quaternion randomYRotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
					GameObject.Instantiate(tree, position + Vector3.up * treeYOffset, randomYRotation, parent);
				}
				else if (value > 0.5f)
				{
					GameObject rock = rockPrefabs[Random.Range(0, rockPrefabs.Count)];
					Quaternion randomYRotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
					GameObject.Instantiate(rock, position + Vector3.up * rockYOffset, randomYRotation, parent);
				}
			}
		}
	}
}
