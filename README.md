# Wamage Quality Gate

Automated, multi-gate code quality orchestrator for the Wamage backend in .NET 10.

## Overview

Quality Gate executes a pipeline of 7 quality checks:
1. **Build Gate**: Compiles affected projects with warnings-as-errors.
2. **Test Gate**: Executes tests for affected features and validates results.
3. **Coverage Gate**: Enforces changed-code and global line/branch coverage thresholds.
4. **Complexity Gate**: Analyzes cyclomatic complexity, method lines, and class lines using Roslyn AST.
5. **Static Analysis Gate**: Detects new compiler and analyzer warnings.
6. **Architecture Gate**: Validates layering and forbidden dependency rules per architectural boundary.
7. **Mutation Gate**: Orchestrates Stryker.NET mutation testing.
---

## Pré-requisitos e Instalação de Dependências

### 1. Dependências Obrigatórias

Para executar os gates padrão (**Build**, **Test**, **Coverage**, **Complexity**, **Static Analysis**, **Architecture**):

- **.NET 10 SDK** (versão 10.0 ou superior):
  ```sh
  dotnet --version
  ```
- **Git CLI** (disponível no PATH para resolução de diff e commits):
  ```sh
  git --version
  ```

### 2. Dependências Opcionais (por Gate)

#### Gate de Mutação (`MutationGate`)
Para executar testes de mutação com o Stryker.NET:
```sh
dotnet tool install --global dotnet-stryker
```
*Verificação*: `dotnet stryker --help` ou `dotnet-stryker --help`

#### Relatórios HTML de Cobertura (Opcional)
Caso deseje gerar relatórios visuais HTML a partir dos arquivos Cobertura XML:
```sh
dotnet tool install --global dotnet-reportgenerator-globaltool
```

---

## Instalação e Restauração do Quality Gate

Na raiz do repositório, compile e restaure o Quality Gate:

```sh
dotnet restore .tools/quality-gate/QualityGate.sln
dotnet build .tools/quality-gate/QualityGate.sln --configuration Release
```

---

## CLI Usage

### Running Quality Checks

```sh
# Diff scope (default in git repo) - checks only changed files vs base commit
dotnet run --project .tools/quality-gate/src/QualityGate -- check --diff

# Specific namespace
dotnet run --project .tools/quality-gate/src/QualityGate -- check --namespace Wamage.Features.Persons

# Specific project
dotnet run --project .tools/quality-gate/src/QualityGate -- check --project backend/Wamage.csproj

# Full repository check
dotnet run --project .tools/quality-gate/src/QualityGate -- check --repository

# Output JSON report
dotnet run --project .tools/quality-gate/src/QualityGate -- check --diff --format json

# Both console and JSON output (writes to .artifacts/<runId>/report.json)
dotnet run --project .tools/quality-gate/src/QualityGate -- check --diff --format both

# Skip specific gates
dotnet run --project .tools/quality-gate/src/QualityGate -- check --diff --skip mutation,staticanalysis

# Only run specific gates
dotnet run --project .tools/quality-gate/src/QualityGate -- check --diff --only build,test,coverage

# Run mutation testing on a specific feature namespace
dotnet run --project .tools/quality-gate/src/QualityGate -- check --namespace Wamage.Features.Persons --only mutation

# Run mutation testing only on changed code (Diff mode)
dotnet run --project .tools/quality-gate/src/QualityGate -- check --diff --only mutation
# Execute quality check with ratchet verification against main baseline
dotnet run --project .tools/quality-gate/src/QualityGate -- check --diff --ratchet --baseline .tools/quality-gate/baseline.json

# Fail fast on first error
dotnet run --project .tools/quality-gate/src/QualityGate -- check --diff --fail-fast

# Enable verbose diagnostic output
dotnet run --project .tools/quality-gate/src/QualityGate -- check --diff --verbose
```

### Sistema de Catraca (Quality Ratchet)

O modo de catraca (`--ratchet`) garante que métricas de código (cobertura, avisos do compilador, violações arquiteturais e complexidade) **nunca regridam**:
- **Modo Diff**: Mapeia as features alteradas e garante que a cobertura da feature e violações arquiteturais não piorem em relação à baseline.
- **Opção `--baseline <path>`**: Permite sobrescrever o caminho do arquivo `baseline.json` (por exemplo, ao comparar contra uma baseline extraída da branch `main` em CI).

### Gravação de Nova Baseline (`baseline record`)

Gera um novo snapshot consolidado das métricas globais e por feature:

```sh
dotnet run --project .tools/quality-gate/src/QualityGate -- baseline record --output .tools/quality-gate/baseline.json
```

### CLI Options (`check` command)

| Option | Type | Default | Description |
|---|---|---|---|
| `--diff` | Flag | `true` (in git) | Evaluate only changed code |
| `--namespace <ns>` | String | — | Evaluate a specific namespace |
| `--project <path>` | String | — | Evaluate a specific `.csproj` project |
| `--repository` | Flag | — | Evaluate the entire repository |
| `--base <ref>` | String | `HEAD~1` / merge-base | Git base reference for diff comparison |
| `--format <fmt>` | Enum | `console` | Output format: `console`, `json`, `both` |
| `--skip <gates>` | List | — | Gates to skip (comma-separated: `build,test,coverage,complexity,staticanalysis,architecture,mutation`) |
| `--only <gates>` | List | — | Only run specified gates (comma-separated) |
| `--fail-fast` | Flag | `false` | Stop execution immediately after the first failing gate |
| `--verbose` | Flag | `false` | Enable detailed diagnostic output (SDK versions, paths, timings, target resolution) |
| `--config <path>` | String | `qualitygate.json` | Path to custom configuration file |
| `--ratchet` | Flag | `false` | Enable quality ratchet verification against baseline |
| `--baseline <path>` | String | `.tools/quality-gate/baseline.json` | Path to baseline JSON file for ratchet comparison |

### Version Command
```sh
dotnet run --project .tools/quality-gate/src/QualityGate -- version
```

---

## Exit Codes

| Code | Name | Description |
|---|---|---|
| `0` | Success | All evaluated quality gates passed |
| `1` | QualityFailure | One or more gates failed quality thresholds |
| `2` | ConfigurationError | Invalid CLI arguments, conflicting options, or malformed config |
| `3` | InfrastructureError | External tool failure (.NET SDK, git, or tool crash) |
| `4` | ScopeError | Target scope could not be resolved |
| `5` | InternalError | Unexpected internal exception |

---

## Configuration (`qualitygate.json`)

The default configuration file is located at `.tools/quality-gate/qualitygate.json`:

```json
{
  "version": 1,
  "changedCode": {
    "lineCoverage": 80,
    "branchCoverage": 70,
    "maxCyclomaticComplexity": 10,
    "maxMethodLines": 40,
    "maxClassLines": 300,
    "newWarnings": 0
  },
  "global": {
    "lineCoverage": null,
    "branchCoverage": null
  },
  "mutation": {
    "enabled": false,
    "minimumScore": 70
  },
  "execution": {
    "failFast": false,
    "unitTests": true,
    "integrationTests": false,
    "e2eTests": false,
    "processTimeoutSeconds": 300
  },
  "architecture": {
    "rules": [
      {
        "name": "DomainIsolation",
        "source": "Wamage.Features.*.Domain",
        "forbiddenDependencies": [
          "Microsoft.EntityFrameworkCore",
          "Microsoft.AspNetCore",
          "MediatR",
          "Wamage.Features.*.Infrastructure",
          "Wamage.Features.*.API"
        ],
        "strict": true
      },
      {
        "name": "ApplicationIsolation",
        "source": "Wamage.Features.*.Application",
        "forbiddenDependencies": [
          "Microsoft.EntityFrameworkCore",
          "Wamage.Features.*.Infrastructure",
          "Wamage.Features.*.API",
          "Wamage.Shared.Database.AppDbContext"
        ],
        "strict": true
      },
      {
        "name": "ApiIsolation",
        "source": "Wamage.Features.*.API",
        "forbiddenDependencies": [
          "Wamage.Features.*.Infrastructure",
          "Wamage.Shared.Database.AppDbContext"
        ],
        "strict": true
      }
    ]
  }
}
```
## CI / GitHub Actions Integration

### 1. PR Quality Ratchet (`.github/workflows/pr-quality-ratchet.yml`)

Compara o PR contra a baseline da branch `main`:

```yaml
name: PR Quality Ratchet

on:
  pull_request:
    branches: [ main, master ]

jobs:
  quality-ratchet:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Extrair Baseline da Main
        run: |
          mkdir -p /tmp/ratchet
          git show origin/main:.tools/quality-gate/baseline.json > /tmp/ratchet/main-baseline.json 2>/dev/null || true

      - name: Run Quality Gate with Ratchet
        run: |
          dotnet run --project .tools/quality-gate/src/QualityGate -- \
            check --diff --ratchet --baseline /tmp/ratchet/main-baseline.json --format both
```

### 2. Atualização Automática da Baseline (`.github/workflows/update-quality-baseline.yml`)

Grava o novo patamar de métricas após merges na branch `main`:

```yaml
name: Update Quality Baseline

on:
  push:
    branches: [ main ]

jobs:
  update-baseline:
    runs-on: ubuntu-latest
    permissions:
      contents: write
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: |
          dotnet run --project .tools/quality-gate/src/QualityGate -- baseline record --output .tools/quality-gate/baseline.json
          git config user.name "github-actions[bot]"
          git config user.email "github-actions[bot]@users.noreply.github.com"
          git add .tools/quality-gate/baseline.json
          git diff --staged --quiet || (git commit -m "chore(quality): ratchet update baseline metrics [skip ci]" && git push)
```

### 3. Pipeline de CI e Release de Executáveis CLI (`.github/workflows/release.yml`)

O projeto conta com pipelines automatizados para testes contínuos e geração de binários executáveis standalone (single-file self-contained):

#### Matriz de Plataformas Suportadas:
- **Linux x64**: `qualitygate-v<version>-linux-x64.tar.gz` (ELF)
- **Linux ARM64**: `qualitygate-v<version>-linux-arm64.tar.gz` (ELF aarch64)
- **Windows x64**: `qualitygate-v<version>-win-x64.zip` (`qualitygate.exe`)
- **macOS x64**: `qualitygate-v<version>-osx-x64.tar.gz` (Intel)
- **macOS ARM64**: `qualitygate-v<version>-osx-arm64.tar.gz` (Apple Silicon)

#### Controle de Versão e Publicação de Release:

1. **Por Git Tag (Recomendado)**:
   ```sh
   git tag v1.0.0
   git push origin v1.0.0
   ```
   O workflow `.github/workflows/release.yml` é acionado automaticamente, valida todos os testes, compila os executáveis com a versão embutida, gera os checksums SHA-256 e cria a **GitHub Release** com as notas de versão.

2. **Manual (GitHub Actions UI)**:
   Acesse a aba **Actions** > **Release** > **Run workflow** e informe a versão desejada (ex: `1.0.0` ou `1.0.0-rc.1`).

#### Download e Uso do Executável da Release:

Após o término do pipeline, os binários estarão disponíveis para download na página de **Releases** do repositório no GitHub:

```sh
# Exemplo no Linux:
tar -xzvf qualitygate-v1.0.0-linux-x64.tar.gz
./qualitygate version
./qualitygate check --diff

# Exemplo no Windows (PowerShell):
Expand-Archive qualitygate-v1.0.0-win-x64.zip
.\qualitygate.exe version
.\qualitygate.exe check --diff
```
