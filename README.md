# Quality Gate CLI

Orquestrador automatizado de qualidade de código com múltiplos gates para aplicações .NET 10.

## Visão Geral

O Quality Gate executa um pipeline automatizado com 7 verificações de qualidade:
1. **Gate de Compilação (`Build Gate`)**: Compila os projetos afetados tratando avisos como erros (`TreatWarningsAsErrors`).
2. **Gate de Testes (`Test Gate`)**: Executa a suíte de testes dos módulos alterados e valida os resultados.
3. **Gate de Cobertura (`Coverage Gate`)**: Aplica limiares mínimos de cobertura de linhas e branches (geral e para código alterado).
4. **Gate de Complexidade (`Complexity Gate`)**: Analisa complexidade ciclomática, linhas por método e linhas por classe via AST do Roslyn.
5. **Gate de Análise Estática (`Static Analysis Gate`)**: Detecta novos avisos do compilador e analisadores de código.
6. **Gate de Arquitetura (`Architecture Gate`)**: Valida regras de arquitetura em camadas e dependências proibidas entre módulos.
7. **Gate de Mutação (`Mutation Gate`)**: Orquestra testes de mutação com Stryker.NET.

---

## Instalação e Download da CLI

O Quality Gate é distribuído como um executável autônomo em arquivo único (*single-file*), portanto não requer instalação prévia do runtime do .NET para sua execução.

### 1. Baixar a Versão no GitHub
Acesse a página de **[Releases](../../releases)** e baixe o pacote correspondente ao seu sistema operacional e arquitetura:

| Plataforma | Pacote | Executável |
|---|---|---|
| **Linux (x64)** | `qualitygate-v<versao>-linux-x64.tar.gz` | `qualitygate` |
| **Linux (ARM64)** | `qualitygate-v<versao>-linux-arm64.tar.gz` | `qualitygate` |
| **Windows (x64)** | `qualitygate-v<versao>-win-x64.zip` | `qualitygate.exe` |
| **macOS (Intel)** | `qualitygate-v<versao>-osx-x64.tar.gz` | `qualitygate` |
| **macOS (Apple Silicon)** | `qualitygate-v<versao>-osx-arm64.tar.gz` | `qualitygate` |

### 2. Extração e Execução

**No Linux ou macOS:**
```sh
tar -xzvf qualitygate-v1.0.0-linux-x64.tar.gz
chmod +x qualitygate
./qualitygate version
```

**No Windows (PowerShell):**
```powershell
Expand-Archive qualitygate-v1.0.0-win-x64.zip
.\qualitygate.exe version
```

> **Dica:** Adicione a pasta com o executável à variável de ambiente `PATH` para poder chamá-lo diretamente como `qualitygate` a partir de qualquer diretório.

---

## Pré-requisitos de Uso

Para que a ferramenta consiga compilar e inspecionar o projeto .NET alvo:

### 1. Obrigatórios
- **.NET 10 SDK** (versão 10.0 ou superior):
  ```sh
  dotnet --version
  ```
- **Git CLI** (disponível no PATH para resolução de diferenças e commits):
  ```sh
  git --version
  ```

### 2. Opcionais (por Gate)
- **Gate de Mutação (`MutationGate`)**:
  ```sh
  dotnet tool install --global dotnet-stryker
  ```
- **Relatórios Visuais HTML de Cobertura**:
  ```sh
  dotnet tool install --global dotnet-reportgenerator-globaltool
  ```

---

## Como Usar

Execute os comandos a partir da pasta raiz do repositório .NET que você deseja analisar.

### Verificações de Qualidade (`check`)

```sh
# Modo padrão em repositório Git: avalia apenas o código alterado em relação ao commit base
qualitygate check --diff


# Validação pré-commit local: inclui alterações não commitadas (working tree, staged e untracked)
qualitygate check --diff --working-tree # ou -w
# Repositório completo
qualitygate check --repository

# Projeto específico
qualitygate check --project src/MeuProjeto/MeuProjeto.csproj

# Namespace específico
qualitygate check --namespace MeuApp.Features.Pedidos

# Saída em formato JSON (imprime JSON e grava .qualitygate/artifacts/<runId>/report.json)
qualitygate check --diff --format json

# Saída em formato Markdown (imprime Markdown e grava .qualitygate/artifacts/<runId>/report.md)
qualitygate check --diff --format markdown # ou --format md

# Saída dupla: console no terminal e relatórios JSON e MD gravados em .qualitygate/artifacts/<runId>/
qualitygate check --diff --format both
# Pular gates específicos
qualitygate check --diff --skip mutation,staticanalysis

# Executar exclusivamente gates selecionados
qualitygate check --diff --only build,test,coverage

# Executar testes de mutação apenas no código alterado (modo Diff)
qualitygate check --diff --only mutation

# Executar com verificação em catraca (ratchet) contra a baseline gravada
qualitygate check --diff --ratchet --baseline .qualitygate/baseline.json

# Interromper imediatamente no primeiro gate que reprovar
qualitygate check --diff --fail-fast

# Saída detalhada de diagnóstico técnico com telemetria em tempo real
qualitygate check --diff --verbose
```

### Sistema de Catraca (Ratchet)

O modo de catraca (`--ratchet`) assegura que a qualidade do código **nunca regrida** em relação ao patamar já registrado:
- **No Modo Diff (`--diff`)**: Atua sobre as **features afetadas**, garantindo que a cobertura de testes daquela feature não caiu em relação à baseline gravada. Regras de código novo (novas violações de arquitetura e novos warnings) são barradas com tolerância zero pelos respectivos gates (`ArchitectureGate` e `StaticAnalysisGate`).
- **No Modo Repositório (`--repository`)**: Avalia o repositório consolidado e impede qualquer aumento na dívida técnica global (violações de arquitetura acumuladas, total de warnings e queda na cobertura global).
- **Opção `--baseline <caminho>`**: Permite apontar o caminho do arquivo de baseline (o padrão adotado é `.qualitygate/baseline.json`).
### Gravação de Nova Baseline (`baseline record`)

Para registrar o estado atual de métricas do repositório como o novo patamar mínimo de qualidade:

```sh
qualitygate baseline record --output .qualitygate/baseline.json
```

### Opções da Linha de Comando (`check`)

| Opção | Tipo | Padrão | Descrição |
|---|---|---|---|
| `--diff` | Sinalizador | `true` (em Git) | Avalia exclusivamente os arquivos alterados |
| `--repository` | Sinalizador | — | Avalia todo o repositório |
| `--project <caminho>` | Texto | — | Avalia um projeto `.csproj` específico |
| `--namespace <ns>` | Texto | — | Avalia um namespace específico |
| `--base <ref>` | Texto | `HEAD~1` / merge-base | Referência Git base para comparação de diff |
| `--working-tree`, `-w` | Sinalizador | `false` | Inclui alterações locais não comitadas (staged, unstaged e untracked) |
| `--format <formato>` | Enumeração | `console` | Formato de saída: `console`, `json`, `markdown` (ou `md`), ou `both` |
| `--skip <gates>` | Lista | — | Gates ignorados (`build,test,coverage,complexity,staticanalysis,architecture,mutation`) |
| `--only <gates>` | Lista | — | Executa apenas os gates informados |
| `--fail-fast` | Sinalizador | `false` | Para a execução logo após a primeira falha |
| `--verbose` | Sinalizador | `false` | Exibe detalhes de diagnóstico (caminhos, tempos, resoluções) |
| `--config <caminho>` | Texto | `qualitygate.json` | Caminho para arquivo de configuração personalizado |
| `--ratchet` | Sinalizador | `false` | Habilita verificação em catraca contra a baseline |
| `--baseline <caminho>` | Texto | `.qualitygate/baseline.json` | Caminho do arquivo de baseline para verificação |

### Códigos de Saída da CLI

| Código | Identificador | Descrição |
|---|---|---|
| `0` | `Success` | Todos os gates avaliados foram aprovados |
| `1` | `QualityFailure` | Um ou mais gates reprovaram por não atingir os limiares |
| `2` | `ConfigurationError` | Parâmetros inválidos ou arquivo de configuração malformado |
| `3` | `InfrastructureError` | Falha de ferramenta externa (.NET SDK, Git ou falha de processo) |
| `4` | `ScopeError` | O escopo alvo da análise não pôde ser resolvido |
| `5` | `InternalError` | Ocorreu uma exceção interna não tratada |

---

## Configuração do Projeto (`qualitygate.json`)

### Ordem de Busca da Configuração
Ao iniciar, o Quality Gate busca o arquivo de regras na seguinte ordem:
1. Caminho explícito passado pelo parâmetro `--config <caminho>`
2. Raiz do projeto alvo: `./qualitygate.json`
3. Pasta padrão da ferramenta: `./.qualitygate/qualitygate.json`
4. Se nenhum arquivo for localizado, adota os valores padrão embutidos.

### Modelo de Exemplo
Para configurar o seu projeto, utilize o modelo de exemplo `qualitygate.sample.json` presente neste repositório. Basta copiá-lo para o seu projeto:

```sh
cp qualitygate.sample.json meu-projeto/.qualitygate/qualitygate.json
```

### Diretório de Artefatos Gerados
Todos os relatórios (`report.json`, `report.md`) e saídas intermediárias de cobertura gerados pela CLI são gravados exclusivamente em:
```
.qualitygate/artifacts/<runId>/
```

### Exclusão de Código Gerado e Padrões Ignorados (`ignorePatterns`)
Por padrão, a CLI ignora automaticamente código gerado por ferramentas do .NET em memória (sem custo de I/O):
- `*.Designer.cs` (metadados de migrations do EF Core, Windows Forms, etc.)
- `*ModelSnapshot.cs` (snapshot de modelo do EF Core, independentemente do nome da pasta)
- `*.g.cs` e `*.g.i.cs` (Roslyn Source Generators, Razor, gRPC, NSwag)

Caso seu projeto use diretórios customizados de migrations ou pastas com código gerado/legado, declare `ignorePatterns` no `qualitygate.json`:
```json
"changedCode": {
  "ignorePatterns": [
    "**/DatabaseMigrations/**",
    "**/CustomGenerated/**"
  ]
}
```

### Isenção de Projetos de Teste na Complexidade
Por padrão, o `ComplexityGate` avalia exclusivamente **código de produção**, isentando projetos de teste identificados automaticamente pelo `ProjectDiscovery` (como testes unitários, testes de integração com `WebApplicationFactory` e testes E2E). Para alterar esse comportamento, configure:
```json
"changedCode": {
  "ignoreTestProjectsInComplexity": true
}
```
Adicione a pasta `.qualitygate/artifacts/` ao arquivo `.gitignore` do seu projeto.

---

## Integração Contínua em Projetos Clientes (GitHub Actions)

A forma recomendada e mais simples de integrar o Quality Gate em pipelines do GitHub Actions é através da **Action Composta Oficial** (`WalissonPires/quality-gate-dotnet@v1.0.6`), que gerencia automaticamente a resolução da plataforma (Linux, Windows, macOS x64/arm64), download do binário, extração de baseline da branch de destino, execução das verificações e publicação do relatório Markdown no `GITHUB_STEP_SUMMARY`.

### 1. Verificação de Pull Request com Catraca (`.github/workflows/pr-quality-ratchet.yml`)

```yaml
name: Quality Gate

on:
  pull_request:
    branches: [ main, master ]

jobs:
  quality-gate:
    name: Quality Gate Check
    runs-on: ubuntu-latest
    steps:
      - name: Checkout repository
        uses: actions/checkout@v4
        with:
          fetch-depth: 0 # Necessário para histórico Git e diff

      - name: Setup .NET 10 SDK
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Run Quality Gate
        uses: WalissonPires/quality-gate-dotnet@v1.0.6
        with:
          diff: 'true'
          ratchet: 'true'
          format: 'both'
          summary: 'true'
```

### 2. Atualização Automática da Baseline no Merge (`.github/workflows/update-quality-baseline.yml`)

```yaml
name: Update Quality Baseline

on:
  push:
    branches: [ main ]

jobs:
  record-baseline:
    name: Record Baseline
    runs-on: ubuntu-latest
    permissions:
      contents: write
    steps:
      - name: Checkout repository
        uses: actions/checkout@v4

      - name: Setup .NET 10 SDK
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Record Quality Baseline
        uses: WalissonPires/quality-gate-dotnet@v1.0.6
        with:
          command: 'baseline-record'
          baseline-path: '.qualitygate/baseline.json'

      - name: Commit and Push Baseline
        run: |
          git config user.name "github-actions[bot]"
          git config user.email "github-actions[bot]@users.noreply.github.com"
          git add .qualitygate/baseline.json
          git diff --staged --quiet || (git commit -m "chore(quality): update metrics baseline [skip ci]" && git push)
```

### Versionamento da Action

A action segue o versionamento SemVer padrão do repositório (`v1.0.6`, `v1.0.5`, etc.). Para fixar a versão em seus workflows, referencie a tag da release desejada:
- `uses: WalissonPires/quality-gate-dotnet@v1.0.6`: Executa a versão `v1.0.6`.

### Referência de Entradas (`inputs`)

| Entrada | Descrição | Padrão |
|---|---|---|
| `version` | Versão da CLI a baixar (`latest`, `1.0.4`, `v1.0.4`) | `latest` |
| `command` | Modo de execução: `check`, `baseline-record` ou `setup` (apenas instala no `PATH`) | `check` |
| `diff` | Avaliar apenas código modificado contra a referência base Git | `true` |
| `working-tree` | Incluir alterações locais não commitadas (staged, unstaged, untracked) | `false` |
| `repository` | Avaliar o repositório completo | `false` |
| `project` | Caminho para um arquivo de projeto específico (`.csproj`) | `''` |
| `namespace` | Namespace C# específico a avaliar | `''` |
| `base` | Referência Git base para comparação de diff (`origin/main`, `HEAD~1`) | `''` |
| `ratchet` | Ativar modo catraca contra baseline para prevenir regressão | `false` |
| `baseline-path` | Caminho explícito para o arquivo JSON de baseline | `''` |
| `auto-extract-baseline` | Se `ratchet: 'true'` e `baseline-path` vazio, extrai automaticamente `.qualitygate/baseline.json` da branch de destino | `true` |
| `baseline-branch` | Branch/ref para extração da baseline (padrão: `github.base_ref` ou `origin/main`) | `''` |
| `format` | Formato de saída no console (`both`, `console`, `markdown`, `json`) | `both` |
| `skip` | Gates a ignorar separados por vírgula (`coverage,mutation`) | `''` |
| `only` | Gates a executar com exclusividade (`build,test`) | `''` |
| `fail-fast` | Interromper a execução no primeiro gate que falhar | `false` |
| `verbose` | Ativar saída detalhada de diagnóstico | `false` |
| `config` | Caminho para o arquivo de configuração `qualitygate.json` | `qualitygate.json` |
| `summary` | Publicar relatório Markdown no `$GITHUB_STEP_SUMMARY` | `true` |
| `token` | Token do GitHub para download de releases | `${{ github.token }}` |
| `repo` | Repositório que hospeda as releases | `WalissonPires/quality-gate-dotnet` |
| `qualitygate-path` | Caminho para binário pré-existente (ignora download) | `''` |
| `install-dir` | Diretório para instalar o binário baixado | `${{ runner.temp }}/qualitygate-bin` |
| `additional-args` | Argumentos extras repassados diretamente à CLI | `''` |

### Referência de Saídas (`outputs`)

| Saída | Descrição |
|---|---|
| `exit-code` | Código de saída da CLI (`0`=Sucesso, `1`=Falha de Qualidade, `2`=Configuração, `3`=Infraestrutura, `4`=Escopo, `5`=Erro Interno) |
| `passed` | String booleana (`true` ou `false`) indicando se a esteira passou |
| `run-id` | GUID único da execução gerado pela CLI |
| `artifacts-dir` | Caminho para o diretório de artefatos `.qualitygate/artifacts/<runId>` |
| `report-json` | Caminho para o arquivo `report.json` gerado |
| `report-md` | Caminho para o arquivo `report.md` gerado |
| `cli-version` | Versão da CLI Quality Gate instalada ou executada |

### Instalação Manual da CLI (Ambientes Customizados)

Para ambientes fora do GitHub Actions (como GitLab CI, Azure DevOps ou scripts locais), baixe os binários diretamente da página de [Releases](https://github.com/WalissonPires/quality-gate-dotnet/releases) correspondentes à sua plataforma (`linux-x64`, `linux-arm64`, `win-x64`, `osx-x64`, `osx-arm64`) e torne o executável disponível no seu `PATH`.
---

## Desenvolvimento e Contribuição

Esta seção é destinada a desenvolvedores que queiram compilar, testar ou contribuir com o código-fonte deste projeto.

### Compilação Local da Solução

```sh
dotnet restore QualityGate.sln
dotnet build QualityGate.sln --configuration Release
```

### Execução da Suíte de Testes Automatizados

```sh
dotnet test QualityGate.sln --configuration Release
```

### Executar a CLI em Modo de Desenvolvimento

Durante o desenvolvimento, você pode rodar a CLI diretamente sem precisar gerar o executável publicado:

```sh
dotnet run --project src/QualityGate -- check --diff
dotnet run --project src/QualityGate -- version
```

### Pipelines de CI/CD do Repositório

O repositório conta com duas automações via GitHub Actions:
- **`CI - Build and Test` (`.github/workflows/ci.yml`)**: Restaura, compila com avisos como erros e roda os 106 testes unitários a cada Pull Request e push na branch principal.
- **`Release` (`.github/workflows/release.yml`)**: Gera executáveis autônomos *single-file* para as 5 plataformas (Linux x64/ARM64, Windows x64, macOS Intel/Apple Silicon), calcula os resumos de integridade SHA-256 e cria a Release no GitHub.

### Como Publicar uma Nova Versão da Ferramenta

Para gerar e publicar uma nova release com versionamento semântico automático:

```sh
# 1. Crie uma tag semântica (ex: v1.0.0, v1.1.0)
git tag v1.0.0

# 2. Envie a tag para o repositório remoto no GitHub
git push origin v1.0.0
```

O pipeline de release iniciará automaticamente, executará todos os testes e disponibilizará a nova versão com todos os binários compactados prontos para download.
