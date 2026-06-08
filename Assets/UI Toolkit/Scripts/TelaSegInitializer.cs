using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class TelaSegInitializer : MonoBehaviour
{
    [Header("Supabase")]
    public string supabaseRestUrl = "https://xoixhsvipnykrrvscmet.supabase.co/rest/v1/";
    public string supabasePublishableKey = "sb_publishable_E53U7mwhkIpOs_e8-R6eyQ_h2eEEn5b";

    [Header("Distrito Federal")]
    public VisualTreeAsset dfUxml;   // arraste o df.uxml aqui no Inspector

    private UIDocument uiDoc;
    private VisualElement root;

    // Popup de crimes
    private Button crimeButton;
    private VisualElement crimePopup;
    private ScrollView crimeListContainer;
    private Label selectedCountLabel;
    private Button clearBtn, applyBtn;
    private List<CrimeItem> crimes = new List<CrimeItem>();
    private HashSet<int> pendingSelectedIds = new HashSet<int>();
    private HashSet<int> activeSelectedIds = new HashSet<int>();

    // Filtro de data
    private TextField dataInicioField, dataFimField;
    private Button aplicarDataBtn;
    private DateTime? dataInicio, dataFim;
    private DropdownField anoDropdown;

    // Mapa
    private Dictionary<string, Image> estadoImages = new Dictionary<string, Image>();
    private Dictionary<string, int> nomeEstadoParaId = new Dictionary<string, int>();
    private Label stateTitle, riskBadge, stateValue;
    private MapClickController mapClick;

    private Image estadoSelecionado = null;

    // Resumo
    private Label totalCrimesLabel, topCrimeLabel;

    // Distrito Federal
    private DFController dfController;

    private class CrimeItem
    {
        public int id;
        public string nome;
        public Toggle toggle;
    }

    void Start()
    {
        uiDoc = GetComponent<UIDocument>();
        root = uiDoc.rootVisualElement;

        // Capturar elementos PRIMEIRO
        crimeButton = root.Q<Button>("CrimeButton");
        crimePopup = root.Q<VisualElement>("CrimePopup");
        crimeListContainer = root.Q<ScrollView>("CrimeListContainer");
        selectedCountLabel = root.Q<Label>("SelectedCountLabel");
        clearBtn = root.Q<Button>("ClearBtn");
        applyBtn = root.Q<Button>("ApplyBtn");
        var closePopupBtn = root.Q<Button>("ClosePopupBtn");
        dataInicioField = root.Q<TextField>("DataInicio");
        dataFimField = root.Q<TextField>("DataFim");
        aplicarDataBtn = root.Q<Button>("AplicarDataBtn");

        totalCrimesLabel = root.Q<Label>("totalCrimesLabel");
        topCrimeLabel = root.Q<Label>("topCrimeLabel");
        stateTitle = root.Q<Label>("stateTitle");
        riskBadge = root.Q<Label>("riskBadge");
        stateValue = root.Q<Label>("stateValue");

        // ⚠️ Validação dos labels do resumo (ajuda a debugar)
        if (totalCrimesLabel == null)
            Debug.LogError("❌ 'totalCrimesLabel' NÃO encontrado no UXML! Confere o name.");
        if (topCrimeLabel == null)
            Debug.LogError("❌ 'topCrimeLabel' NÃO encontrado no UXML! Confere o name.");

        if (dataInicioField != null)
            ConfigurarCampoNumerico(dataInicioField, "mm", 2, true);

        ConfigurarDropdownAno();

        // Inicializa o controller do DF
        if (dfUxml != null)
            dfController = new DFController(root, root.Q<VisualElement>("Mapa"), dfUxml);

        // ===================================================================
        // 🔌 PEGA O MapClickController E CONECTA O RESUMO (na ordem certa!)
        // ===================================================================
        mapClick = GetComponent<MapClickController>();
        if (mapClick == null)
            mapClick = FindObjectOfType<MapClickController>();

        if (mapClick != null)
        {
            mapClick.Configurar(supabaseRestUrl, supabasePublishableKey);
            mapClick.OnResumoAtualizado += AtualizarResumo;   // ✅ AGORA mapClick NÃO É NULL
            Debug.Log("[TelaSeg] Resumo conectado ao MapClickController.");
        }
        else
        {
            Debug.LogWarning("⚠️ MapClickController não encontrado na cena!");
        }

        // Eventos do popup
        crimeButton.clicked += () =>
        {
            crimePopup.style.display = DisplayStyle.Flex;
            pendingSelectedIds = new HashSet<int>(activeSelectedIds);
            foreach (var crime in crimes)
                crime.toggle.SetValueWithoutNotify(pendingSelectedIds.Contains(crime.id));
            UpdateSelectedCountLabel();
        };

        closePopupBtn.clicked += () => crimePopup.style.display = DisplayStyle.None;

        clearBtn.clicked += () =>
        {
            pendingSelectedIds.Clear();
            foreach (var crime in crimes)
                crime.toggle.SetValueWithoutNotify(false);
            UpdateSelectedCountLabel();
        };

        applyBtn.clicked += () =>
        {
            activeSelectedIds = new HashSet<int>(pendingSelectedIds);
            crimePopup.style.display = DisplayStyle.None;
            UpdateCrimeButtonText();

            if (mapClick != null)
                mapClick.AplicarFiltros(activeSelectedIds.ToList(), dataInicio, dataFim);
        };

        aplicarDataBtn.clicked += OnDataApplied;

        // Fechar popup ao clicar fora
        root.RegisterCallback<ClickEvent>(evt =>
        {
            if (crimePopup.style.display == DisplayStyle.Flex)
            {
                var target = evt.target as VisualElement;
                if (target != crimePopup && !crimePopup.Contains(target) && target != crimeButton)
                {
                    crimePopup.style.display = DisplayStyle.None;
                }
            }
        });

        // Criar gradiente horizontal
        var gradientElement = root.Q<VisualElement>("HeatGradient");
        if (gradientElement != null)
            CreateHorizontalGradient(gradientElement);

        // Carrega a lista de crimes e popula o popup
        StartCoroutine(MapDataService.GetCrimes(
            supabaseRestUrl, supabasePublishableKey, PopularPopupCrimes
        ));

        // Seleção visual dos estados
        StartCoroutine(SetupMapImages());
    }

    // ===================================================================
    // 🔎 POPULA O POPUP DE CRIMES COM TOGGLES
    // ===================================================================
    private void PopularPopupCrimes(List<(int id, string nome)> listaCrimes)
    {
        crimes.Clear();
        crimeListContainer.Clear();

        foreach (var (id, nome) in listaCrimes)
        {
            var toggle = new Toggle(nome);
            toggle.AddToClassList("crime-toggle");

            toggle.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue) pendingSelectedIds.Add(id);
                else              pendingSelectedIds.Remove(id);
                UpdateSelectedCountLabel();
            });

            crimeListContainer.Add(toggle);

            crimes.Add(new CrimeItem
            {
                id = id,
                nome = nome,
                toggle = toggle
            });
        }

        Debug.Log($"[TelaSeg] Popup populado com {crimes.Count} crimes.");
        UpdateCrimeButtonText();
    }

    private void CreateHorizontalGradient(VisualElement container)
    {
        container.Clear();
        container.style.flexDirection = FlexDirection.Row;
        int steps = 10;
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            Color cor = GetHeatColor(t);
            var box = new VisualElement();
            box.style.flexGrow = 1;
            box.style.backgroundColor = cor;
            container.Add(box);
        }
    }

    private void UpdateSelectedCountLabel()
    {
        selectedCountLabel.text = $"{pendingSelectedIds.Count} selecionados";
    }

    private void UpdateCrimeButtonText()
    {
        int count = activeSelectedIds.Count;
        if (count == 0) crimeButton.text = "Tipo de Crime (nenhum)";
        else if (count == crimes.Count) crimeButton.text = "Tipo de Crime (todos)";
        else crimeButton.text = $"Tipo de Crime ({count} selecionados)";
    }

    private void OnDataApplied()
    {
        string mesTxt = dataInicioField.value;
        string anoTxt = anoDropdown != null ? anoDropdown.value : null;

        // ⬇️ Ano é obrigatório
        if (string.IsNullOrEmpty(anoTxt) || !int.TryParse(anoTxt, out int ano))
        {
            Debug.LogWarning("⚠️ Selecione um ano!");
            dataInicio = null;
            dataFim = null;
            return;
        }

        // ⬇️ Mês é OPCIONAL
        bool temMes = !string.IsNullOrEmpty(mesTxt)
                      && mesTxt != "mm"
                      && int.TryParse(mesTxt, out int mes)
                      && mes >= 1 && mes <= 12;

        if (temMes)
        {
            int m = int.Parse(mesTxt);
            // Filtra só o MÊS escolhido
            dataInicio = new DateTime(ano, m, 1);
            dataFim    = dataInicio.Value.AddMonths(1).AddSeconds(-1);
        }
        else
        {
            // Filtra o ANO INTEIRO (01/jan a 31/dez)
            dataInicio = new DateTime(ano, 1, 1);
            dataFim    = new DateTime(ano, 12, 31, 23, 59, 59);
        }

        Debug.Log($"[Filtro] Ano={ano} | TemMes={temMes} | Início={dataInicio} | Fim={dataFim}");

        if (mapClick != null)
            mapClick.AplicarFiltros(activeSelectedIds.ToList(), dataInicio, dataFim);
    }

    private DateTime? MontarData(string mes, string ano)
    {
        if (string.IsNullOrEmpty(mes) || mes == "mm") return null;
        if (string.IsNullOrEmpty(ano) || ano == "aaaa") return null;

        if (int.TryParse(mes, out int m) && int.TryParse(ano, out int a))
        {
            if (m >= 1 && m <= 12 && (a == 2025 || a == 2026))
                return new DateTime(a, m, 1);
        }
        return null;
    }

    private IEnumerator SetupMapImages()
    {
        yield return null;
        yield return null;

        var mapaInstance = root.Q<VisualElement>("Mapa");
        int tentativas = 0;
        while (mapaInstance == null && tentativas < 30)
        {
            yield return null;
            mapaInstance = root.Q<VisualElement>("Mapa");
            tentativas++;
        }

        if (mapaInstance == null)
        {
            Debug.LogError("[Mapa] Elemento 'Mapa' não encontrado após esperar!");
            yield break;
        }

        var allImages = mapaInstance.Query<Image>().ToList();
        Debug.Log($"[Mapa] Encontradas {allImages.Count} imagens.");

        foreach (var img in allImages)
        {
            if (string.IsNullOrEmpty(img.name)) continue;

            estadoImages[img.name] = img;
            img.tintColor = Color.white;
            img.RemoveFromClassList("estado-selecionado");
            img.pickingMode = PickingMode.Ignore;
        }

        Debug.Log($"[Mapa] {estadoImages.Count} imagens registradas: {string.Join(", ", estadoImages.Keys)}");
    }

    private string MapImageNameToStateName(string imageName)
    {
        var map = new Dictionary<string, string>
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
        return map.ContainsKey(imageName) ? map[imageName] : imageName;
    }

    private Color GetHeatColor(float t)
    {
        t = Mathf.Clamp01(t);
        Color c;
        if (t < 0.33f) c = Color.Lerp(Color.blue, Color.yellow, t / 0.33f);
        else if (t < 0.66f) c = Color.Lerp(Color.yellow, new Color(1f, 0.5f, 0f), (t - 0.33f) / 0.33f);
        else c = Color.Lerp(new Color(1f, 0.5f, 0f), Color.red, (t - 0.66f) / 0.34f);
        c.a = 1f;
        return c;
    }

    // ===================================================================
    // 📅 CAMPO NUMÉRICO COM LIMITE (mês 1-12 / ano até o atual)
    // ===================================================================
    private void ConfigurarCampoNumerico(TextField campo, string placeholder, int maxDigitos, bool ehMes)
    {
        campo.maxLength = maxDigitos;
        bool temFoco = false;
        int anoAtual = DateTime.Now.Year;

        campo.SetValueWithoutNotify(placeholder);
        campo.AddToClassList("placeholder");

        campo.RegisterCallback<FocusInEvent>(_ =>
        {
            temFoco = true;
            if (campo.value == placeholder)
            {
                campo.SetValueWithoutNotify("");
                campo.RemoveFromClassList("placeholder");
            }
        });

        campo.RegisterCallback<FocusOutEvent>(_ =>
        {
            temFoco = false;
            if (string.IsNullOrEmpty(campo.value))
            {
                campo.SetValueWithoutNotify(placeholder);
                campo.AddToClassList("placeholder");
            }
        });

        campo.RegisterValueChangedCallback(evt =>
        {
            if (!temFoco) return;
            if (evt.newValue == placeholder) return;

            // 1) Só números
            string numeros = "";
            foreach (char c in evt.newValue)
                if (char.IsDigit(c))
                    numeros += c;

            // 2) Limita a quantidade de dígitos
            if (numeros.Length > maxDigitos)
                numeros = numeros.Substring(0, maxDigitos);

            // 3) Valida o LIMITE do valor
            if (!string.IsNullOrEmpty(numeros) && int.TryParse(numeros, out int valor))
            {
                if (ehMes)
                {
                    // MÊS: não deixa passar de 12
                    if (valor > 12) numeros = "12";
                }
                else
                {
                    // ANO: não deixa passar do ano atual
                    if (numeros.Length == maxDigitos && valor > anoAtual)
                        numeros = anoAtual.ToString();
                }
            }

            if (numeros != evt.newValue)
                campo.SetValueWithoutNotify(numeros);
        });
    }

    // ===================================================================
    // 📅 DROPDOWN DE ANO (apenas 2025 e 2026)
    // ===================================================================
    private void ConfigurarDropdownAno()
    {
        anoDropdown = root.Q<DropdownField>("AnoDropdown");

        if (anoDropdown == null)
        {
            Debug.LogError("[Dropdown] AnoDropdown NÃO encontrado no UXML!");
            return;
        }

        anoDropdown.choices = new List<string> { "2025", "2026" };
        anoDropdown.value = "2025"; // valor inicial (opcional)

        Debug.Log($"[Dropdown] Configurado com {anoDropdown.choices.Count} opções.");
    }

    // ===================================================================
    // 📊 RESUMO GERAL (total + maior ocorrência)
    // ===================================================================
    public void AtualizarResumo(int total, string topCrime)
    {
        // CRIMES REGISTRADOS
        if (totalCrimesLabel != null)
            totalCrimesLabel.text = total.ToString("N0");

        // MAIOR OCORRÊNCIA
        if (topCrimeLabel != null)
            topCrimeLabel.text = (total > 0 && !string.IsNullOrEmpty(topCrime))
                ? topCrime
                : "—";

        Debug.Log($"[Resumo] Total={total} | Maior={(topCrimeLabel != null ? topCrimeLabel.text : "label null")}");
    }
}
