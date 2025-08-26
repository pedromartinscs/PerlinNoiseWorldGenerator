using System;
using UnityEngine;
using UnityEngine.UI;

public class BiomeChecklist : MonoBehaviour
{
    [Header("Toggles")]
    [SerializeField] private Toggle forestToggle;
    [SerializeField] private Toggle desertToggle;

    // Fires whenever the set of enabled biomes changes
    public event Action<bool, bool> OnChanged;

    void Awake()
    {
        // If both are off in the inspector, default Forest on
        if (forestToggle != null || desertToggle != null)
        {
            if (!(forestToggle != null && forestToggle.isOn) &&
                !(desertToggle != null && desertToggle.isOn))
            {
                if (forestToggle != null) forestToggle.isOn = true;
            }
        }

        Subscribe();
        // Initial validation + initial notify
        Validate(forestToggle != null && forestToggle.isOn ? forestToggle : desertToggle);
    }

    void OnEnable()  => Subscribe();
    void OnDisable() => Unsubscribe();

    void Subscribe()
    {
        if (forestToggle != null) forestToggle.onValueChanged.AddListener(_ => Validate(forestToggle));
        if (desertToggle != null) desertToggle.onValueChanged.AddListener(_ => Validate(desertToggle));
    }

    void Unsubscribe()
    {
        if (forestToggle != null) forestToggle.onValueChanged.RemoveAllListeners();
        if (desertToggle != null) desertToggle.onValueChanged.RemoveAllListeners();
    }

    void Validate(Toggle changed)
    {
        // Ensure at least one is on
        bool forestOn = forestToggle != null && forestToggle.isOn;
        bool desertOn = desertToggle != null && desertToggle.isOn;

        if (!forestOn && !desertOn && changed != null)
        {
            changed.isOn = true; // re-enable the one the user just turned off
            forestOn = forestToggle != null && forestToggle.isOn;
            desertOn = desertToggle != null && desertToggle.isOn;
        }

        OnChanged?.Invoke(forestOn, desertOn);
    }

    public bool Forest => forestToggle != null && forestToggle.isOn;
    public bool Desert => desertToggle != null && desertToggle.isOn;
}
