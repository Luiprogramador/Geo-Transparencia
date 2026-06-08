using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public partial class MapClickController : MonoBehaviour
{
    // ===================================================================
    // 🎨 HEATMAP
    // ===================================================================

    // Converte risco (0=baixo, 1=alto) em cor — IGUAL à legenda do TelaSegInitializer
    private Color CorDoRisco(float t)
    {
        t = Mathf.Clamp01(t);
        Color c;
        if (t < 0.33f)
            c = Color.Lerp(Color.blue, Color.yellow, t / 0.33f);
        else if (t < 0.66f)
            c = Color.Lerp(Color.yellow, new Color(1f, 0.5f, 0f), (t - 0.33f) / 0.33f);
        else
            c = Color.Lerp(new Color(1f, 0.5f, 0f), Color.red, (t - 0.66f) / 0.34f);
        c.a = 1f;
        return c;
    }

    // Converte o texto de risco da API em 0..1 (pra escolher a cor do heatmap)
    private float RiscoTextoParaValor(string risk)
    {
        if (string.IsNullOrEmpty(risk)) return 0f;
        string r = risk.Trim().ToUpper();

        switch (r)
        {
            case "CRÍTICO":
            case "CRITICO":  return 1.0f;
            case "ALTO":     return 0.75f;
            case "MÉDIO":
            case "MEDIO":    return 0.5f;
            case "BAIXO":    return 0.25f;
            case "MÍNIMO":
            case "MINIMO":   return 0.1f;
            default:         return 0f;   // "Sem dados" → fica cinza
        }
    }

    private void PintarHeatmapInicial()
    {
        // 1. Começa todo mundo cinza (enquanto a API não responde)
        foreach (var kv in estadoElementos)
        {
            corRiscoEstado[kv.Key] = CorSemDados;
            AplicarTint(kv.Value, CorSemDados);
        }

        Debug.Log($"🎨 Heatmap inicial: {estadoElementos.Count} estados em cinza. Buscando risco real...");

        // 2. Busca o risco de CADA estado (respeitando filtros, se houver) e repinta
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

                    if (estadoElementos.TryGetValue(chave, out var el))
                        AplicarTint(el, cor);

                    Debug.Log($"🎨 {chave} pintado → risco '{risk}' ({valor})");
                }
            ));
        }
    }

    // ====== APLICA O TINT ======
    private void AplicarTint(VisualElement el, Color cor)
    {
        if (el == null) return;

        if (el is Image img)
            img.tintColor = cor;
        else
            el.style.unityBackgroundImageTintColor = cor;
    }

    // ====== BORDA ======
    private void AplicarBorda(VisualElement el, Color cor, float largura)
    {
        if (el == null) return;

        el.style.borderTopWidth    = largura;
        el.style.borderBottomWidth = largura;
        el.style.borderLeftWidth   = largura;
        el.style.borderRightWidth  = largura;

        el.style.borderTopColor    = cor;
        el.style.borderBottomColor = cor;
        el.style.borderLeftColor   = cor;
        el.style.borderRightColor  = cor;
    }

    private void RemoverBorda(VisualElement el)
    {
        if (el == null) return;

        el.style.borderTopWidth    = 0;
        el.style.borderBottomWidth = 0;
        el.style.borderLeftWidth   = 0;
        el.style.borderRightWidth  = 0;
    }
}
