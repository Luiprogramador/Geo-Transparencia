using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public class MapClickController : MonoBehaviour
{
    // ===================================================================
    // 🔧 CONFIGURAÇÃO
    // ===================================================================
    public string restUrl;
    public string apiKey;
    public Action<int, string> OnResumoAtualizado; // total, topCrime
    public Action<string> OnEstadoSelecionado;      // nome do estado ou null para "Brasil"

    // Filtros ativos (para consulta de detalhes)
    private List<int> filtroCrimeIds = new List<int>();
    private int? filtroAno = null;

    // Estado atual do mapa
    private VisualElement estadoAtualSelecionado = null;
    private string estadoAtualNome = null;

    // Elemento container do mapa (para validar área de clique)
    private VisualElement mapContainer;

    // ===================================================================
    // 🗺️ MAPEAMENTO DE ESTADOS
    // ===================================================================
    private Dictionary<string, int> estadoParaId = new Dictionary<string, int>()
    {
        {"Acre", 1}, {"Amapa", 2}, {"Amazonas", 3}, {"Para", 4},
        {"Rondonia", 5}, {"Roraima", 6}, {"Tocantins", 7}, {"Alagoas", 8},
        {"Bahia", 9}, {"Ceara", 10}, {"Maranhao", 11}, {"Paraiba", 12},
        {"Pernambuco", 13}, {"Piaui", 14}, {"RioGrandeNorte", 15}, {"Sergipe", 16},
        {"DistritoFederal", 17}, {"Goias", 18}, {"MatoGrosso", 19}, {"MatoGrossoSul", 20},
        {"EspiritoSanto", 21}, {"MinasGerais", 22}, {"RioJaneiro", 23}, {"SaoPaulo", 24},
        {"Parana", 25}, {"RioGrandeSul", 26}, {"SantaCatarina", 27}
    };

    private Dictionary<string, string> estadoNomes = new Dictionary<string, string>()
    {
        {"Acre", "Acre"}, {"Alagoas", "Alagoas"}, {"Amapa", "Amapá"}, {"Amazonas", "Amazonas"},
        {"Bahia", "Bahia"}, {"Ceara", "Ceará"}, {"DistritoFederal", "Distrito Federal"},
        {"EspiritoSanto", "Espírito Santo"}, {"Goias", "Goiás"}, {"Maranhao", "Maranhão"},
        {"MatoGrosso", "Mato Grosso"}, {"MatoGrossoSul", "Mato Grosso do Sul"},
        {"MinasGerais", "Minas Gerais"}, {"Para", "Pará"}, {"Paraiba", "Paraíba"},
        {"Parana", "Paraná"}, {"Pernambuco", "Pernambuco"}, {"Piaui", "Piauí"},
        {"RioGrandeNorte", "Rio Grande do Norte"}, {"RioGrandeSul", "Rio Grande do Sul"},
        {"RioJaneiro", "Rio de Janeiro"}, {"Rondonia", "Rondônia"}, {"Roraima", "Roraima"},
        {"SantaCatarina", "Santa Catarina"}, {"SaoPaulo", "São Paulo"}, {"Sergipe", "Sergipe"},
        {"Tocantins", "Tocantins"}
    };

    // Elementos da UI
    private Dictionary<string, VisualElement> estadoElementos = new Dictionary<string, VisualElement>();
    private VisualElement root;

    // Labels do painel de estado
    private Label labelNome;
    private Label labelRisco;
    private Label labelOcorrencias;

    // ===================================================================
    // 🎮 MÉTODOS PÚBLICOS
    // ===================================================================
    public void Configurar(string url, string key)
    {
        restUrl = url;
        apiKey = key;
        Debug.Log($"[MapClick] Configurado");
    }

    public void AplicarFiltros(List<int> crimeIds, int? ano)
    {
        filtroCrimeIds = crimeIds ?? new List<int>();
        filtroAno = ano;

        if (estadoAtualSelecionado != null && estadoAtualNome != null)
        {
            AtualizarDetalhesEstado(estadoAtualNome);
        }
        else
        {
            CarregarResumoGeral();
        }
    }

    public List<int> GetCrimeIdsOuNull() => (filtroCrimeIds != null && filtroCrimeIds.Count > 0) ? filtroCrimeIds : null;
    public int? GetAno() => filtroAno;
    public string GetEstadoAtual() => estadoAtualNome;

    // ===================================================================
    // 🖱️ CLIQUE NO MAPA
    // ===================================================================
    private void OnEnable() => StartCoroutine(SetupClicks());
    private void OnDisable() { if (root != null) root.UnregisterCallback<ClickEvent>(OnMapaClicked); }

    private IEnumerator SetupClicks()
    {
        root = GetComponent<UIDocument>().rootVisualElement;
        yield return null;
        yield return null;

        int tentativas = 0;
        while (root.Q<VisualElement>("SaoPaulo") == null && tentativas < 30)
        {
            yield return null;
            tentativas++;
        }

        // Obtém o container do mapa (usado para validar área de clique)
        mapContainer = root.Q<VisualElement>("mapContainer");
        if (mapContainer == null)
            Debug.LogWarning("[MapClick] 'mapContainer' não encontrado. A validação de área pode falhar.");

        estadoElementos.Clear();
        foreach (var estado in estadoParaId)
        {
            VisualElement elemento = root.Q<VisualElement>(estado.Key);
            if (elemento != null)
            {
                estadoElementos[estado.Key] = elemento;
                elemento.pickingMode = PickingMode.Ignore;
            }
            else
                Debug.LogWarning($"⚠️ Elemento '{estado.Key}' não encontrado!");
        }

        Debug.Log($"[MapClick] {estadoElementos.Count} estados registrados.");

        root.pickingMode = PickingMode.Position;
        root.RegisterCallback<ClickEvent>(OnMapaClicked);

        labelNome = root.Q<Label>("stateTitle");
        labelRisco = root.Q<Label>("riskBadge");
        labelOcorrencias = root.Q<Label>("stateValue");

        if (labelNome == null) Debug.LogWarning("Label 'stateTitle' não encontrado!");
        if (labelRisco == null) Debug.LogWarning("Label 'riskBadge' não encontrado!");
        if (labelOcorrencias == null) Debug.LogWarning("Label 'stateValue' não encontrado!");

        CarregarResumoGeral();
    }

    private void OnMapaClicked(ClickEvent evt)
    {
        // Valida se o clique foi dentro da área do mapa (container)
        if (!IsClickInsideMapArea(evt))
            return;

        Vector2 clickPos = evt.position;
        string acertou = null;

        var ordem = estadoElementos.OrderBy(kv => kv.Key == "DistritoFederal" ? 0 : 1).ToList();

        foreach (var kv in ordem)
        {
            string chave = kv.Key;
            VisualElement elemento = kv.Value;
            Texture2D tex = GetTextura(elemento);
            if (tex == null || !tex.isReadable) continue;

            Vector2 local = elemento.WorldToLocal(clickPos);
            var rect = elemento.contentRect;
            if (local.x < 0 || local.y < 0 || local.x > rect.width || local.y > rect.height)
                continue;

            float texAspect = (float)tex.width / tex.height;
            float rectAspect = rect.width / rect.height;
            float drawW = rect.width, drawH = rect.height;
            float offsetX = 0, offsetY = 0;

            if (texAspect > rectAspect)
            {
                drawH = rect.width / texAspect;
                offsetY = (rect.height - drawH) / 2f;
            }
            else
            {
                drawW = rect.height * texAspect;
                offsetX = (rect.width - drawW) / 2f;
            }

            float lx = local.x - offsetX;
            float ly = local.y - offsetY;
            if (lx < 0 || ly < 0 || lx > drawW || ly > drawH) continue;

            int px = Mathf.Clamp((int)(lx / drawW * tex.width), 0, tex.width - 1);
            int py = Mathf.Clamp((int)(ly / drawH * tex.height), 0, tex.height - 1);
            py = tex.height - 1 - py;

            if (TemPixelOpaco(tex, px, py))
            {
                acertou = chave;
                break;
            }
        }

        if (acertou != null)
            EstadoSelecionado(acertou);
        else
            CarregarResumoGeral(); // clicou no mapa mas fora dos estados
    }

    /// <summary>Verifica se o clique ocorreu dentro do container do mapa</summary>
    private bool IsClickInsideMapArea(ClickEvent evt)
    {
        if (mapContainer == null) return true; // fallback: processa sempre

        Vector2 clickPos = evt.position;
        var worldBound = mapContainer.worldBound;
        return worldBound.Contains(clickPos);
    }

    private Texture2D GetTextura(VisualElement elemento)
    {
        if (elemento is Image img && img.image is Texture2D t) return t;
        var bg = elemento.resolvedStyle.backgroundImage;
        return bg.texture as Texture2D;
    }

    private bool TemPixelOpaco(Texture2D tex, int px, int py, int raio = 2, float limite = 0.01f)
    {
        for (int x = -raio; x <= raio; x++)
            for (int y = -raio; y <= raio; y++)
            {
                int sx = Mathf.Clamp(px + x, 0, tex.width - 1);
                int sy = Mathf.Clamp(py + y, 0, tex.height - 1);
                if (tex.GetPixel(sx, sy).a > limite)
                    return true;
            }
        return false;
    }

    private void EstadoSelecionado(string nomeChave)
    {
        string nomeBonito = estadoNomes.ContainsKey(nomeChave) ? estadoNomes[nomeChave] : nomeChave;

        // ✅ Otimização: se já estiver selecionado o mesmo estado, não recarrega
        if (estadoAtualNome == nomeBonito)
        {
            Debug.Log($"[MapClick] Estado '{nomeBonito}' já selecionado. Ignorando recarga.");
            return;
        }

        Debug.Log($"Você selecionou: {nomeBonito}");

        if (estadoElementos.TryGetValue(nomeChave, out var el))
            estadoAtualSelecionado = el;
        estadoAtualNome = nomeBonito;

        OnEstadoSelecionado?.Invoke(nomeBonito);
        AtualizarDetalhesEstado(nomeBonito);
    }

    private void AtualizarDetalhesEstado(string nomeEstado)
    {
        if (labelNome != null) labelNome.text = nomeEstado;
        if (labelOcorrencias != null) labelOcorrencias.text = "—";
        if (labelRisco != null) labelRisco.text = "—";

        StartCoroutine(MapDataService.GetStateDetailsByName(
            restUrl, apiKey, nomeEstado,
            GetCrimeIdsOuNull(), filtroAno,
            OnDadosEstadoRecebidos
        ));
    }

    private void OnDadosEstadoRecebidos(int total, string topCrime, string risk)
    {
        Debug.Log($"✅ Dados do estado: total={total}, topCrime={topCrime}, risk={risk}");

        if (labelOcorrencias != null)
            labelOcorrencias.text = total > 0 ? total.ToString("N0") : "0";
        if (labelRisco != null)
            labelRisco.text = string.IsNullOrEmpty(risk) ? "Sem dados" : risk;

        OnResumoAtualizado?.Invoke(total, topCrime);
    }

    private void CarregarResumoGeral()
    {
        // ✅ Otimização: se já estiver no modo resumo geral (Brasil), não recarrega
        if (estadoAtualNome == null)
        {
            Debug.Log("[MapClick] Já está no resumo geral. Ignorando recarga.");
            return;
        }

        Debug.Log("[MapClick] Carregando resumo geral (Brasil)");
        estadoAtualSelecionado = null;
        estadoAtualNome = null;

        OnEstadoSelecionado?.Invoke(null); // null significa "Brasil"

        if (labelNome != null) labelNome.text = "Brasil";
        if (labelOcorrencias != null) labelOcorrencias.text = "—";
        if (labelRisco != null) labelRisco.text = "—";

        StartCoroutine(MapDataService.GetGeneralSummary(
            restUrl, apiKey,
            GetCrimeIdsOuNull(), filtroAno,
            OnResumoGeralRecebido
        ));
    }

    private void OnResumoGeralRecebido(int total, string topCrime)
    {
        Debug.Log($"✅ Resumo geral: total={total}, topCrime={topCrime}");

        string risk = CalcularRiscoNacional(total);

        if (labelOcorrencias != null)
            labelOcorrencias.text = total > 0 ? total.ToString("N0") : "0";
        if (labelRisco != null)
            labelRisco.text = string.IsNullOrEmpty(risk) ? "Sem dados" : risk;

        OnResumoAtualizado?.Invoke(total, topCrime);
    }

    private string CalcularRiscoNacional(int total)
    {
        if (total > 1000000) return "CRÍTICO";
        if (total > 500000) return "ALTO";
        if (total > 100000) return "MÉDIO";
        if (total > 10000) return "BAIXO";
        return "MÍNIMO";
    }
}