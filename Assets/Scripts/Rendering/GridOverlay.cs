using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class GridOverlay : MonoBehaviour
{
    [Header("Appearance")]
    [Range(0.001f, 0.1f)]
    public float lineThickness = 0.02f;
    [Tooltip("Slight Y offset to avoid Z-fighting with ground.")]
    public float yOffset = 0.01f;
    public Color lineColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);

    [Header("Material (optional)")]
    [SerializeField] private Material customMaterial; // leave null to auto-create URP Unlit

    MeshFilter mf;
    MeshRenderer mr;

    int _builtW = -1, _builtH = -1;
    float _builtTileSize = -1f;

    void Awake()
    {
        mf = GetComponent<MeshFilter>();
        mr = GetComponent<MeshRenderer>();
        EnsureMaterial();
        ApplyRenderSettings();
    }

    void EnsureMaterial()
    {
        if (mr == null) mr = GetComponent<MeshRenderer>();

        if (customMaterial != null)
        {
            mr.sharedMaterial = customMaterial;
            TrySetBaseColor(mr.sharedMaterial, lineColor);
            return;
        }

        // Auto-create a simple transparent unlit material (URP)
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlit == null) unlit = Shader.Find("Unlit/Color"); // fallback

        var mat = new Material(unlit);
        TrySetBaseColor(mat, lineColor);
        mat.renderQueue = 3000; // Transparent
        mr.sharedMaterial = mat;
    }

    void TrySetBaseColor(Material m, Color c)
    {
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        else if (m.HasProperty("_Color")) m.SetColor("_Color", c);
    }

    void ApplyRenderSettings()
    {
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        if (mr.sharedMaterial != null)
        {
            // Best-effort transparent settings across URP/Built-in
            if (mr.sharedMaterial.HasProperty("_Surface")) mr.sharedMaterial.SetFloat("_Surface", 1f); // 1=Transparent
            if (mr.sharedMaterial.HasProperty("_ZWrite")) mr.sharedMaterial.SetFloat("_ZWrite", 0f);
            if (mr.sharedMaterial.HasProperty("_Cull")) mr.sharedMaterial.SetFloat("_Cull", 0f); // Both
        }
    }

    public void SetVisible(bool visible)
    {
        if (mr == null) mr = GetComponent<MeshRenderer>();
        mr.enabled = visible;
    }

    /// <summary>
    /// Builds or rebuilds the grid mesh.
    /// </summary>
    public void Build(int width, int height, float tileSize = 1f)
    {
        if (mf == null) mf = GetComponent<MeshFilter>();
        if (mr == null) mr = GetComponent<MeshRenderer>();
        EnsureMaterial();
        ApplyRenderSettings();

        _builtW = width;
        _builtH = height;
        _builtTileSize = tileSize;

        // We generate thin quads for each line (vertical and horizontal).
        var mesh = new Mesh();
        mesh.name = "GridOverlayMesh";

        int vLines = width + 1;
        int hLines = height + 1;
        int totalLines = vLines + hLines;
        int quads = totalLines;

        var verts = new Vector3[quads * 4];
        var tris  = new int[quads * 6];

        float half = Mathf.Max(0.0001f, lineThickness * 0.5f);

        int v = 0;
        int t = 0;

        // Vertical lines (parallel to Z axis)
        for (int x = 0; x <= width; x++)
        {
            float wx = x * tileSize;
            Vector3 a = new Vector3(wx - half, yOffset, 0f);
            Vector3 b = new Vector3(wx + half, yOffset, 0f);
            Vector3 c = new Vector3(wx + half, yOffset, height * tileSize);
            Vector3 d = new Vector3(wx - half, yOffset, height * tileSize);

            verts[v + 0] = a;
            verts[v + 1] = b;
            verts[v + 2] = c;
            verts[v + 3] = d;

            tris[t + 0] = v + 0;
            tris[t + 1] = v + 1;
            tris[t + 2] = v + 2;
            tris[t + 3] = v + 0;
            tris[t + 4] = v + 2;
            tris[t + 5] = v + 3;

            v += 4;
            t += 6;
        }

        // Horizontal lines (parallel to X axis)
        for (int z = 0; z <= height; z++)
        {
            float wz = z * tileSize;
            Vector3 a = new Vector3(0f, yOffset, wz - half);
            Vector3 b = new Vector3(width * tileSize, yOffset, wz - half);
            Vector3 c = new Vector3(width * tileSize, yOffset, wz + half);
            Vector3 d = new Vector3(0f, yOffset, wz + half);

            verts[v + 0] = a;
            verts[v + 1] = b;
            verts[v + 2] = c;
            verts[v + 3] = d;

            tris[t + 0] = v + 0;
            tris[t + 1] = v + 1;
            tris[t + 2] = v + 2;
            tris[t + 3] = v + 0;
            tris[t + 4] = v + 2;
            tris[t + 5] = v + 3;

            v += 4;
            t += 6;
        }
		
		Vector3 offset = new Vector3(tileSize * 0.5f, 0f, tileSize * 0.5f);
		for (int i = 0; i < verts.Length; i++)
		{
			verts[i] -= offset;
		}

        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        mf.sharedMesh = mesh;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (_builtW > 0 && _builtH > 0)
        {
            Build(_builtW, _builtH, _builtTileSize > 0f ? _builtTileSize : 1f);
        }
        if (mr != null && mr.sharedMaterial != null)
        {
            TrySetBaseColor(mr.sharedMaterial, lineColor);
        }
    }
#endif
}
