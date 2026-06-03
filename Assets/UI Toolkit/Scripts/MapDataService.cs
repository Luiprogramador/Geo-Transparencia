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
        List<int> crimeIds, DateTime? dataInicio, DateTime? dataFim,
        Action<List<StateData>> onComplete)
    {
        string resourcePath = "vw_ocorrencias_estado?select=estado,total";
        Debug.Log($"[MapDataService] Query: {restUrl}{resourcePath}");

        bool completed = false;
        List<StateData> result = new List<StateData>();

        yield return SupabaseRestClient.Get(restUrl, key, resourcePath, (status, body, err) =>
        {
            if (!string.IsNullOrEmpty(err))
            {
                Debug.LogError($"[MapDataService] Erro: {err} (HTTP {status})");
                result = GetMockStateData();
            }
            else
            {
                result = ParseStateCounts(body);
                Debug.Log($"[MapDataService] Estados retornados: {result.Count}");
                foreach (var s in result) Debug.Log($"  {s.nome}: {s.ocorrencias}");
            }
            completed = true;
        });
        yield return new WaitUntil(() => completed);

        int total = result.Sum(x => x.ocorrencias);
        if (total > 0)
            foreach (var s in result) s.percentual = (float)s.ocorrencias / total;

        onComplete?.Invoke(result);
    }

    public static IEnumerator GetGeneralSummary(string restUrl, string key,
        List<int> crimeIds, DateTime? dataInicio, DateTime? dataFim,
        Action<int, string> onComplete)
    {
        string resourcePath = "vw_ocorrencias_crime?select=crime,total";
        Debug.Log($"[MapDataService] Resumo: {resourcePath}");

        bool completed = false;
        int total = 0;
        string topCrime = "";

        yield return SupabaseRestClient.Get(restUrl, key, resourcePath, (status, body, err) =>
        {
            if (string.IsNullOrEmpty(err))
            {
                var crimes = ParseCrimeCounts(body);
                total = crimes.Values.Sum();
                if (crimes.Count > 0)
                    topCrime = crimes.OrderByDescending(x => x.Value).First().Key;
                Debug.Log($"[MapDataService] Resumo: total={total}, topCrime={topCrime}");
            }
            else
            {
                Debug.LogError($"Erro resumo: {err}");
                total = 0;
                topCrime = "-";
            }
            completed = true;
        });
        yield return new WaitUntil(() => completed);
        onComplete?.Invoke(total, topCrime);
    }

    public static IEnumerator GetStateDetails(string restUrl, string key, int stateId,
        List<int> crimeIds, DateTime? dataInicio, DateTime? dataFim,
        Action<int, string, string> onComplete)
    {
        var filters = new List<string> { $"id_estado=eq.{stateId}" };
        if (crimeIds != null && crimeIds.Count > 0)
            filters.Add($"id_crime=in.({string.Join(",", crimeIds)})");
        if (dataInicio.HasValue)
            filters.Add($"data_hora=gte.{dataInicio.Value:yyyy-MM-dd}");
        if (dataFim.HasValue)
            filters.Add($"data_hora=lte.{dataFim.Value:yyyy-MM-dd}");

        string filterStr = "&" + string.Join("&", filters);
        string resourcePath = $"vw_ocorrencias?select=crime,count()&group=crime{filterStr}";
        Debug.Log($"[MapDataService] Detalhes estado {stateId}: {resourcePath}");

        bool completed = false;
        int total = 0;
        string topCrime = "";
        string risk = "Médio";

        yield return SupabaseRestClient.Get(restUrl, key, resourcePath, (status, body, err) =>
        {
            if (string.IsNullOrEmpty(err))
            {
                var crimes = ParseCrimeCounts(body);
                total = crimes.Values.Sum();
                if (crimes.Count > 0)
                    topCrime = crimes.OrderByDescending(x => x.Value).First().Key;
                risk = GetRiskLevel(total);
            }
            else
            {
                Debug.LogError($"Erro detalhes estado: {err}");
                total = 0;
                topCrime = "-";
                risk = "Sem dados";
            }
            completed = true;
        });
        yield return new WaitUntil(() => completed);
        onComplete?.Invoke(total, topCrime, risk);
    }

    private static List<StateData> ParseStateCounts(string json)
    {
        var list = new List<StateData>();
        if (string.IsNullOrEmpty(json)) return list;
        MatchCollection matches = Regex.Matches(json, "\"estado\"\\s*:\\s*\"([^\"]+)\".*?\"total\"\\s*:\\s*(\\d+)");
        foreach (Match m in matches)
            if (int.TryParse(m.Groups[2].Value, out int count))
                list.Add(new StateData { nome = m.Groups[1].Value, ocorrencias = count });
        return list;
    }

    private static Dictionary<string, int> ParseCrimeCounts(string json)
    {
        var dict = new Dictionary<string, int>();
        if (string.IsNullOrEmpty(json)) return dict;
        MatchCollection matches = Regex.Matches(json, "\"crime\"\\s*:\\s*\"([^\"]+)\".*?\"total\"\\s*:\\s*(\\d+)");
        foreach (Match m in matches)
            if (int.TryParse(m.Groups[2].Value, out int count))
                dict[m.Groups[1].Value] = count;
        return dict;
    }

    private static string GetRiskLevel(int total)
    {
        if (total > 50000) return "CRÍTICO";
        if (total > 20000) return "ALTO";
        if (total > 5000) return "MÉDIO";
        if (total > 1000) return "BAIXO";
        return "MÍNIMO";
    }

    private static List<StateData> GetMockStateData()
    {
        return new List<StateData>
        {
            new StateData { nome = "Distrito Federal", ocorrencias = 4111 }
        };
    }
}