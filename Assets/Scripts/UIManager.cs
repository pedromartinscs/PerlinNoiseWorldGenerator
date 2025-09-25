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
	public TMP_Dropdown waterDropdown;
	
	[Header("Grid")]
	[SerializeField] private Toggle showGridToggle;
	[SerializeField] private Color gridColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);
	[SerializeField] private float gridLineThickness = 0.02f;

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
	public GameObject biomeOverlayPrefab;
	public List<GameObject> desertTreePrefabs;
	public List<GameObject> desertVegetationPrefabs;
	public List<GameObject> forestTreePrefabs;
	public List<GameObject> forestVegetationPrefabs;
	public List<GameObject> randomDecorationPrefabs;
	
	[Header("Biomes")]
	public BiomeChecklist biomeChecklist;


	[Header("Materials")]
	public Material sandMaterial;
	public Material grassMaterial;
	public Material grassToSandMat;
	public Material sandToGrassMat;
	
	private Transform mapParent;
	
	private GridOverlay gridOverlay;

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
		if (showGridToggle != null) showGridToggle.onValueChanged.AddListener(OnShowGridChanged);
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
		
		// Preview texture still works as before
		OnGenerateNoise();
		
		int width = GetMapWidth();
		int height = GetMapHeight();
		NoiseSettings settings = GetSettingsFromUI();
		
		bool isRareWater = waterDropdown.value == 1;
		
		// Biomes toggles (keep as-is)
		bool enableForest = (biomeChecklist != null && biomeChecklist.Forest);
		bool enableDesert = (biomeChecklist != null && biomeChecklist.Desert);
		
		// --- Phase 1: GENERATION ONLY (no spawning here)
		MapData map = generator.BuildMap(width, height, settings, enableForest, enableDesert, isRareWater);
		
		// --- Phase 2: RENDERING via ChunkRenderer ---
		mapParent = new GameObject("GeneratedMap").transform;
		
		var chunkRenderer = mapParent.gameObject.AddComponent<ChunkRenderer>();
		chunkRenderer.Initialize(
			map,
			groundPrefab,
			waterPrefab,
			grassMaterial,
			sandMaterial,
			Camera.main != null ? Camera.main.transform : Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Exclude)?.transform,
			chunkSize: 32,        // tweak if you like
			renderRadius: 2       // tweak if you like
		);
		
		chunkRenderer.shorePrefabsByCode = BuildShorePrefabsByCode();
		
		chunkRenderer.forestTreePrefabs        = forestTreePrefabs?.ToArray();
		chunkRenderer.forestVegetationPrefabs  = forestVegetationPrefabs?.ToArray();
		chunkRenderer.desertTreePrefabs        = desertTreePrefabs?.ToArray();
		chunkRenderer.desertVegetationPrefabs  = desertVegetationPrefabs?.ToArray();
		chunkRenderer.randomDecorationPrefabs  = randomDecorationPrefabs?.ToArray();
		
		chunkRenderer.treeYOffset              = 0f;
		chunkRenderer.vegetationYOffset        = 0f;
		float desertTreeThreshold 			   = 0.05f;
		float desertVegetationThreshold 	   = 0.10f;
		if (enableForest && enableDesert)
		{
			desertTreeThreshold += 0.10f;
			desertVegetationThreshold += 0.10f;
		}
		chunkRenderer.desertTreeThreshold      = desertTreeThreshold;
		chunkRenderer.desertVegetationThreshold= desertVegetationThreshold;
		chunkRenderer.biomeOverlayMaterial = sandToGrassMat;
		chunkRenderer.biomeOverlayPrefab = biomeOverlayPrefab;
		chunkRenderer.drawBiomeBorders    = true; 
		
		// ---- Build / Update Grid Overlay ----
		if (gridOverlay != null) Destroy(gridOverlay.gameObject);
		
		var gridGO = new GameObject("GridOverlay");
		gridGO.transform.SetParent(mapParent, false);
		
		gridOverlay = gridGO.AddComponent<GridOverlay>();
		gridOverlay.lineColor = gridColor;
		gridOverlay.lineThickness = gridLineThickness;
		
		const float TILE_SIZE = 1f; // same as MapData
		gridOverlay.Build(width, height, TILE_SIZE);
		
		bool show = showGridToggle != null && showGridToggle.isOn;
		gridOverlay.SetVisible(show);
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
	
	private GameObject[] BuildShorePrefabsByCode()
	{
		// Indices must match the codes used in EvaluateShore(...)
		// 0 pondPrefab
		// 1 shorePocketPrefab
		// 2 shoreDoubleSidePrefab
		// 3 shoreCornerPrefab
		// 4 shoreCornerExtendedPrefab
		// 5 shoreDoubleTinyCornerPrefab
		// 6 shoreTinyCornerPrefab
		// 7 shoreSidePrefab
		// 8 shoreSideDoubleTinyCornerPrefab
		return new GameObject[]
		{
			pondPrefab,
			shorePocketPrefab,
			shoreDoubleSidePrefab,
			shoreCornerPrefab,
			shoreCornerExtendedPrefab,
			shoreDoubleTinyCornerPrefab,
			shoreTinyCornerPrefab,
			shoreSidePrefab,
			shoreSideDoubleTinyCornerPrefab
		};
	}
	
	public int GetMapWidth()
	{
		return int.TryParse(mapWidthInput.text, out int w) ? Mathf.Clamp(w, 1, 500) : 64;
	}
	
	public int GetMapHeight()
	{
		return int.TryParse(mapHeightInput.text, out int h) ? Mathf.Clamp(h, 1, 500) : 64;
	}
	
	private void OnShowGridChanged(bool on)
	{
		if (gridOverlay != null) gridOverlay.SetVisible(on);
	}
	
	void OnDestroy()
	{
		if (showGridToggle != null) showGridToggle.onValueChanged.RemoveListener(OnShowGridChanged);
	}
}
