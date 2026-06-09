using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public class MapClickController : MonoBehaviour
{
    public string restUrl;
    public string apiKey;
    public Action<int, string> OnResumoAtualizado;
    public Action<string> OnEstadoSelecionado;

    private List<int> filtroCrimeIds = new List<int>();
    private int? filtroAno = null;
    private VisualElement estadoAtualSelecionado = null;
    private string estadoAtualNome = null;
    private VisualElement mapContainer;
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
    private Dictionary<string, VisualElement> estadoElementos = new Dictionary<string, VisualElement>();
    private VisualElement root;
    private Label labelNome;
    private Label labelRisco;
    private Label labelOcorrencias;

    public void Configurar(string url, string key)
    {
        restUrl = url;
        apiKey = key;
    }

    public void AplicarFiltros(List<int> crimeIds, int? ano)
    {
        filtroCrimeIds = crimeIds ?? new List<int>();
        filtroAno = ano;

        if (estadoAtualSelecionado != null && estadoAtualNome != null)
            AtualizarDetalhesEstado(estadoAtualNome);
        else
            CarregarResumoGeral();
    }

    public List<int> GetCrimeIdsOuNull() => (filtroCrimeIds != null && filtroCrimeIds.Count > 0) ? filtroCrimeIds : null;
    public int? GetAno() => filtroAno;
    public string GetEstadoAtual() => estadoAtualNome;

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

        mapContainer = root.Q<VisualElement>("mapContainer");
        estadoElementos.Clear();
        foreach (var estado in estadoParaId)
        {
            VisualElement elemento = root.Q<VisualElement>(estado.Key);
            if (elemento != null)
            {
                estadoElementos[estado.Key] = elemento;
                elemento.pickingMode = PickingMode.Ignore;
            }
        }

        root.pickingMode = PickingMode.Position;
        root.RegisterCallback<ClickEvent>(OnMapaClicked);

        labelNome = root.Q<Label>("stateTitle");
        labelRisco = root.Q<Label>("riskBadge");
        labelOcorrencias = root.Q<Label>("stateValue");

        CarregarResumoGeral();
    }

    private void OnMapaClicked(ClickEvent evt)
    {
        if (mapContainer != null && !mapContainer.worldBound.Contains(evt.position)) return;

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
            if (local.x < 0 || local.y < 0 || local.x > rect.width || local.y > rect.height) continue;

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
            CarregarResumoGeral();
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
                if (tex.GetPixel(sx, sy).a > limite) return true;
            }
        return false;
    }

    private void EstadoSelecionado(string nomeChave)
    {
        string nomeBonito = estadoNomes.ContainsKey(nomeChave) ? estadoNomes[nomeChave] : nomeChave;
        if (estadoAtualNome == nomeBonito) return;

        estadoAtualNome = nomeBonito;
        if (estadoElementos.TryGetValue(nomeChave, out var el)) estadoAtualSelecionado = el;

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
            OnDadosEstadoRecebidos));
    }

    private void OnDadosEstadoRecebidos(int total, string topCrime, string risk)
    {
        if (labelOcorrencias != null) labelOcorrencias.text = total > 0 ? total.ToString("N0") : "0";
        if (labelRisco != null) labelRisco.text = string.IsNullOrEmpty(risk) ? "Sem dados" : risk;
        OnResumoAtualizado?.Invoke(total, topCrime);
    }

    private void CarregarResumoGeral()
    {
        if (estadoAtualNome == null) return;
        estadoAtualNome = null;
        estadoAtualSelecionado = null;

        OnEstadoSelecionado?.Invoke(null);

        if (labelNome != null) labelNome.text = "Brasil";
        if (labelOcorrencias != null) labelOcorrencias.text = "—";
        if (labelRisco != null) labelRisco.text = "—";

        StartCoroutine(MapDataService.GetGeneralSummary(
            restUrl, apiKey,
            GetCrimeIdsOuNull(), filtroAno,
            OnResumoGeralRecebido));
    }

    private void OnResumoGeralRecebido(int total, string topCrime)
    {
        string risk = total > 1000000 ? "CRÍTICO" : total > 500000 ? "ALTO" : total > 100000 ? "MÉDIO" : total > 10000 ? "BAIXO" : "MÍNIMO";
        if (labelOcorrencias != null) labelOcorrencias.text = total > 0 ? total.ToString("N0") : "0";
        if (labelRisco != null) labelRisco.text = risk;
        OnResumoAtualizado?.Invoke(total, topCrime);
    }
}