using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

public class MultiSelectDropdown : VisualElement
{
    public class Item
    {
        public int id;
        public string name;
        public Item(int id, string name) { this.id = id; this.name = name; }
    }

    private Label titleLabel;
    private Button toggleButton;
    private ScrollView listView;
    private List<Item> items = new List<Item>();
    private HashSet<int> selectedIds = new HashSet<int>();
    private Toggle allToggle;
    public event Action<IEnumerable<int>> OnSelectionChanged;

    public MultiSelectDropdown()
    {
        var container = new VisualElement { style = { flexDirection = FlexDirection.Column } };
        titleLabel = new Label();
        titleLabel.AddToClassList("msd-title");
        toggleButton = new Button { text = "Todos" };
        toggleButton.AddToClassList("msd-button");
        toggleButton.clicked += () => listView.visible = !listView.visible;
        listView = new ScrollView { style = { maxHeight = 180 }, visible = false };
        listView.AddToClassList("msd-list");
        container.Add(titleLabel);
        container.Add(toggleButton);
        container.Add(listView);
        Add(container);
    }

    public void SetTitle(string title) => titleLabel.text = title;
    public void SetItems(IEnumerable<Item> values) { items = values.ToList(); BuildList(); }

    private void BuildList()
    {
        listView.Clear();
        allToggle = new Toggle("Todos");
        allToggle.RegisterValueChangedCallback(evt =>
        {
            bool v = evt.newValue;
            foreach (var child in listView.Children())
                if (child is Toggle tog && tog != allToggle)
                    tog.SetValueWithoutNotify(v);
            if (v) selectedIds = new HashSet<int>(items.Select(i => i.id));
            else selectedIds.Clear();
            OnSelectionChanged?.Invoke(selectedIds);
            UpdateButtonText();
        });
        listView.Add(allToggle);

        foreach (var item in items)
        {
            var tog = new Toggle(item.name);
            tog.userData = item.id;
            tog.RegisterValueChangedCallback(evt =>
            {
                int id = (int)tog.userData;
                if (evt.newValue) selectedIds.Add(id);
                else selectedIds.Remove(id);
                UpdateAllToggleState();
                UpdateButtonText();
                OnSelectionChanged?.Invoke(selectedIds);
            });
            listView.Add(tog);
        }
        UpdateAllToggleState();
        UpdateButtonText();
    }

    private void UpdateAllToggleState()
    {
        bool allOn = items.Count > 0 && items.All(i => selectedIds.Contains(i.id));
        allToggle.SetValueWithoutNotify(allOn);
    }

    private void UpdateButtonText()
    {
        if (selectedIds.Count == 0) toggleButton.text = "Nenhum";
        else if (selectedIds.Count == items.Count) toggleButton.text = "Todos";
        else if (selectedIds.Count == 1)
        {
            var first = items.First(i => selectedIds.Contains(i.id));
            toggleButton.text = first.name;
        }
        else toggleButton.text = $"{selectedIds.Count} selecionados";
    }

    public IEnumerable<int> SelectedIds => selectedIds;
}