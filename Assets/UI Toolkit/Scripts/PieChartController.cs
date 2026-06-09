using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UIElements;

public class PieChartController
{
    private UIDocument uiDoc;
    private VisualElement root;
    private PieChart pieChart;
    private VisualElement legendContainer;
    private string supabaseRestUrl;
    private string supabasePublishableKey;
    private MonoBehaviour coroutineStarter;

    public string selectedEstado { get; set; } = null;
    public List<int> selectedCrimeIds { get; set; } = new List<int>();
    public int? selectedAno { get; set; } = null;

    public PieChartController(UIDocument document, MonoBehaviour starter, string restUrl, string key)
    {
        uiDoc = document;
        coroutineStarter = starter;
        supabaseRestUrl = restUrl;
        supabasePublishableKey = key;
        root = uiDoc.rootVisualElement;

        var chartCanvas = root.Q<VisualElement>("ChartCanvas");
        if (chartCanvas == null)
        {
            Debug.LogError("PieChartController: ChartCanvas não encontrado!");
            return;
        }

        legendContainer = root.Q<VisualElement>("LegendContainer");
        if (legendContainer == null)
            Debug.LogWarning("PieChartController: LegendContainer não encontrado.");

        pieChart = new PieChart();
        chartCanvas.Add(pieChart);
    }

    public void Refresh()
    {
        if (uiDoc == null || pieChart == null || coroutineStarter == null) return;
        coroutineStarter.StartCoroutine(LoadPieChartData());
    }

    private IEnumerator LoadPieChartData()
    {
        var filters = new List<string>();

        if (!string.IsNullOrEmpty(selectedEstado) && selectedEstado != "Nenhum")
            filters.Add($"estado=eq.{Uri.EscapeDataString(selectedEstado)}");

        if (selectedCrimeIds != null && selectedCrimeIds.Count > 0 && selectedCrimeIds.Count < 20)
            filters.Add($"id_crime=in.({string.Join(",", selectedCrimeIds)})");

        if (selectedAno.HasValue)
            filters.Add($"ano=eq.{selectedAno.Value}");

        string filterStr = filters.Count > 0 ? "&" + string.Join("&", filters) : "";
        string resourcePath = $"vw_estado_crime_mesano?select=crime,total{filterStr}";

        bool completed = false;
        var crimeTotals = new Dictionary<string, int>();

        yield return SupabaseRestClient.Get(supabaseRestUrl, supabasePublishableKey, resourcePath, (status, body, err) =>
        {
            if (string.IsNullOrEmpty(err))
            {
                var pattern = @"""crime""\s*:\s*""([^""]+)""\s*,\s*""total""\s*:\s*(\d+)";
                foreach (Match m in Regex.Matches(body, pattern))
                    if (int.TryParse(m.Groups[2].Value, out int total))
                        crimeTotals[m.Groups[1].Value] = crimeTotals.GetValueOrDefault(m.Groups[1].Value) + total;
            }
            else Debug.LogError($"Erro pizza: {err}");
            completed = true;
        });
        yield return new WaitUntil(() => completed);

        var sorted = crimeTotals.OrderByDescending(kv => kv.Value).ToList();
        var values = new List<float>();
        var colors = new List<Color>();
        float total = sorted.Sum(kv => kv.Value);
        int totalCrimes = sorted.Count;

        for (int i = 0; i < totalCrimes; i++)
        {
            values.Add(sorted[i].Value);
            float hue = (float)i / totalCrimes;
            colors.Add(Color.HSVToRGB(hue, 0.8f, 0.9f));
        }

        pieChart.Values = values;
        pieChart.Colors = colors;
        UpdateLegend(sorted, total);
    }

    private void UpdateLegend(List<KeyValuePair<string, int>> data, float total)
    {
        if (legendContainer == null) return;
        legendContainer.Clear();
        if (total <= 0) return;

        int totalCrimes = data.Count;
        for (int i = 0; i < totalCrimes; i++)
        {
            var kvp = data[i];
            float percent = (kvp.Value / total) * 100f;
            float hue = (float)i / totalCrimes;
            Color cor = Color.HSVToRGB(hue, 0.8f, 0.9f);
            AddLegendItem(kvp.Key, cor, percent, kvp.Value);
        }
    }

    private void AddLegendItem(string label, Color color, float percent, int quantidade)
    {
        var item = new VisualElement();
        item.AddToClassList("legend-item");
        var colorBox = new VisualElement();
        colorBox.AddToClassList("legend-color");
        colorBox.style.backgroundColor = color;
        item.Add(colorBox);
        var labelElement = new Label($"{label} ({percent:F1}% - {quantidade} ocorrências)");
        labelElement.AddToClassList("legend-label");
        item.Add(labelElement);
        legendContainer.Add(item);
    }
}