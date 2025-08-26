using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(GridLayoutGroup))]
public class AutoGridColumns : MonoBehaviour
{
    GridLayoutGroup grid;
    RectTransform rt;

    void Awake()
    {
        grid = GetComponent<GridLayoutGroup>();
        rt = GetComponent<RectTransform>();
        UpdateColumns();
    }

    void OnRectTransformDimensionsChange()
    {
        if (!isActiveAndEnabled) return;
        UpdateColumns();
    }

    void UpdateColumns()
    {
        float width = rt.rect.width;
        if (width <= 0f) return;

        float padding = grid.padding.left + grid.padding.right;
        float cell    = grid.cellSize.x;
        float space   = grid.spacing.x;

        // How many cells fit across the current width?
        int cols = Mathf.Max(1, Mathf.FloorToInt((width - padding + space) / (cell + space)));
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = cols;
    }
}
