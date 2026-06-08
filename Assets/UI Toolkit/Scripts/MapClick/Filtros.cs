using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class MapClickController : MonoBehaviour
{
    // ===================================================================
    // 🔍 FILTROS (tipos de crime + período)
    // ===================================================================

    // Filtros ativos vindos do TelaSegInitializer (null/vazio = sem filtro)
    private List<int> filtroCrimeIds = new List<int>();
    private DateTime? filtroDataInicio;
    private DateTime? filtroDataFim;

    /// <summary>
    /// Chamado pelo TelaSegInitializer quando o usuário aplica os filtros.
    /// Guarda os filtros e repinta o heatmap inteiro respeitando eles.
    /// </summary>
    public void AplicarFiltros(List<int> crimeIds, DateTime? inicio, DateTime? fim)
    {
        filtroCrimeIds   = crimeIds ?? new List<int>();
        filtroDataInicio = inicio;
        filtroDataFim    = fim;

        Debug.Log($"🔍 Filtros aplicados → crimes: [{string.Join(",", filtroCrimeIds)}] | " +
                  $"de {filtroDataInicio?.ToString("dd/MM/yyyy") ?? "—"} " +
                  $"até {filtroDataFim?.ToString("dd/MM/yyyy") ?? "—"}");

        // Repinta o mapa inteiro com os filtros
        RepintarHeatmapComFiltros();

        // Se tiver um estado selecionado, atualiza o painel dele também
        if (estadoAtualSelecionado != null)
        {
            var chave = estadoElementos.FirstOrDefault(x => x.Value == estadoAtualSelecionado).Key;
            if (chave != null && estadoParaId.TryGetValue(chave, out int idSel))
            {
                StartCoroutine(MapDataService.GetStateDetails(
                    restUrl, apiKey, idSel,
                    GetCrimeIdsOuNull(), filtroDataInicio, filtroDataFim,
                    OnDadosRecebidos
                ));
            }
        }
    }

    /// <summary>
    /// Repinta TODOS os estados aplicando os filtros atuais.
    /// </summary>
    private void RepintarHeatmapComFiltros()
    {
        foreach (var kv in estadoParaId)
        {
            string chave = kv.Key;
            int id = kv.Value;

            StartCoroutine(MapDataService.GetStateDetails(
                restUrl, apiKey, id,
                GetCrimeIdsOuNull(), filtroDataInicio, filtroDataFim,
                (total, topCrime, risk) =>
                {
                    float valor = RiscoTextoParaValor(risk);
                    Color cor = CorDoRisco(valor);
                    corRiscoEstado[chave] = cor;

                    // não repinta por cima do estado selecionado (mantém o branco suave)
                    if (estadoElementos.TryGetValue(chave, out var el) && el != estadoAtualSelecionado)
                        AplicarTint(el, cor);

                    Debug.Log($"🎨 [filtro] {chave} → risco '{risk}' ({valor})");
                }
            ));
        }
    }

    /// <summary>
    /// Retorna a lista de crimes, ou null se estiver vazia
    /// (assim o MapDataService não adiciona o filtro de crime).
    /// </summary>
    private List<int> GetCrimeIdsOuNull()
    {
        return (filtroCrimeIds != null && filtroCrimeIds.Count > 0) ? filtroCrimeIds : null;
    }
}
