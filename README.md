# Projeto integrador

---

## Projeto Segurança

- Mapa de calor, gráfico de pizza
- [https://www.ssp.sp.gov.br/estatistica/consulta](https://www.ssp.sp.gov.br/estatistica/consultas)s (dados relativamente agrupados em xlsx - São Paulo)
- [https://www.ispdados.rj.gov.br/estatistica.html](https://www.ispdados.rj.gov.br/estatistica.html) (dados relativamente agrupados em xlsx - Rio de Janeiro)
- [https://www.seguranca.mg.gov.br/index.php/transparencia/dados-abertos](https://www.seguranca.mg.gov.br/index.php/transparencia/dados-abertos) (espalhados - Minas Gerais)
- [https://observatorio.sesp.es.gov.br/infograficos](https://observatorio.sesp.es.gov.br/infograficos) (espalhados - Espirito Santo)
- [https://www.ssp.rs.gov.br/indicadores-criminais](https://www.ssp.rs.gov.br/indicadores-criminais) (agrupados - Rio Grande do Sul)
- [https://forumseguranca.org.br/publicacoes/anuario-brasileiro-de-seguranca-publica/](https://forumseguranca.org.br/publicacoes/anuario-brasileiro-de-seguranca-publica/) (centralizados por UF - sem municípios e apenas anual)
- [https://www.ssp.df.gov.br/dados-por-regiao-administrativa/](https://www.ssp.df.gov.br/dados-por-regiao-administrativa/) (por regiões do DF).

---

## Documentação – Sistema de Transparência Visual de Dados Públicos

## 1. Introdução

A transparência pública é um princípio fundamental da administração governamental e permite que a população acompanhe a utilização de recursos e indicadores sociais. No entanto, apesar da existência de diversos portais de transparência, os dados disponibilizados frequentemente apresentam dificuldades de interpretação devido à sua estrutura técnica, formato bruto e ausência de visualizações intuitivas.

Dados de segurança pública, por exemplo, são geralmente disponibilizados em planilhas extensas ou relatórios estatísticos que dificultam a compreensão por parte do público geral.

Este projeto propõe o desenvolvimento de um sistema que transforma dados públicos em **visualizações interativas**, facilitando a análise e interpretação dessas informações.

---

## 2. Problema

Os portais de transparência disponibilizam dados relevantes, porém apresentam dificuldades como:

- dados distribuídos em diferentes formatos
- inconsistências estruturais
- ausência de visualização intuitiva
- dificuldade de análise geográfica
- dificuldade de interpretação para usuários não técnicos

Isso limita o acesso real à informação pública.

---

## 3. Objetivo Geral

Desenvolver um sistema que permita visualizar dados públicos de segurança de forma intuitiva por meio de mapas e gráficos interativos.

---

## 4. Objetivos Específicos

- coletar dados públicos de segurança
- realizar limpeza e padronização dos dados
- estruturar os dados em formato unificado
- apresentar os dados em mapas de calor
- permitir filtragem por região, período e tipo de crime
- possibilitar expansão futura para outros datasets públicos

---

## 5. Justificativa

Embora os dados públicos estejam disponíveis, a dificuldade de interpretação limita seu impacto social.

A criação de ferramentas de visualização contribui para:

- maior transparência governamental
- acesso facilitado à informação
- apoio a pesquisas acadêmicas
- conscientização da população sobre indicadores sociais

---

## 6. Metodologia de Desenvolvimento

O projeto utilizará uma abordagem **incremental**, na qual o sistema será desenvolvido em módulos progressivos.

Cada incremento adicionará uma nova funcionalidade ao sistema, permitindo evolução contínua e validação progressiva.

O desenvolvimento também utilizará práticas inspiradas em metodologias ágeis, como organização de tarefas em backlog e desenvolvimento em ciclos curtos.

---

## 7. Arquitetura do Sistema

O sistema será dividido em módulos funcionais:

### 1 — Coleta de dados

Responsável por importar dados provenientes de portais públicos.

Fontes incluem bases governamentais e datasets estatísticos.

---

### 2 — Tratamento de dados

Etapa responsável por:

- limpeza de inconsistências
- padronização de campos
- conversão de formatos
- organização de registros

Exemplo de padronização:

```
estado
cidade
tipo_crime
data
quantidade
```

---

### 3 — Armazenamento local

Os dados tratados serão armazenados em um banco local para permitir funcionamento offline.

Tecnologia proposta: SQLite

---

### 4 — API interna

Camada responsável por fornecer os dados ao sistema de visualização.

Funções:

- filtragem
- agregação
- consulta por região ou período

---

### 5 — Interface de visualização

Responsável por apresentar os dados ao usuário de forma intuitiva.

Funcionalidades principais:

- mapa de calor nacional
- visualização por estado
- gráficos estatísticos
- filtros de busca

Prototipagem será realizada com Figma.

---

## 8. Tecnologias Utilizadas

Para desenvolvimento do sistema serão utilizadas as seguintes tecnologias:

Interface e aplicação:

- Flutter

Banco de dados:

- PostgreSQL

Prototipagem:

- Figma

Controle de versão:

- Git

---

## 9. Funcionalidades do Sistema

O sistema deverá oferecer as seguintes funcionalidades:

### Visualização geográfica

Mapa do Brasil apresentando distribuição de crimes por estado.

### Filtros de pesquisa

- por tipo de crime
- por período
- por região

### Visualização detalhada

Permitir acesso a dados detalhados ao selecionar uma região específica.

### Gráficos estatísticos

Exibição de gráficos para análise comparativa entre regiões.

---

## 10. Resultados Esperados

Espera-se que o sistema:

- facilite a interpretação de dados públicos
- permita análise visual de indicadores de segurança
- contribua para transparência e acesso à informação
- sirva como base para futuras expansões envolvendo outros datasets governamentais.

---

## Tasks iniciais para o ágil (Backlog inicial)

Agora a parte que ajuda MUITO na apresentação: backlog.

### Sprint 1 — Base do projeto

tasks:

- definir estrutura do projeto
- organizar datasets coletados
- identificar inconsistências principais
- definir modelo de dados unificado
- criar banco SQLite inicial

---

### Sprint 2 — Pipeline de dados

tasks:

- criar script de limpeza de dados
- padronizar estados e cidades
- padronizar datas
- normalizar tipos de crime
- inserir dados no banco

---

### Sprint 3 — Backend

tasks:

- criar consultas de dados
- implementar filtros
- agregar dados por estado
- agregar dados por cidade

---

### Sprint 4 — Interface inicial

tasks:

- criar layout base
- integrar mapa do Brasil
- carregar dados do banco
- exibir mapa de calor

---

### Sprint 5 — Visualizações

tasks:

- gráficos de crimes por estado
- gráfico temporal
- filtros interativos

---

### Sprint 6 — refinamento

tasks:

- melhoria de UX
- otimização de consultas
- preparação para expansão de datasets

---

## Estrutura do figma

- Home (pagina agradável, com caminhos explicativos como: segurança publica, como utilizar (guia do app))
- pagina segurança (mapa de calor do pais podendo descer ate as cidade, ao lado um gráfico de pizza ou dashboard pra analise dos crimes)
- Guia do app explicara os padrões de interface e como usar o  sistema.

---

# Empresas possíveis para contato(stakeholder)

### 1. GovTechs (Empresas de Tecnologia para o Governo)

São empresas privadas que criam softwares para prefeituras e estados. Elas amam projetos que "limpam" dados públicos, pois isso é exatamente o que elas vendem.

- **Colab:** Uma das maiores GovTechs do Brasil, focada em gestão colaborativa e zeladoria urbana.
- **Aprova Digital:** Focada em digitalizar processos públicos.
- **Gove:** Especializada em inteligência de dados para gestão pública municipal.

### 2. Empresas de Segurança Privada e Monitoramento

Essas empresas usam dados de manchas criminais para planejar onde colocar câmeras, viaturas e rotas de vigilância.

- **Grupo Verzani & Sandrini:** Uma das gigantes do setor de segurança patrimonial.
- **Abitante ou Gocil:** Empresas que gerenciam grandes operações de segurança e poderiam validar se o "mapa de calor" de vocês ajuda na tomada de decisão estratégica deles.
- **Gabriel:** Aquela empresa de câmeras inteligentes espalhadas por bairros (muito forte em SP e RJ). Eles têm um interesse direto em dados de segurança pública para correlacionar com as imagens deles.

### 3. Empresas de Logística e Transporte

O roubo de carga e a segurança de motoristas é uma dor latente. Ter um mapa de calor unificado ajuda essas empresas a definir rotas seguras.

- **Loggi:** Empresa de logística que depende fortemente de dados de segurança para suas rotas de entrega.
- **Localiza ou Movida:** Como gestoras de frotas, a segurança dos veículos e dos usuários é um dado de interesse.
- **Gerenciadoras de Risco (ex: Opentech ou Buonny):** Elas vivem de analisar dados de sinistros e crimes para seguradoras. O modelo de vocês é um "prato cheio" para elas.

### 4. Institutos e Think Tanks (Altamente Recomendado)

Embora não sejam "empresas" no sentido comercial estrito, a assinatura de um diretor de um desses institutos tem um peso acadêmico gigantesco:

- **Instituto Sou da Paz:** Referência em análise de dados de segurança.
- **Instituto Igarapé:** Focado em segurança pública e dados. Eles criaram o "Monitor de Homicídios", algo parecido com o que vocês propõem.
- **Fórum Brasileiro de Segurança Pública:** Se conseguirem um contato aqui, o projeto de vocês ganha "selo de qualidade" máximo.

---

## Protótipo interface.

[UX Pilot - Superfast UX Design with AI](https://uxpilot.ai/s/864f480e33f823e44816b4293e751b67)

---

## Modelo Banco de dados

![Captura_de_tela_2026-04-06_101058](https://github.com/user-attachments/assets/bbf47429-22fc-4a4a-af81-f4a3331a2603)

[modelo_conceitual.brM3](modelo_conceitual.brm3)

![Captura_de_tela_2026-04-06_104944](https://github.com/user-attachments/assets/60acc7d9-1fc1-4cc4-8ab3-637dd54dad78)

[modelo_logico.brM3](modelo_logico.brm3)
