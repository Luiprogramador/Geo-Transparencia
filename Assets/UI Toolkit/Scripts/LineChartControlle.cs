using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class LineChartController
{
    private VisualElement container;
    private LineChartVisualElement chartElement;
    private List<Label> monthLabels = new List<Label>();
    private List<Label> valueLabels = new List<Label>();
    private Label yAxisTitle;

    public LineChartController(VisualElement parentContainer)
    {
        container = parentContainer;
        container.Clear();
        container.style.flexDirection = FlexDirection.Column;
        container.style.paddingLeft = 0;
        container.style.paddingRight = 0;
        container.style.paddingTop = 0;
        container.style.paddingBottom = 0;

        chartElement = new LineChartVisualElement();
        chartElement.style.flexGrow = 1;
        chartElement.style.minHeight = 250;
        container.Add(chartElement);

        CreateLabels();
    }

    private void CreateLabels()
    {
        yAxisTitle = new Label("OCORRÊNCIAS");
        yAxisTitle.style.position = Position.Absolute;
        yAxisTitle.style.fontSize = 16;
        yAxisTitle.style.color = new Color(0.9f, 0.9f, 0.9f);
        yAxisTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
        yAxisTitle.style.width = 120;
        yAxisTitle.style.height = 24;
        yAxisTitle.style.rotate = new Rotate(-90);
        container.Add(yAxisTitle);

        string[] months = { "Jan", "Fev", "Mar", "Abr", "Mai", "Jun", "Jul", "Ago", "Set", "Out", "Nov", "Dez" };
        for (int i = 0; i < 12; i++)
        {
            var lbl = new Label(months[i]);
            lbl.style.position = Position.Absolute;
            lbl.style.fontSize = 16;
            lbl.style.color = Color.white;
            lbl.style.unityTextAlign = TextAnchor.MiddleCenter;
            lbl.style.width = 45;
            lbl.style.height = 24;
            container.Add(lbl);
            monthLabels.Add(lbl);
        }

        for (int i = 0; i <= 4; i++)
        {
            var lbl = new Label("");
            lbl.style.position = Position.Absolute;
            lbl.style.fontSize = 16;
            lbl.style.color = Color.white;
            lbl.style.unityTextAlign = TextAnchor.MiddleRight;
            lbl.style.width = 70;
            lbl.style.height = 24;
            container.Add(lbl);
            valueLabels.Add(lbl);
        }
    }

    public void SetData(List<int> values)
    {
        chartElement.SetData(values);
        UpdateLabelsPosition();
        UpdateValueLabels(values);
    }

    private void UpdateLabelsPosition()
    {
        container.schedule.Execute(() =>
        {
            var rect = chartElement.contentRect;
            if (rect.width < 50 || rect.height < 50) return;

            float leftMargin = 80;
            float rightMargin = 20;
            float topMargin = 30;
            float bottomMargin = 40;
            float graphWidth = rect.width - leftMargin - rightMargin;
            float graphHeight = rect.height - topMargin - bottomMargin;

            yAxisTitle.style.left = -40;
            yAxisTitle.style.top = (rect.height / 2) - 30;

            float stepX = graphWidth / 11f;
            for (int i = 0; i < 12; i++)
            {
                float x = leftMargin + i * stepX - 22;
                float y = rect.height - bottomMargin + 5;
                monthLabels[i].style.left = x;
                monthLabels[i].style.top = y;
            }

            float stepY = graphHeight / 4f;
            for (int i = 0; i <= 4; i++)
            {
                float y = rect.height - bottomMargin - i * stepY - 12;
                valueLabels[i].style.left = 8;
                valueLabels[i].style.top = y;
            }
        }).ExecuteLater(50);
    }

    private void UpdateValueLabels(List<int> values)
    {
        if (values == null || values.Count == 0) return;
        int max = 1;
        foreach (var v in values) if (v > max) max = v;
        if (max == 0) max = 1;

        for (int i = 0; i <= 4; i++)
        {
            int val = Mathf.RoundToInt((float)i / 4f * max);
            valueLabels[i].text = val.ToString("N0");
        }
    }

    private class LineChartVisualElement : VisualElement
    {
        private List<int> monthlyTotals = new List<int>();
        private int maxValue = 1;

        public LineChartVisualElement()
        {
            generateVisualContent += OnGenerateVisualContent;
        }

        public void SetData(List<int> values)
        {
            monthlyTotals = values ?? new List<int>();
            while (monthlyTotals.Count < 12) monthlyTotals.Add(0);
            maxValue = 0;
            foreach (var v in monthlyTotals) if (v > maxValue) maxValue = v;
            if (maxValue <= 0) maxValue = 1;
            MarkDirtyRepaint();
        }

        private void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            var rect = contentRect;
            if (rect.width < 50 || rect.height < 50) return;

            var painter = mgc.painter2D;
            painter.lineWidth = 1.2f;

            painter.fillColor = new Color(0.1f, 0.12f, 0.18f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(0, 0));
            painter.LineTo(new Vector2(rect.width, 0));
            painter.LineTo(new Vector2(rect.width, rect.height));
            painter.LineTo(new Vector2(0, rect.height));
            painter.ClosePath();
            painter.Fill();

            float leftMargin = 80;
            float rightMargin = 20;
            float topMargin = 30;
            float bottomMargin = 40;

            float graphLeft = leftMargin;
            float graphRight = rect.width - rightMargin;
            float graphTop = topMargin;
            float graphBottom = rect.height - bottomMargin;

            painter.strokeColor = new Color(0.6f, 0.6f, 0.7f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(graphLeft, graphTop));
            painter.LineTo(new Vector2(graphLeft, graphBottom));
            painter.LineTo(new Vector2(graphRight, graphBottom));
            painter.Stroke();

            painter.strokeColor = new Color(0.25f, 0.28f, 0.35f);
            float stepY = (graphBottom - graphTop) / 4f;
            for (int i = 1; i <= 4; i++)
            {
                float y = graphBottom - i * stepY;
                painter.BeginPath();
                painter.MoveTo(new Vector2(graphLeft, y));
                painter.LineTo(new Vector2(graphRight, y));
                painter.Stroke();
            }

            if (monthlyTotals.Count == 12)
            {
                float stepX = (graphRight - graphLeft) / 11f;
                Vector2[] points = new Vector2[12];
                for (int i = 0; i < 12; i++)
                {
                    float x = graphLeft + i * stepX;
                    float y = graphBottom - (monthlyTotals[i] / (float)maxValue) * (graphBottom - graphTop);
                    points[i] = new Vector2(x, y);
                }

                painter.lineWidth = 2.5f;
                painter.strokeColor = new Color(0.2f, 0.6f, 1f);
                painter.BeginPath();
                painter.MoveTo(points[0]);
                for (int i = 1; i < 12; i++) painter.LineTo(points[i]);
                painter.Stroke();

                painter.fillColor = new Color(0.2f, 0.6f, 1f);
                foreach (var p in points)
                {
                    DrawCircle(painter, p, 5f);
                }
            }
        }

        private void DrawCircle(Painter2D painter, Vector2 center, float radius)
        {
            int segments = 20;
            painter.BeginPath();
            for (int i = 0; i <= segments; i++)
            {
                float ang = (float)i / segments * Mathf.PI * 2;
                Vector2 p = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius;
                if (i == 0) painter.MoveTo(p);
                else painter.LineTo(p);
            }
            painter.ClosePath();
            painter.Fill();
        }
    }
}