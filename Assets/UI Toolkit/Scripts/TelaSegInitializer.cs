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

    // Mapa
    private Dictionary<string, Image> estadoImages = new Dictionary<string, Image>();
    private Dictionary<string, int> nomeEstadoParaId = new Dictionary<string, int>();
    private Label stateTitle, riskBadge, stateValue;

    // Resumo
    private Label totalCrimesLabel, topCrimeLabel;

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

        // Capturar elementos
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
            RefreshData();
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

        // Elementos de exibição
        totalCrimesLabel = root.Q<Label>("totalCrimesLabel");
        topCrimeLabel = root.Q<Label>("topCrimeLabel");
        stateTitle = root.Q<Label>("stateTitle");
        riskBadge = root.Q<Label>("riskBadge");
        stateValue = root.Q<Label>("stateValue");

        // Criar gradiente horizontal
        var gradientElement = root.Q<VisualElement>("HeatGradient");
        if (gradientElement != null)
            CreateHorizontalGradient(gradientElement);

        // Carregar dados
        StartCoroutine(LoadCrimes());
        StartCoroutine(LoadStatesMapping());
        StartCoroutine(SetupMapImages());
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

    private IEnumerator LoadCrimes()
    {
        bool done = false;
        yield return SupabaseRestClient.Get(supabaseRestUrl, supabasePublishableKey, "crime?select=id,crime", (status, body, err) =>
        {
            if (string.IsNullOrEmpty(err))
            {
                var matches = Regex.Matches(body, "\"id\"\\s*:\\s*(\\d+).*?\"crime\"\\s*:\\s*\"([^\"]+)\"");
                crimes.Clear();
                crimeListContainer.Clear();
                pendingSelectedIds.Clear();
                foreach (Match m in matches)
                {
                    if (int.TryParse(m.Groups[1].Value, out int id))
                    {
                        string nome = m.Groups[2].Value;
                        var toggle = new Toggle(nome);
                        toggle.userData = id;
                        toggle.RegisterValueChangedCallback(evt =>
                        {
                            int crimeId = (int)toggle.userData;
                            if (evt.newValue) pendingSelectedIds.Add(crimeId);
                            else pendingSelectedIds.Remove(crimeId);
                            UpdateSelectedCountLabel();
                        });
                        crimes.Add(new CrimeItem { id = id, nome = nome, toggle = toggle });
                        crimeListContainer.Add(toggle);
                        pendingSelectedIds.Add(id);
                        toggle.SetValueWithoutNotify(true);
                    }
                }
                activeSelectedIds = new HashSet<int>(pendingSelectedIds);
                UpdateSelectedCountLabel();
                UpdateCrimeButtonText();
            }
            done = true;
        });
        yield return new WaitUntil(() => done);
        // Após carregar crimes, atualiza o mapa
        RefreshData();
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
        dataInicio = ParseDate(dataInicioField.value);
        dataFim = ParseDate(dataFimField.value);
        RefreshData();
    }

    private DateTime? ParseDate(string value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (DateTime.TryParseExact(value, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime dt))
            return dt;
        return null;
    }

    private IEnumerator LoadStatesMapping()
    {
        bool done = false;
        yield return SupabaseRestClient.Get(supabaseRestUrl, supabasePublishableKey, "estado?select=id,estado", (status, body, err) =>
        {
            if (string.IsNullOrEmpty(err))
            {
                var matches = Regex.Matches(body, "\"id\"\\s*:\\s*(\\d+).*?\"estado\"\\s*:\\s*\"([^\"]+)\"");
                foreach (Match m in matches)
                    if (int.TryParse(m.Groups[1].Value, out int id))
                        nomeEstadoParaId[m.Groups[2].Value] = id;
            }
            done = true;
        });
        yield return new WaitUntil(() => done);
    }

    private IEnumerator SetupMapImages()
    {
        yield return new WaitForSeconds(0.1f);
        var mapaInstance = root.Q<VisualElement>("Mapa");
        if (mapaInstance == null)
        {
            Debug.LogError("[Mapa] Elemento 'Mapa' não encontrado!");
            yield break;
        }

        var allImages = mapaInstance.Query<Image>().ToList();
        Debug.Log($"[Mapa] Encontradas {allImages.Count} imagens.");
        foreach (var img in allImages)
        {
            if (string.IsNullOrEmpty(img.name)) continue;
            estadoImages[img.name] = img;
            img.RegisterCallback<ClickEvent>(evt => OnStateClicked(img.name));
        }
        Debug.Log($"[Mapa] {estadoImages.Count} imagens registradas.");

        // Força uma atualização inicial para colorir o mapa
        RefreshData();
    }

    private void OnStateClicked(string imageName)
    {
        string stateName = MapImageNameToStateName(imageName);
        if (nomeEstadoParaId.TryGetValue(stateName, out int id))
        {
            stateTitle.text = stateName;
            StartCoroutine(MapDataService.GetStateDetails(supabaseRestUrl, supabasePublishableKey, id,
                activeSelectedIds.ToList(), dataInicio, dataFim,
                (total, topCrime, risk) =>
                {
                    stateValue.text = total.ToString("N0");
                    riskBadge.text = risk;
                }));
        }
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

    private void RefreshData()
    {
        StartCoroutine(MapDataService.GetOccurrencesByState(supabaseRestUrl, supabasePublishableKey,
            activeSelectedIds.ToList(), dataInicio, dataFim, UpdateMapColors));
        StartCoroutine(MapDataService.GetGeneralSummary(supabaseRestUrl, supabasePublishableKey,
            activeSelectedIds.ToList(), dataInicio, dataFim, UpdateSummary));
    }

    private void UpdateMapColors(List<MapDataService.StateData> data)
    {
        if (data == null || data.Count == 0)
        {
            Debug.LogWarning("[Mapa] Sem dados para colorir, usando cinza.");
            foreach (var img in estadoImages.Values) img.tintColor = Color.gray;
            return;
        }

        float maxOcc = data.Max(x => x.ocorrencias);
        Debug.Log($"[Mapa] Max ocorrências: {maxOcc}");
        foreach (var kvp in estadoImages)
        {
            string stateName = MapImageNameToStateName(kvp.Key);
            var state = data.FirstOrDefault(d => d.nome == stateName);
            if (state != null)
            {
                float intensity = maxOcc > 0 ? (float)state.ocorrencias / maxOcc : 0;
                kvp.Value.tintColor = GetHeatColor(intensity);
                Debug.Log($"[Mapa] {kvp.Key} -> {state.ocorrencias} ocorrências, intensidade {intensity:F2}, cor {kvp.Value.tintColor}");
            }
            else
            {
                kvp.Value.tintColor = Color.gray;
                Debug.Log($"[Mapa] {kvp.Key} -> sem dados, cinza");
            }
        }
    }

    private Color GetHeatColor(float t)
    {
        t = Mathf.Clamp01(t);
        if (t < 0.33f) return Color.Lerp(Color.blue, Color.yellow, t / 0.33f);
        if (t < 0.66f) return Color.Lerp(Color.yellow, new Color(1f, 0.5f, 0f), (t - 0.33f) / 0.33f);
        return Color.Lerp(new Color(1f, 0.5f, 0f), Color.red, (t - 0.66f) / 0.34f);
    }

    private void UpdateSummary(int total, string topCrime)
    {
        totalCrimesLabel.text = total.ToString("N0");
        topCrimeLabel.text = topCrime;
    }
}