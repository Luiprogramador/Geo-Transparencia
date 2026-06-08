using System;
using UnityEngine;
using UnityEngine.UIElements;

public class DFController
{
    private VisualElement dfRoot;
    private VisualElement telaPrincipal;
    private VisualElement root;

    // Recebe o root principal e a tela principal (pra poder voltar)
    public DFController(VisualElement root, VisualElement telaPrincipal, VisualTreeAsset dfUxml)
    {
        this.root = root;
        this.telaPrincipal = telaPrincipal;

        // Instancia o mapa do DF a partir do uxml
        dfRoot = dfUxml.Instantiate();
        dfRoot.style.position = Position.Absolute;
        dfRoot.style.left = 0;
        dfRoot.style.top = 0;
        dfRoot.style.right = 0;
        dfRoot.style.bottom = 0;
        dfRoot.style.display = DisplayStyle.None; // começa escondido

        root.Add(dfRoot);

        // Botão voltar
        var voltarBtn = dfRoot.Q<Button>("VoltarBtn");
        if (voltarBtn != null)
            voltarBtn.clicked += Fechar;

        // Registrar clique nas regiões (provisório)
        var mapaDF = dfRoot.Q<VisualElement>("MapaDF");
        if (mapaDF != null)
        {
            foreach (var regiao in mapaDF.Children())
            {
                string nome = regiao.name;
                regiao.RegisterCallback<ClickEvent>(evt => OnRegiaoClicked(nome));
            }
        }
    }

    // Abre a tela do DF
    public void Abrir()
    {
        telaPrincipal.style.display = DisplayStyle.None;
        dfRoot.style.display = DisplayStyle.Flex;
    }

    // Volta pro mapa do Brasil
    private void Fechar()
    {
        dfRoot.style.display = DisplayStyle.None;
        telaPrincipal.style.display = DisplayStyle.Flex;
    }

    private void OnRegiaoClicked(string nomeRegiao)
    {
        Debug.Log($"[DF] Região clicada: {nomeRegiao}");
        // Aqui depois você coloca a lógica de dados da RA
    }
}
