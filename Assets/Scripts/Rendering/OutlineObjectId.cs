using UnityEngine;

[ExecuteAlways, DisallowMultipleComponent]
public class OutlineObjectId : MonoBehaviour
{
    static readonly int _ObjectIdProp = Shader.PropertyToID("_ObjectId01");

    [Tooltip("Apply the same ID to all child Renderers (recommended for multi-mesh prefabs).")]
    public bool applyToChildren = true;

    // Stored so the ID is stable across re-apply calls.
    [SerializeField, Range(1, 255)] int _id8 = 0;

    MaterialPropertyBlock _mpb;

    void OnEnable()   { EnsureId(); Apply(); }
    void OnValidate() { EnsureId(); Apply(); }

    void EnsureId()
    {
        if (_id8 >= 1 && _id8 <= 255) return;

        // Derive a stable 8-bit value from the instance id, but NEVER 0.
        int h = GetInstanceID();
        // Mix bits so the low 8 bits aren't frequently zero.
        h ^= (h >> 8);
        h ^= (h >> 16);
        h &= 0xFF;              // 0..255
        if (h == 0) h = 1;      // reserve 0 for background
        _id8 = h;
    }

    void Apply()
    {
        _mpb ??= new MaterialPropertyBlock();
        float id01 = _id8 / 255f;

        if (applyToChildren)
        {
            var renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            foreach (var r in renderers)
            {
                if (!r) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetFloat(_ObjectIdProp, id01);
                r.SetPropertyBlock(_mpb);
            }
        }
        else
        {
            var r = GetComponent<Renderer>();
            if (!r) return;
            r.GetPropertyBlock(_mpb);
            _mpb.SetFloat(_ObjectIdProp, id01);
            r.SetPropertyBlock(_mpb);
        }
    }
}
