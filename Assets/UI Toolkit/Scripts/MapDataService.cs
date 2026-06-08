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
        // Essa view JÁ vem com a coluna "total" agregada
        string resourcePath = "vw_ocorrencias_crime?select=crime,total";
        Debug.Log($"[MapDataService] Resumo: {resourcePath}");

        bool completed = false;
        int total = 0;
        string topCrime = "";

        yield return SupabaseRestClient.Get(restUrl, key, resourcePath, (status, body, err) =>
        {
            if (string.IsNullOrEmpty(err))
            {
                var crimes = ParseCrimeCounts(body); // usa "total" pronto da view
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
        string resourcePath = $"vw_ocorrencias?select=crime{filterStr}";

        Debug.Log($"[MapDataService] Detalhes estado {stateId}: {resourcePath}");

        bool completed = false;
        int total = 0;
        string topCrime = "Nenhum";
        string risk = "MÍNIMO";

        yield return SupabaseRestClient.Get(restUrl, key, resourcePath, (status, body, err) =>
        {
            // 1) Erro de rede / HTTP
            if (!string.IsNullOrEmpty(err))
            {
                Debug.LogError($"[MapDataService] Erro detalhes estado {stateId}: {err} (HTTP {status})");
                total = 0;
                topCrime = "-";
                risk = "Sem dados";
                completed = true;
                return;
            }

            // 2) Body nulo/vazio (defensivo)
            if (string.IsNullOrWhiteSpace(body))
            {
                Debug.LogWarning($"[MapDataService] Body vazio no estado {stateId}.");
                total = 0;
                topCrime = "Nenhum";
                risk = "MÍNIMO";
                completed = true;
                return;
            }

            // 3) Array vazio "[]" → estado sem ocorrências. É SUCESSO.
            string trimmed = body.Trim();
            if (trimmed == "[]")
            {
                Debug.Log($"[MapDataService] Estado {stateId} sem ocorrências (array vazio).");
                total = 0;
                topCrime = "Nenhum";
                risk = "MÍNIMO";
                completed = true;
                return;
            }

            // 4) Sucesso com dados → conta as linhas
            var crimes = ParseCrimeRows(body);
            total = crimes.Values.Sum();

            if (crimes.Count > 0)
                topCrime = crimes.OrderByDescending(x => x.Value).First().Key;
            else
                topCrime = "Nenhum";

            risk = GetRiskLevel(total);

            Debug.Log($"[MapDataService] Estado {stateId}: total={total}, topCrime={topCrime}, risco={risk}");
            completed = true;
        });

        yield return new WaitUntil(() => completed);
        onComplete?.Invoke(total, topCrime, risk);
    }

    // ===================================================================
    // 🔎 LISTA DE CRIMES (pra popular o popup de filtro)
    // ===================================================================
    public static IEnumerator GetCrimes(string restUrl, string key,
        Action<List<(int id, string nome)>> onComplete)
    {
        string resourcePath = "vw_ocorrencias_crime?select=crime"; // ajuste se tiver tabela própria
        Debug.Log($"[MapDataService] Buscando crimes: {resourcePath}");

        bool completed = false;
        List<(int, string)> result = new List<(int, string)>();

        yield return SupabaseRestClient.Get(restUrl, key, resourcePath, (status, body, err) =>
        {
            if (!string.IsNullOrEmpty(err))
            {
                Debug.LogError($"[MapDataService] Erro ao buscar crimes: {err} (HTTP {status}). Usando lista fixa.");
                result = GetCrimesFixos();
            }
            else
            {
                // Como a view não tem ID, usamos a lista fixa (que tem os IDs corretos)
                result = GetCrimesFixos();
                Debug.Log($"[MapDataService] {result.Count} crimes disponíveis (lista fixa).");
            }
            completed = true;
        });

        yield return new WaitUntil(() => completed);
        onComplete?.Invoke(result);
    }

    // Lista fixa de crimes — IDs batem com id_crime da tabela vw_ocorrencias
    private static List<(int id, string nome)> GetCrimesFixos()
    {
        return new List<(int, string)>
        {
            (1,  "Estupro"),
            (2,  "Estupro de Vulnerável"),
            (3,  "Feminicídio"),
            (4,  "Furto a Transeunte"),
            (5,  "Furto em Veículo"),
            (6,  "Homicídio"),
            (7,  "Latrocínio"),
            (8,  "Lesão Corporal Seguida de Morte"),
            (9,  "Localização de Veículo Furtado ou Roubado"),
            (10, "Posse/Porte de Arma"),
            (11, "Roubo a Transeunte"),
            (12, "Roubo de Veículo"),
            (13, "Roubo em Coletivo"),
            (14, "Roubo em Comércio"),
            (15, "Roubo em Residência"),
            (16, "Tentativa de Feminicídio"),
            (17, "Tentativa de Homicídio"),
            (18, "Tentativa de Latrocínio"),
            (19, "Tráfico de Drogas"),
            (20, "Uso e Porte de Drogas"),
        };
    }

    // Parser para views que JÁ trazem "total" agregado
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

    // Parser para views que JÁ trazem "total" agregado (ex: vw_ocorrencias_crime)
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

    // Parser para quando NÃO há agregação: conta quantas linhas tem de cada crime
    private static Dictionary<string, int> ParseCrimeRows(string json)
    {
        var dict = new Dictionary<string, int>();
        if (string.IsNullOrEmpty(json)) return dict;

        MatchCollection matches = Regex.Matches(json, "\"crime\"\\s*:\\s*\"([^\"]+)\"");
        foreach (Match m in matches)
        {
            string nome = m.Groups[1].Value;
            dict[nome] = dict.ContainsKey(nome) ? dict[nome] + 1 : 1;
        }
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
