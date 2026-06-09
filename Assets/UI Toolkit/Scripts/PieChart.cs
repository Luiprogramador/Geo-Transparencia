using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class PieChart : VisualElement
{
    private List<float> _values = new List<float>();
    private List<Color> _colors = new List<Color>();

    public List<float> Values
    {
        get => _values;
        set { _values = value; MarkDirtyRepaint(); }
    }

    public List<Color> Colors
    {
        get => _colors;
        set { _colors = value; MarkDirtyRepaint(); }
    }

    public PieChart()
    {
        generateVisualContent += OnGenerateVisualContent;
        style.flexGrow = 1;
        style.minHeight = 200;
    }

    private void OnGenerateVisualContent(MeshGenerationContext mgc)
    {
        if (_values == null || _values.Count == 0) return;

        float total = 0;
        foreach (float v in _values) total += v;
        if (total <= 0) return;

        var painter = mgc.painter2D;
        Rect rect = contentRect;
        Vector2 center = rect.center;
        float radius = Mathf.Min(rect.width, rect.height) * 0.45f;

        float startAngle = -90f;

        painter.strokeColor = Color.black;
        painter.lineWidth = 2f;

        for (int i = 0; i < _values.Count; i++)
        {
            float sweep = (_values[i] / total) * 360f;
            float endAngle = startAngle + sweep;

            painter.fillColor = _colors[i % _colors.Count];
            painter.BeginPath();
            painter.MoveTo(center);
            painter.Arc(center, radius, startAngle, endAngle);
            painter.LineTo(center);
            painter.ClosePath();
            painter.Fill();
            painter.Stroke();

            startAngle = endAngle;
        }
    }
}