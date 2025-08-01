using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    [Header("Inputs")]
    public TMP_InputField seedInput;
	public Slider scaleSlider;
	public TextMeshProUGUI scaleLabel;
	public Slider octaveSlider;
	public TextMeshProUGUI octaveLabel;
	public Slider persistenceSlider;
	public TextMeshProUGUI persistenceLabel;
	public TMP_InputField mapWidthInput;
	public TMP_InputField mapHeightInput;

    [Header("Output")]
    public RawImage previewImage;

    [Header("Generator")]
    public PerlinNoiseGenerator generator;
	
	[Header("Map Prefabs")]
	public GameObject groundPrefab;
	public GameObject waterPrefab;
	public GameObject shoreSidePrefab;
	public GameObject shoreCornerPrefab;
	public GameObject shoreTinyCornerPrefab;	
	public GameObject shorePocketPrefab;
	public GameObject shoreCornerExtendedPrefab;
	public GameObject shoreDoubleSidePrefab;
	public GameObject shoreDoubleTinyCornerPrefab;
	public GameObject pondPrefab;
	public GameObject shoreSideDoubleTinyCornerPrefab;
	public List<GameObject> treePrefabs;
	public List<GameObject> rockPrefabs;
	
	private Transform mapParent;

    private const int previewSize = 128;
	
	void Start()
	{
		// Initialize label values
		UpdateScaleLabel();
		UpdateOctaveLabel();
		UpdatePersistenceLabel();
	
		// Add listeners
		scaleSlider.onValueChanged.AddListener(delegate { UpdateScaleLabel(); });
		octaveSlider.onValueChanged.AddListener(delegate { UpdateOctaveLabel(); });
		persistenceSlider.onValueChanged.AddListener(delegate { UpdatePersistenceLabel(); });
	}
	
	void UpdateScaleLabel()
	{
		scaleLabel.text = $"Scale: {scaleSlider.value:F0}";
	}
	
	void UpdateOctaveLabel()
	{
		octaveLabel.text = $"Octaves: {octaveSlider.value:F0}";
	}
	
	void UpdatePersistenceLabel()
	{
		persistenceLabel.text = $"Persistence: {persistenceSlider.value:F2}";
	}
	
	public void OnRandomSeed()
	{
		int randomSeed = Random.Range(0, 99999);
		seedInput.text = randomSeed.ToString();
	}

	private void ClearPreviousMap()
	{
		if (mapParent != null)
			Destroy(mapParent.gameObject);
	}

    public void OnGenerateNoise()
    {
        NoiseSettings settings = GetSettingsFromUI();
        Texture2D texture = generator.GenerateNoiseTexture(previewSize, previewSize, settings);
        previewImage.texture = texture;
    }

    public void OnGenerateNoiseAndMap()
    {
        ClearPreviousMap();
		
		OnGenerateNoise();

		int width = GetMapWidth();
		int height = GetMapHeight();
		NoiseSettings settings = GetSettingsFromUI();
	
		mapParent = new GameObject("GeneratedMap").transform;
	
		generator.GenerateMap(
			width,
			height,
			settings,
			mapParent,
			groundPrefab,
			treePrefabs,
			rockPrefabs,
			waterPrefab,
			shoreSidePrefab,
			shoreCornerPrefab,
			shoreTinyCornerPrefab,
			shorePocketPrefab,
			shoreCornerExtendedPrefab,
			shoreDoubleSidePrefab,
			shoreDoubleTinyCornerPrefab,
			pondPrefab,
			shoreSideDoubleTinyCornerPrefab
		);
    }
	
	public void OnExit()
	{
		#if UNITY_EDITOR
			UnityEditor.EditorApplication.isPlaying = false;
		#else
			Application.Quit();
		#endif
	}

    private NoiseSettings GetSettingsFromUI()
    {
        NoiseSettings settings = new NoiseSettings();

        int.TryParse(seedInput.text, out settings.seed);
        settings.scale = scaleSlider.value;
		settings.octaves = Mathf.RoundToInt(octaveSlider.value);
		settings.persistence = persistenceSlider.value;

        return settings;
    }
	
	public int GetMapWidth()
	{
		return int.TryParse(mapWidthInput.text, out int w) ? Mathf.Clamp(w, 1, 500) : 64;
	}
	
	public int GetMapHeight()
	{
		return int.TryParse(mapHeightInput.text, out int h) ? Mathf.Clamp(h, 1, 500) : 64;
	}
}
