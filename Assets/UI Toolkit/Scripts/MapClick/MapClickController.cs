using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public partial class MapClickController : MonoBehaviour
{
    public System.Action<int, string> OnResumoAtualizado;
    public string restUrl;
    public string apiKey;

    // 👇 recebe as credenciais do TelaSegInitializer
    public void Configurar(string url, string key)
    {
        restUrl = url;
        apiKey = key;
        Debug.Log($"[MapClick] Configurado → restUrl='{restUrl}' | apiKey vazio? {string.IsNullOrEmpty(apiKey)}");
    }

    // Nome do elemento no UXML → ID da SUA API
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

    // Nome do elemento → nome bonito pra exibir
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

    // Guarda referência de cada elemento de estado encontrado no UXML
    private Dictionary<string, VisualElement> estadoElementos = new Dictionary<string, VisualElement>();

    // Guarda a cor de risco de cada estado (pra reset depois do clique)
    private Dictionary<string, Color> corRiscoEstado = new Dictionary<string, Color>();

    private VisualElement root;

    // ====== HIGHLIGHT ======
    private VisualElement estadoAtualSelecionado = null;
    private static readonly Color CorSemDados = new Color(0.35f, 0.40f, 0.50f); // cinza-azulado
    private static readonly Color CorBorda    = Color.white;                    // cor do contorno
    private const float LarguraBorda          = 6f;                             // grossura da borda
    private const float EscalaSelecionado     = 1.10f;                          // 10% maior = "sobe"

    // ====== PAINEL (labels) ======
    private Label labelNome;
    private Label labelRisco;
    private Label labelOcorrencias;

    void OnEnable()
    {
        StartCoroutine(SetupClicks());
    }

    private System.Collections.IEnumerator SetupClicks()
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
            {
                Debug.LogWarning($"⚠️ Elemento '{estado.Key}' não encontrado no UXML!");
            }
        }

        Debug.Log($"[MapClick] {estadoElementos.Count} estados registrados.");

        root.pickingMode = PickingMode.Position;
        root.RegisterCallback<ClickEvent>(OnMapaClicked);

        labelNome        = root.Q<Label>("stateTitle");
        labelRisco       = root.Q<Label>("riskBadge");
        labelOcorrencias = root.Q<Label>("stateValue");

        if (labelNome == null)
            Debug.LogWarning(" Label 'stateTitle' não encontrado — confira o name no UXML!");
        if (labelRisco == null)
            Debug.LogWarning(" Label 'riskBadge' não encontrado!");
        if (labelOcorrencias == null)
            Debug.LogWarning(" Label 'stateValue' não encontrado!");

        // PINTA O HEATMAP INICIAL (cinza pra todos + risco real pela API)
        PintarHeatmapInicial();
    }

    void OnDisable()
    {
        if (root != null)
            root.UnregisterCallback<ClickEvent>(OnMapaClicked);
    }

    // ===================================================================
    // 🖱️ CLIQUE
    // ===================================================================

    void OnMapaClicked(ClickEvent evt)
    {
        Debug.Log("Clique detectado em: " + (evt.target as VisualElement)?.name);

        var elementoClicado = evt.target as VisualElement;
        if (elementoClicado != null)
            Debug.Log("Estado clicado: " + elementoClicado.name);

        Vector2 clickPos = evt.position;
        string acertou = null;

        Debug.Log($"==== CLIQUE em {clickPos} ====");

        var ordem = estadoElementos
            .OrderBy(kv => kv.Key == "DistritoFederal" ? 0 : 1)
            .ToList();

        foreach (var kv in ordem)
        {
            string nomeChave = kv.Key;
            VisualElement elemento = kv.Value;

            Texture2D tex = GetTextura(elemento);

            if (nomeChave == "DistritoFederal")
            {
                Vector2 l = elemento.WorldToLocal(clickPos);
                var r = elemento.contentRect;
                Debug.Log($"[DF] tex={(tex == null ? "NULL" : tex.name)} | " +
                          $"readable={(tex != null && tex.isReadable)} | " +
                          $"contentRect={r} | local={l} | " +
                          $"worldBound={elemento.worldBound} | " +
                          $"display={elemento.resolvedStyle.display}");
            }

            if (tex == null || !tex.isReadable) continue;

            Vector2 local = elemento.WorldToLocal(clickPos);
            var rect = elemento.contentRect;

            if (rect.width <= 0 || rect.height <= 0) continue;
            if (local.x < 0 || local.y < 0 || local.x > rect.width || local.y > rect.height)
                continue;

            float texAspect  = (float)tex.width / tex.height;
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

            if (lx < 0 || ly < 0 || lx > drawW || ly > drawH)
                continue;

            int px = Mathf.Clamp((int)(lx / drawW * tex.width),  0, tex.width  - 1);
            int py = Mathf.Clamp((int)(ly / drawH * tex.height), 0, tex.height - 1);
            py = tex.height - 1 - py;

            Color pixel = tex.GetPixel(px, py);
            Debug.Log($"{nomeChave} | px,py=({px},{py}) | alpha={pixel.a:F3} | tex={tex.width}x{tex.height}");

            if (TemPixelOpaco(tex, px, py))
            {
                acertou = nomeChave;
                break;
            }
        }

        if (acertou != null)
            EstadoSelecionado(acertou, estadoParaId[acertou]);
        else
            Debug.Log("Clique fora de qualquer estado (área transparente).");
    }

    private Texture2D GetTextura(VisualElement elemento)
    {
        if (elemento is Image img && img.image is Texture2D t)
            return t;

        var bg = elemento.resolvedStyle.backgroundImage;
        if (bg.texture != null)
            return bg.texture;

        return null;
    }

    private bool TemPixelOpaco(Texture2D tex, int px, int py, int raio = 2, float limite = 0.01f)
    {
        for (int x = -raio; x <= raio; x++)
        {
            for (int y = -raio; y <= raio; y++)
            {
                int sx = Mathf.Clamp(px + x, 0, tex.width  - 1);
                int sy = Mathf.Clamp(py + y, 0, tex.height - 1);
                if (tex.GetPixel(sx, sy).a > limite)
                    return true;
            }
        }
        return false;
    }

    // ====== DESTACA O ESTADO SELECIONADO ======
    private void DestacarEstado(string nomeChave)
    {
        // RESET do anterior: volta pra cor de risco original
        if (estadoAtualSelecionado != null)
        {
            var chaveAnt = estadoElementos.FirstOrDefault(x => x.Value == estadoAtualSelecionado).Key;
            if (chaveAnt != null && corRiscoEstado.TryGetValue(chaveAnt, out var corOrig))
                AplicarTint(estadoAtualSelecionado, corOrig);
        }

        // DESTAQUE no novo: branco suave
        if (estadoElementos.TryGetValue(nomeChave, out var el))
        {
            Color brancoSuave = new Color(0.90f, 0.92f, 0.95f); // off-white levemente azulado
            AplicarTint(el, brancoSuave);

            el.BringToFront();
            estadoAtualSelecionado = el;
            Debug.Log($"🆙 Estado destacado (branco suave): {nomeChave}");
        }
        else
        {
            Debug.LogWarning($"⚠️ Não achei o elemento '{nomeChave}' para destacar!");
        }
    }

    void EstadoSelecionado(string nomeChave, int idEstado)
    {
        string nomeBonito = estadoNomes.ContainsKey(nomeChave) ? estadoNomes[nomeChave] : nomeChave;

        Debug.Log($"Você selecionou: {nomeBonito} | ID = {idEstado}");

        DestacarEstado(nomeChave);

        // Atualiza o painel JÁ (dados locais)
        if (labelNome != null)        labelNome.text        = nomeBonito;
        if (labelOcorrencias != null) labelOcorrencias.text = "—";
        if (labelRisco != null)       labelRisco.text       = "—";

        // Busca detalhes no Supabase (respeitando filtros ativos)
        StartCoroutine(MapDataService.GetStateDetails(
            restUrl, apiKey, idEstado,
            GetCrimeIdsOuNull(), filtroDataInicio, filtroDataFim,
            OnDadosRecebidos
        ));
    }

    // Callback: total, topCrime, risk
    void OnDadosRecebidos(int total, string topCrime, string risk)
    {
        Debug.Log($"✅ Dados recebidos → total: {total} | topCrime: {topCrime} | risk: {risk}");

        // 1) ATUALIZA OS LABELS DO PAINEL
        if (labelOcorrencias != null)
            labelOcorrencias.text = total > 0 ? total.ToString("N0") : "0";
        else
            Debug.LogError("❌ labelOcorrencias é NULL! Confere o name no UXML.");

        if (labelRisco != null)
            labelRisco.text = string.IsNullOrEmpty(risk) ? "Sem dados" : risk;
        else
            Debug.LogError("❌ labelRisco é NULL! Confere o name no UXML.");

        // 2) ATUALIZA A COR DO MAPA
        if (estadoAtualSelecionado != null)
        {
            var chave = estadoElementos.FirstOrDefault(x => x.Value == estadoAtualSelecionado).Key;
            if (chave != null)
            {
                float valor = RiscoTextoParaValor(risk);
                Color cor = CorDoRisco(valor);
                corRiscoEstado[chave] = cor; // guarda a cor real pro reset depois

                // como está selecionado, mantém o branco suave por cima
                AplicarTint(estadoAtualSelecionado, new Color(0.90f, 0.92f, 0.95f));
            }
        }

        // 3) ⬇️ ALIMENTA O RESUMO GERAL (sempre, fora de qualquer if)
        OnResumoAtualizado?.Invoke(total, topCrime);
    }
}
