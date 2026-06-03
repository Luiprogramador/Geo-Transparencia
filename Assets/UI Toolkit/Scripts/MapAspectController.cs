using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class MapAspectController : MonoBehaviour
{
    [Tooltip("Height = width * aspect")]
    public float aspect = 0.75f; // height = width * aspect; increased to match reference
    // Recommended desktop aspect to match reference map shape
    // height = width * aspect. Increase to 0.75 to make map taller.
    // You can tweak in Inspector.
    // public float aspect = 0.75f;

    [Tooltip("Hide filters when root width is less than this (px)")]
    public float hideFiltersBelow = 1200f;

    [Tooltip("Switch to column layout below this width (px)")]
    public float columnBelow = 800f;

    UIDocument uiDoc;
    VisualElement root;
    VisualElement mapContainer;
    VisualElement filters;
    VisualElement resumo;

    void OnEnable()
    {
        uiDoc = GetComponent<UIDocument>();
        if (uiDoc == null) return;
        root = uiDoc.rootVisualElement;

        // Try to find elements by name first; ensure your UXML sets name="mapContainer" etc.
        mapContainer = root.Q<VisualElement>("mapContainer");
        if (mapContainer == null)
            mapContainer = root.Q<VisualElement>(className: "mapContainer");

        filters = root.Q<VisualElement>("filtros");
        if (filters == null)
            filters = root.Q<VisualElement>(className: "filtros");

        resumo = root.Q<VisualElement>("resumo");
        if (resumo == null)
            resumo = root.Q<VisualElement>(className: "resumo");

        root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        UpdateLayout();
    }

    void OnDisable()
    {
        if (root != null)
            root.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
    }

    void OnGeometryChanged(GeometryChangedEvent evt)
    {
        UpdateLayout();
    }

    void UpdateLayout()
    {
        if (root == null) return;

        float rootWidth = root.layout.width;

        // Ensure map container fills remaining flex space instead of forcing a height.
        if (mapContainer != null)
        {
            mapContainer.style.flexGrow = 1;
            // Clear any explicit height previously set so USS flex can size it.
            mapContainer.style.height = StyleKeyword.Auto;
        }

        // Hide filters on narrow screens
        if (filters != null)
        {
            filters.style.display = rootWidth < hideFiltersBelow ? DisplayStyle.None : DisplayStyle.Flex;
        }

        // Switch root direction
        if (rootWidth < columnBelow)
        {
            root.style.flexDirection = UnityEngine.UIElements.FlexDirection.Column;
        }
        else
        {
            root.style.flexDirection = UnityEngine.UIElements.FlexDirection.Row;
        }
    }
}
