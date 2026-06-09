using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
    private PieChartController pieChartController;
    private LineChartController lineChartController;

    private Button crimeButton;
    private DropdownField anoDropdown;
    private Button aplicarPeriodoBtn;
    private Label totalCrimesLabel, topCrimeLabel;
    private Label stateTitle, riskBadge, stateValue;
    private Button exportExcelBtn, exportCsvBtn;
    private TextField fileNameField;

    private VisualElement crimePopup;
    private ScrollView crimeListContainer;
    private Label selectedCountLabel;
    private Button allBtn, clearBtn, applyBtn;
    private List<CrimeButtonItem> crimeButtons = new List<CrimeButtonItem>();
    private HashSet<int> pendingSelectedIds = new HashSet<int>();
    private HashSet<int> activeSelectedIds = new HashSet<int>();

    private int? selectedAno = null;
    private string selectedEstado = null;

    private MapClickController mapClickController;

    private class CrimeButtonItem
    {
        public int id;
        public string nome;
        public Button button;
    }

    void Start()
    {
        uiDoc = GetComponent<UIDocument>();
        root = uiDoc.rootVisualElement;

        pieChartController = new PieChartController(uiDoc, this, supabaseRestUrl, supabasePublishableKey);

        var lineContainer = root.Q<VisualElement>("LineChartContainer");
        if (lineContainer != null)
            lineChartController = new LineChartController(lineContainer);
        else
            Debug.LogError("LineChartContainer não encontrado!");

        crimeButton = root.Q<Button>("CrimeButton");
        anoDropdown = root.Q<DropdownField>("AnoDropdown");
        aplicarPeriodoBtn = root.Q<Button>("AplicarPeriodoBtn");
        totalCrimesLabel = root.Q<Label>("totalCrimesLabel");
        topCrimeLabel = root.Q<Label>("topCrimeLabel");
        stateTitle = root.Q<Label>("stateTitle");
        riskBadge = root.Q<Label>("riskBadge");
        stateValue = root.Q<Label>("stateValue");

        fileNameField = root.Q<TextField>("FileNameField");
        if (fileNameField != null && string.IsNullOrEmpty(fileNameField.value))
            fileNameField.value = "ocorrencias";

        exportExcelBtn = root.Q<Button>("ExportExcelBtn");
        exportCsvBtn = root.Q<Button>("ExportCsvBtn");

        if (exportExcelBtn != null) exportExcelBtn.clicked += () => ExportData("excel");
        if (exportCsvBtn != null) exportCsvBtn.clicked += () => ExportData("csv");

        ConfigureAnoDropdown();

        aplicarPeriodoBtn.clicked += () =>
        {
            string anoStr = anoDropdown.value;
            selectedAno = (anoStr == "Todos" || string.IsNullOrEmpty(anoStr)) ? (int?)null : int.Parse(anoStr);
            RefreshData();
        };

        CreateCrimePopup();

        var gradientElement = root.Q<VisualElement>("HeatGradient");
        if (gradientElement != null) CreateHorizontalGradient(gradientElement);

        StartCoroutine(InitializeAsync());

        mapClickController = GetComponent<MapClickController>();
        if (mapClickController != null)
        {
            mapClickController.Configurar(supabaseRestUrl, supabasePublishableKey);
            mapClickController.OnResumoAtualizado += UpdateSummary;
            mapClickController.OnEstadoSelecionado += OnEstadoSelecionadoNoMapa;
        }
        else
        {
            Debug.LogError("[TelaSeg] MapClickController não encontrado!");
        }
    }

    private void ConfigureAnoDropdown()
    {
        var anos = new List<string> { "Todos" };
        for (int y = 2020; y <= DateTime.Now.Year + 1; y++) anos.Add(y.ToString());
        anoDropdown.choices = anos;
        anoDropdown.value = "Todos";
    }

    private void CreateCrimePopup()
    {
        crimePopup = new VisualElement();
        crimePopup.AddToClassList("crime-popup");
        crimePopup.style.display = DisplayStyle.None;
        root.Add(crimePopup);

        var header = new VisualElement();
        header.AddToClassList("popup-header");
        var title = new Label("Selecione os crimes");
        title.AddToClassList("popup-title");
        var closeBtn = new Button(() => ClosePopup()) { text = "✕" };
        closeBtn.AddToClassList("close-btn");
        header.Add(title);
        header.Add(closeBtn);
        crimePopup.Add(header);

        crimeListContainer = new ScrollView();
        crimeListContainer.AddToClassList("crime-list");
        crimePopup.Add(crimeListContainer);

        var footer = new VisualElement();
        footer.AddToClassList("popup-footer");
        selectedCountLabel = new Label("0 selecionados");
        selectedCountLabel.AddToClassList("selected-count");
        footer.Add(selectedCountLabel);
        var btnRow = new VisualElement();
        btnRow.AddToClassList("popup-buttons");
        allBtn = new Button(() => SelectAll()) { text = "Todos" };
        allBtn.AddToClassList("popup-btn");
        clearBtn = new Button(() => ClearAll()) { text = "Limpar" };
        clearBtn.AddToClassList("popup-btn");
        applyBtn = new Button(() => ApplySelection()) { text = "Aplicar" };
        applyBtn.AddToClassList("popup-btn");
        applyBtn.AddToClassList("popup-btn-primary");
        btnRow.Add(allBtn);
        btnRow.Add(clearBtn);
        btnRow.Add(applyBtn);
        footer.Add(btnRow);
        crimePopup.Add(footer);

        crimeButton.clicked += OpenPopup;
        root.RegisterCallback<ClickEvent>(evt =>
        {
            if (crimePopup.style.display == DisplayStyle.Flex)
            {
                VisualElement target = evt.target as VisualElement;
                if (target != crimePopup && !crimePopup.Contains(target) && target != crimeButton)
                    ClosePopup();
            }
        });
    }

    private void OpenPopup()
    {
        Vector2 pos = crimeButton.worldBound.position;
        crimePopup.style.left = pos.x;
        crimePopup.style.top = pos.y + crimeButton.worldBound.height + 5;
        crimePopup.style.right = StyleKeyword.Auto;
        crimePopup.style.bottom = StyleKeyword.Auto;

        pendingSelectedIds = new HashSet<int>(activeSelectedIds);
        UpdateCrimeButtonsSelection();
        UpdateSelectedCountLabel();
        crimePopup.style.display = DisplayStyle.Flex;
        crimePopup.BringToFront();
    }

    private void ClosePopup() => crimePopup.style.display = DisplayStyle.None;
    private void SelectAll() { pendingSelectedIds = new HashSet<int>(crimeButtons.Select(c => c.id)); UpdateCrimeButtonsSelection(); UpdateSelectedCountLabel(); }
    private void ClearAll() { pendingSelectedIds.Clear(); UpdateCrimeButtonsSelection(); UpdateSelectedCountLabel(); }
    private void ApplySelection() { activeSelectedIds = new HashSet<int>(pendingSelectedIds); ClosePopup(); UpdateCrimeButtonText(); RefreshData(); }
    private void UpdateCrimeButtonsSelection()
    {
        foreach (var item in crimeButtons)
        {
            if (pendingSelectedIds.Contains(item.id))
                item.button.AddToClassList("crime-button-selected");
            else
                item.button.RemoveFromClassList("crime-button-selected");
        }
    }
    private void UpdateSelectedCountLabel() => selectedCountLabel.text = $"{pendingSelectedIds.Count} selecionados";
    private void UpdateCrimeButtonText()
    {
        int count = activeSelectedIds.Count;
        if (count == 0) crimeButton.text = "Tipo de Crime (nenhum)";
        else if (count == crimeButtons.Count) crimeButton.text = "Tipo de Crime (todos)";
        else crimeButton.text = $"Tipo de Crime ({count} selecionados)";
    }

    private IEnumerator InitializeAsync()
    {
        yield return LoadCrimes();
        yield return SetupMapImages();
        RefreshData();
    }

    private IEnumerator LoadCrimes()
    {
        bool done = false;
        yield return SupabaseRestClient.Get(supabaseRestUrl, supabasePublishableKey, "crime?select=id,crime", (status, body, err) =>
        {
            if (string.IsNullOrEmpty(err))
            {
                var matches = Regex.Matches(body, "\"id\"\\s*:\\s*(\\d+).*?\"crime\"\\s*:\\s*\"([^\"]+)\"");
                crimeButtons.Clear();
                crimeListContainer.Clear();
                pendingSelectedIds.Clear();

                var container = new VisualElement();
                container.AddToClassList("crime-buttons-container");
                foreach (Match m in matches)
                {
                    if (int.TryParse(m.Groups[1].Value, out int id))
                    {
                        string nome = m.Groups[2].Value;
                        var btn = new Button { text = nome };
                        btn.AddToClassList("crime-button");
                        btn.userData = id;
                        btn.clicked += () =>
                        {
                            int crimeId = (int)btn.userData;
                            if (pendingSelectedIds.Contains(crimeId))
                                pendingSelectedIds.Remove(crimeId);
                            else
                                pendingSelectedIds.Add(crimeId);
                            UpdateCrimeButtonsSelection();
                            UpdateSelectedCountLabel();
                        };
                        crimeButtons.Add(new CrimeButtonItem { id = id, nome = nome, button = btn });
                        container.Add(btn);
                        pendingSelectedIds.Add(id);
                    }
                }
                crimeListContainer.Add(container);
                activeSelectedIds = new HashSet<int>(pendingSelectedIds);
                UpdateCrimeButtonsSelection();
                UpdateSelectedCountLabel();
                UpdateCrimeButtonText();
            }
            done = true;
        });
        yield return new WaitUntil(() => done);
    }

    private IEnumerator SetupMapImages()
    {
        yield return new WaitForSeconds(0.1f);
    }

    private void OnEstadoSelecionadoNoMapa(string estadoNome)
    {
        selectedEstado = estadoNome;
        stateTitle.text = string.IsNullOrEmpty(selectedEstado) ? "Brasil" : selectedEstado;
        RefreshData();
    }

    private void RefreshData()
    {
        mapClickController?.AplicarFiltros(activeSelectedIds.ToList(), selectedAno);
        StartCoroutine(MapDataService.GetOccurrencesByState(supabaseRestUrl, supabasePublishableKey,
            activeSelectedIds.ToList(), selectedAno, UpdateMapColors));
        StartCoroutine(LoadMonthlyData());
        UpdatePieChart();
    }

    private IEnumerator LoadMonthlyData()
    {
        string estado = string.IsNullOrEmpty(selectedEstado) ? null : selectedEstado;
        yield return MapDataService.GetMonthlyData(supabaseRestUrl, supabasePublishableKey,
            estado, activeSelectedIds.ToList(), selectedAno, monthlyData =>
            {
                lineChartController?.SetData(monthlyData);
            });
    }

    private void UpdateMapColors(List<MapDataService.StateData> data)
    {
        var mapaInstance = root.Q<VisualElement>("Mapa");
        if (mapaInstance == null) return;
        var estadoImages = new Dictionary<string, Image>();
        foreach (var img in mapaInstance.Query<Image>().ToList())
            if (!string.IsNullOrEmpty(img.name))
                estadoImages[img.name] = img;

        if (data == null || data.Count == 0)
        {
            foreach (var img in estadoImages.Values) img.tintColor = Color.gray;
            return;
        }

        float max = data.Max(d => d.ocorrencias);
        foreach (var kvp in estadoImages)
        {
            string nomeEstado = MapImageNameToStateName(kvp.Key);
            var state = data.FirstOrDefault(d => d.nome == nomeEstado);
            if (state != null)
            {
                float intensity = max > 0 ? (float)state.ocorrencias / max : 0;
                kvp.Value.tintColor = ObterCorPorIntensidade(intensity);
            }
            else kvp.Value.tintColor = Color.gray;
        }
    }

    private string MapImageNameToStateName(string imageName)
    {
        var map = new Dictionary<string, string>
        {
            {"Acre","Acre"},{"Alagoas","Alagoas"},{"Amapa","Amapá"},{"Amazonas","Amazonas"},
            {"Bahia","Bahia"},{"Ceara","Ceará"},{"DistritoFederal","Distrito Federal"},
            {"EspiritoSanto","Espírito Santo"},{"Goias","Goiás"},{"Maranhao","Maranhão"},
            {"MatoGrosso","Mato Grosso"},{"MatoGrossoSul","Mato Grosso do Sul"},
            {"MinasGerais","Minas Gerais"},{"Para","Pará"},{"Paraiba","Paraíba"},
            {"Parana","Paraná"},{"Pernambuco","Pernambuco"},{"Piaui","Piauí"},
            {"RioGrandeNorte","Rio Grande do Norte"},{"RioGrandeSul","Rio Grande do Sul"},
            {"RioJaneiro","Rio de Janeiro"},{"Rondonia","Rondônia"},{"Roraima","Roraima"},
            {"SantaCatarina","Santa Catarina"},{"SaoPaulo","São Paulo"},{"Sergipe","Sergipe"},
            {"Tocantins","Tocantins"}
        };
        return map.ContainsKey(imageName) ? map[imageName] : imageName;
    }

    private Color ObterCorPorIntensidade(float t)
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

    private void CreateHorizontalGradient(VisualElement container)
    {
        container.Clear();
        container.style.flexDirection = FlexDirection.Row;
        for (int i = 0; i <= 10; i++)
        {
            float t = i / 10f;
            var box = new VisualElement();
            box.style.flexGrow = 1;
            box.style.backgroundColor = ObterCorPorIntensidade(t);
            container.Add(box);
        }
    }

    private void UpdatePieChart()
    {
        if (pieChartController == null) return;
        pieChartController.selectedEstado = string.IsNullOrEmpty(selectedEstado) ? null : selectedEstado;
        pieChartController.selectedCrimeIds = new List<int>(activeSelectedIds);
        pieChartController.selectedAno = selectedAno;
        pieChartController.Refresh();
    }

    // ========== EXPORTAÇÃO ==========
    private string GetDownloadsFolder()
    {
        string downloads = "";
#if UNITY_STANDALONE_WIN
        downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
#elif UNITY_STANDALONE_OSX
        downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
#elif UNITY_STANDALONE_LINUX
        downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
#elif UNITY_ANDROID
        downloads = Application.persistentDataPath;
#elif UNITY_IOS
        downloads = Application.persistentDataPath;
#else
        downloads = Application.persistentDataPath;
#endif
        if (!Directory.Exists(downloads))
            Directory.CreateDirectory(downloads);
        return downloads;
    }

    private string SanitizeFileName(string name)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
    }

    private void ExportData(string format)
    {
        StartCoroutine(ExportDataCoroutine(format));
    }

    private IEnumerator ExportDataCoroutine(string format)
    {
        var filters = new List<string>();
        if (activeSelectedIds != null && activeSelectedIds.Count > 0 && activeSelectedIds.Count < 20)
            filters.Add($"id_crime=in.({string.Join(",", activeSelectedIds)})");
        if (selectedAno.HasValue)
            filters.Add($"ano=eq.{selectedAno.Value}");
        if (!string.IsNullOrEmpty(selectedEstado))
            filters.Add($"estado=eq.{Uri.EscapeDataString(selectedEstado)}");

        string filterStr = filters.Count > 0 ? "&" + string.Join("&", filters) : "";
        string query = $"vw_ocorrencias?select=*{filterStr}";

        Debug.Log($"[Export] Buscando dados: {supabaseRestUrl}{query}");

        bool completed = false;
        string jsonData = "";
        yield return SupabaseRestClient.Get(supabaseRestUrl, supabasePublishableKey, query, (status, body, err) =>
        {
            if (string.IsNullOrEmpty(err))
                jsonData = body;
            else
                Debug.LogError($"Erro na exportação: {err}");
            completed = true;
        });
        yield return new WaitUntil(() => completed);

        if (string.IsNullOrEmpty(jsonData) || jsonData == "[]")
        {
            ShowExportNotification("Nenhum dado encontrado.");
            yield break;
        }

        var records = ParseJsonToRecords(jsonData);
        if (records.Count == 0)
        {
            ShowExportNotification("Nenhum dado encontrado.");
            yield break;
        }

        string fileContent;
        string extension;
        if (format == "csv")
        {
            fileContent = ConvertToCsv(records);
            extension = "csv";
        }
        else
        {
            fileContent = ConvertToHtmlTable(records);
            extension = "xls";
        }

        string baseName = string.IsNullOrEmpty(fileNameField?.value) ? "ocorrencias" : fileNameField.value;
        string safeName = SanitizeFileName(baseName);
        string fileName = $"{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.{extension}";
        string savePath = Path.Combine(GetDownloadsFolder(), fileName);

        try
        {
            Encoding utf8WithBom = new UTF8Encoding(true);
            File.WriteAllText(savePath, fileContent, utf8WithBom);
            ShowExportNotification($"Arquivo salvo em: {savePath}");
            Debug.Log($"Exportado: {savePath}");
        }
        catch (Exception e)
        {
            ShowExportNotification($"Erro ao salvar: {e.Message}");
            Debug.LogError($"Erro na exportação: {e}");
        }
    }

    private List<Dictionary<string, string>> ParseJsonToRecords(string json)
    {
        var list = new List<Dictionary<string, string>>();
        json = json.Trim();
        if (json.StartsWith("[")) json = json.Substring(1, json.Length - 2);
        var objectPattern = @"\{([^{}]*)\}";
        foreach (Match match in Regex.Matches(json, objectPattern))
        {
            var dict = new Dictionary<string, string>();
            string obj = match.Groups[1].Value;
            var pairPattern = "\"([^\"]+)\"\\s*:\\s*\"([^\"]*)\"";
            foreach (Match p in Regex.Matches(obj, pairPattern))
                dict[p.Groups[1].Value] = p.Groups[2].Value;
            var numPattern = "\"([^\"]+)\"\\s*:\\s*(\\d+)";
            foreach (Match p in Regex.Matches(obj, numPattern))
                dict[p.Groups[1].Value] = p.Groups[2].Value;
            if (dict.Count > 0)
                list.Add(dict);
        }
        return list;
    }

    private string ConvertToCsv(List<Dictionary<string, string>> records)
    {
        if (records.Count == 0) return "";
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(";", records[0].Keys));
        foreach (var rec in records)
        {
            var values = rec.Values.Select(v => v.Contains(";") ? $"\"{v}\"" : v);
            sb.AppendLine(string.Join(";", values));
        }
        return sb.ToString();
    }

    private string ConvertToHtmlTable(List<Dictionary<string, string>> records)
    {
        if (records.Count == 0) return "<html><body><p>Sem dados</p></body></html>";
        var sb = new StringBuilder();
        sb.AppendLine("<html><head><meta charset='UTF-8'><title>Ocorrências</title></head><body>");
        sb.AppendLine("<table border='1' cellpadding='5' cellspacing='0'>");
        sb.AppendLine("<tr>");
        foreach (var key in records[0].Keys)
            sb.AppendLine($"<th>{key}</th>");
        sb.AppendLine("</td>");
        foreach (var rec in records)
        {
            sb.AppendLine("<tr>");
            foreach (var val in rec.Values)
                sb.AppendLine($"<td>{val}</td>");
            sb.AppendLine("</tr>");
        }
        sb.AppendLine("表</body></html>");
        return sb.ToString();
    }

    private void ShowExportNotification(string message)
    {
        var notify = new Label(message);
        notify.style.position = Position.Absolute;
        notify.style.bottom = 20;
        notify.style.right = 20;
        notify.style.backgroundColor = new Color(0, 0, 0, 0.8f);
        notify.style.color = Color.white;
        notify.style.paddingLeft = 12;
        notify.style.paddingRight = 12;
        notify.style.paddingTop = 6;
        notify.style.paddingBottom = 6;
        root.Add(notify);
        root.schedule.Execute(() => notify.RemoveFromHierarchy()).ExecuteLater(3000);
    }
}