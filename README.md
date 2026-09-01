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

# Saída detalhada de diagnóstico técnico
qualitygate check --diff --verbose
```

### Sistema de Catraca (Ratchet)

O modo de catraca (`--ratchet`) assegura que a qualidade do código **nunca regrida**:
- **Modo Diff**: Mapeia os arquivos modificados e garante que a cobertura de código e as violações de arquitetura não piorem em relação à baseline gravada.
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
Adicione a pasta `.qualitygate/artifacts/` ao arquivo `.gitignore` do seu projeto.

---

## Integração Contínua em Projetos Clientes (GitHub Actions)

Abaixo estão exemplos de como integrar a CLI do Quality Gate nos fluxos de trabalho de qualquer repositório .NET cliente:

### 1. Verificação de Pull Request com Catraca (`.github/workflows/qualidade.yml`)

```yaml
name: Quality Gate

on:
  pull_request:
    branches: [ main, master ]

jobs:
  verificacao:
    runs-on: ubuntu-latest
    steps:
      - name: Obter código do repositório
        uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Configurar .NET 10 SDK
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Baixar CLI do Quality Gate
        run: |
          gh release download --repo <org>/quality-gate-dotnet --pattern "qualitygate-*-linux-x64.tar.gz"
          tar -xzvf qualitygate-*-linux-x64.tar.gz
          chmod +x qualitygate
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}

      - name: Extrair Baseline da Branch Principal
        run: |
          mkdir -p /tmp/ratchet
          git show origin/main:.qualitygate/baseline.json > /tmp/ratchet/main-baseline.json 2>/dev/null || true

      - name: Executar Verificação de Qualidade
        run: |
          ./qualitygate check --diff --ratchet --baseline /tmp/ratchet/main-baseline.json --format both

      - name: Publicar Relatório no GitHub Actions Summary
        if: always()
        run: |
          REPORT_MD=$(find .qualitygate/artifacts -name "report.md" | sort | tail -n 1)
          if [ -n "$REPORT_MD" ] && [ -f "$REPORT_MD" ]; then
            cat "$REPORT_MD" >> $GITHUB_STEP_SUMMARY
          fi
```
### 2. Atualização Automática da Baseline no Merge (`.github/workflows/atualizar-baseline.yml`)

```yaml
name: Atualizar Baseline de Qualidade

on:
  push:
    branches: [ main ]

jobs:
  gravar-baseline:
    runs-on: ubuntu-latest
    permissions:
      contents: write
    steps:
      - name: Obter código do repositório
        uses: actions/checkout@v4

      - name: Configurar .NET 10 SDK
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Baixar CLI do Quality Gate
        run: |
          gh release download --repo <org>/quality-gate-dotnet --pattern "qualitygate-*-linux-x64.tar.gz"
          tar -xzvf qualitygate-*-linux-x64.tar.gz
          chmod +x qualitygate
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}

      - name: Gravar e Registrar Nova Baseline
        run: |
          ./qualitygate baseline record --output .qualitygate/baseline.json
          git config user.name "github-actions[bot]"
          git config user.email "github-actions[bot]@users.noreply.github.com"
          git add .qualitygate/baseline.json
          git diff --staged --quiet || (git commit -m "chore(qualidade): atualizar baseline de metricas [skip ci]" && git push)
```

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
