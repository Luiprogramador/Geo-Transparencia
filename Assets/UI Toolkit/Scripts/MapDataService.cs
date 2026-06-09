using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

public static class MapDataService
{
    [Serializable]
    public class StateData
    {
        public string nome;
        public int ocorrencias;
        public float percentual;
    }

    public static IEnumerator GetOccurrencesByState(string restUrl, string key,
        List<int> crimeIds, int? ano,
        Action<List<StateData>> onComplete)
    {
        bool hasCrime = crimeIds != null && crimeIds.Count > 0 && crimeIds.Count < 20;
        bool hasYear = ano.HasValue;
        string query = "";

        if (!hasCrime && !hasYear)
        {
            query = "vw_ocorrencias_estado?select=estado,total";
            bool done = false;
            List<StateData> result = new List<StateData>();
            yield return SupabaseRestClient.Get(restUrl, key, query, (status, body, err) =>
            {
                if (string.IsNullOrEmpty(err)) result = ParseStateTotals(body);
                else Debug.LogError($"Erro: {err}");
                done = true;
            });
            yield return new WaitUntil(() => done);
            int total = result.Sum(x => x.ocorrencias);
            if (total > 0) foreach (var s in result) s.percentual = (float)s.ocorrencias / total;
            onComplete?.Invoke(result);
            yield break;
        }

        var filters = new List<string>();
        if (hasCrime) filters.Add($"id_crime=in.({string.Join(",", crimeIds)})");
        if (hasYear) filters.Add($"ano=eq.{ano.Value}");
        string filterStr = filters.Count > 0 ? "&" + string.Join("&", filters) : "";
        query = $"vw_estado_crime_mesano?select=estado,total{filterStr}";

        bool completed = false;
        List<StateData> raw = new List<StateData>();
        yield return SupabaseRestClient.Get(restUrl, key, query, (status, body, err) =>
        {
            if (string.IsNullOrEmpty(err)) raw = ParseStateTotals(body);
            else Debug.LogError($"Erro: {err}");
            completed = true;
        });
        yield return new WaitUntil(() => completed);
        var grouped = raw.GroupBy(s => s.nome).Select(g => new StateData { nome = g.Key, ocorrencias = g.Sum(x => x.ocorrencias) }).ToList();
        int total2 = grouped.Sum(g => g.ocorrencias);
        if (total2 > 0) foreach (var g in grouped) g.percentual = (float)g.ocorrencias / total2;
        onComplete?.Invoke(grouped);
    }

    public static IEnumerator GetGeneralSummary(string restUrl, string key,
        List<int> crimeIds, int? ano,
        Action<int, string> onComplete)
    {
        bool hasCrime = crimeIds != null && crimeIds.Count > 0 && crimeIds.Count < 20;
        bool hasYear = ano.HasValue;
        string query = "";

        if (!hasCrime && !hasYear)
        {
            query = "vw_ocorrencias_crime?select=crime,total";
            bool done = false;
            int total = 0;
            string top = "Nenhum";
            yield return SupabaseRestClient.Get(restUrl, key, query, (status, body, err) =>
            {
                if (string.IsNullOrEmpty(err))
                {
                    var crimes = ParseCrimeTotals(body);
                    total = crimes.Values.Sum();
                    if (crimes.Count > 0) top = crimes.OrderByDescending(x => x.Value).First().Key;
                }
                else Debug.LogError($"Erro: {err}");
                done = true;
            });
            yield return new WaitUntil(() => done);
            onComplete?.Invoke(total, top);
            yield break;
        }

        var filters = new List<string>();
        if (hasCrime) filters.Add($"id_crime=in.({string.Join(",", crimeIds)})");
        if (hasYear) filters.Add($"ano=eq.{ano.Value}");
        string filterStr = filters.Count > 0 ? "&" + string.Join("&", filters) : "";
        query = $"vw_crime_mesano?select=crime,total{filterStr}";

        bool completed = false;
        int total2 = 0;
        string top2 = "Nenhum";
        yield return SupabaseRestClient.Get(restUrl, key, query, (status, body, err) =>
        {
            if (string.IsNullOrEmpty(err))
            {
                var crimes = ParseCrimeTotals(body);
                total2 = crimes.Values.Sum();
                if (crimes.Count > 0) top2 = crimes.OrderByDescending(x => x.Value).First().Key;
            }
            else Debug.LogError($"Erro: {err}");
            completed = true;
        });
        yield return new WaitUntil(() => completed);
        onComplete?.Invoke(total2, top2);
    }

    public static IEnumerator GetStateDetailsByName(string restUrl, string key, string estadoNome,
        List<int> crimeIds, int? ano,
        Action<int, string, string> onComplete)
    {
        var filters = new List<string> { $"estado=eq.{Uri.EscapeDataString(estadoNome)}" };
        if (crimeIds != null && crimeIds.Count > 0 && crimeIds.Count < 20) filters.Add($"id_crime=in.({string.Join(",", crimeIds)})");
        if (ano.HasValue) filters.Add($"ano=eq.{ano.Value}");
        string filterStr = "&" + string.Join("&", filters);
        string query = $"vw_estado_crime_mesano?select=crime,total{filterStr}";

        bool completed = false;
        int total = 0;
        string top = "Nenhum";
        string risk = "Mínimo";
        yield return SupabaseRestClient.Get(restUrl, key, query, (status, body, err) =>
        {
            if (string.IsNullOrEmpty(err))
            {
                var crimes = ParseCrimeTotals(body);
                total = crimes.Values.Sum();
                if (crimes.Count > 0) top = crimes.OrderByDescending(x => x.Value).First().Key;
                if (total > 50000) risk = "CRÍTICO";
                else if (total > 20000) risk = "ALTO";
                else if (total > 5000) risk = "MÉDIO";
                else if (total > 1000) risk = "BAIXO";
                else risk = "MÍNIMO";
            }
            else Debug.LogError($"Erro: {err}");
            completed = true;
        });
        yield return new WaitUntil(() => completed);
        onComplete?.Invoke(total, top, risk);
    }

    public static IEnumerator GetMonthlyData(string restUrl, string key, string estadoNome,
        List<int> crimeIds, int? ano, Action<List<int>> onComplete)
    {
        var filters = new List<string>();
        if (!string.IsNullOrEmpty(estadoNome))
            filters.Add($"estado=eq.{Uri.EscapeDataString(estadoNome)}");
        if (crimeIds != null && crimeIds.Count > 0 && crimeIds.Count < 20)
            filters.Add($"id_crime=in.({string.Join(",", crimeIds)})");
        if (ano.HasValue)
            filters.Add($"ano=eq.{ano.Value}");

        string filterStr = filters.Count > 0 ? "&" + string.Join("&", filters) : "";
        string query = $"vw_estado_crime_mesano?select=mes,total{filterStr}";

        bool completed = false;
        int[] monthly = new int[12];
        yield return SupabaseRestClient.Get(restUrl, key, query, (status, body, err) =>
        {
            if (string.IsNullOrEmpty(err))
            {
                var pattern = @"""mes""\s*:\s*(\d+)\s*,\s*""total""\s*:\s*(\d+)";
                foreach (Match m in Regex.Matches(body, pattern))
                {
                    if (int.TryParse(m.Groups[1].Value, out int mes) && int.TryParse(m.Groups[2].Value, out int total))
                        if (mes >= 1 && mes <= 12) monthly[mes - 1] += total;
                }
            }
            else Debug.LogError($"Erro dados mensais: {err}");
            completed = true;
        });
        yield return new WaitUntil(() => completed);
        onComplete?.Invoke(new List<int>(monthly));
    }

    private static List<StateData> ParseStateTotals(string json)
    {
        var list = new List<StateData>();
        var pattern = @"""estado""\s*:\s*""([^""]+)""\s*,\s*""total""\s*:\s*(\d+)";
        foreach (Match m in Regex.Matches(json, pattern))
            if (int.TryParse(m.Groups[2].Value, out int total))
                list.Add(new StateData { nome = m.Groups[1].Value, ocorrencias = total });
        return list;
    }

    private static Dictionary<string, int> ParseCrimeTotals(string json)
    {
        var dict = new Dictionary<string, int>();
        var pattern = @"""crime""\s*:\s*""([^""]+)""\s*,\s*""total""\s*:\s*(\d+)";
        foreach (Match m in Regex.Matches(json, pattern))
            if (int.TryParse(m.Groups[2].Value, out int total))
                dict[m.Groups[1].Value] = dict.GetValueOrDefault(m.Groups[1].Value) + total;
        return dict;
    }
}