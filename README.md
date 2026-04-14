# TelemachusTelemetryExtended

Custom Telemachus plugin that adds contracts telemetry and `[x] Science!` telemetry.

## Scene support

- Telemachus plugin variables are evaluable in **Flight** scene.
- Querying these keys in VAB/SPH/tracking scenes will return Telemachus "not evaluable outside flight" errors.

## Exported API keys

### Extended contracts

- `extended.contracts.acceptedCount`  
  Number of accepted contracts.
- `extended.contracts.accepted`  
  Full accepted-contract list as a JSON array, or a single item when called as `extended.contracts.accepted[index]` (returned as a JSON string value by Telemachus datalink).
- `extended.contracts.byState`  
  Full current-contract listing grouped by state, with stable keys:
  `generated`, `offered`, `offerExpired`, `declined`, `cancelled`, `active`, `completed`, `deadlineExpired`, `failed`, `withdrawn`, `other` (returned as a JSON string value by Telemachus datalink).

### Extended [x] Science integration

- `extended.science.current`  
  Current filtered science list (based on `[x] Science!` checklist filter/window state when available, returned as a JSON string value by Telemachus datalink).
- `extended.science.global`  
  Full global science list from `[x] Science!` (`AllScienceInstances`, returned as a JSON string value by Telemachus datalink).
- `extended.science.summary`  
  Quick counts/progress for current/global lists (returned as a JSON string value by Telemachus datalink).

Each accepted contract entry includes:
- Contract identity and labels (`id`, `guid`, `title`, `synopsys`, `notes`)
- Status and metadata (`state`, `localizedState`, `prestige`, `dateAccepted`, `dateDeadline`)
- Parameter list (`parameters[]`) with objective status (`id`, `title`, `notes`, `state`, `optional`)

## Example queries

```
http://127.0.0.1:8085/telemachus/datalink?contracts=extended.contracts.accepted
http://127.0.0.1:8085/telemachus/datalink?c0=extended.contracts.accepted[0]
http://127.0.0.1:8085/telemachus/datalink?byState=extended.contracts.byState
http://127.0.0.1:8085/telemachus/datalink?sCurrent=extended.science.current
http://127.0.0.1:8085/telemachus/datalink?sGlobal=extended.science.global
http://127.0.0.1:8085/telemachus/datalink?sSummary=extended.science.summary
```

## Response structure (important)

Telemachus `/telemachus/datalink` always returns an object with your alias on the left:

```json
{ "alias": "<value>" }
```

For most plugin keys here, `<value>` is a **JSON-encoded string**, so consumers should parse twice:

1. parse outer datalink response
2. parse alias value as JSON

### zsh + jq (double parse)

```zsh
curl -s "http://127.0.0.1:8085/telemachus/datalink?sCurrent=extended.science.current" \
| jq -r '.sCurrent | fromjson' \
| jq
```

### Concrete response shapes

- `extended.contracts.accepted`:
  - inner array (or single object for indexed access) with:
    - `id`, `guid`, `title`, `synopsys`, `notes`
    - `state`, `localizedState`, `prestige`
    - `dateAccepted`, `dateDeadline`
    - `parameters[]` (`id`, `title`, `notes`, `state`, `optional`)
- `extended.contracts.byState`:
  - inner object with fixed arrays:
    - `generated`, `offered`, `offerExpired`, `declined`, `cancelled`, `active`, `completed`, `deadlineExpired`, `failed`, `withdrawn`, `other`
- `extended.science.current` and `extended.science.global`:
  - inner object:
    - `available` (bool)
    - `items[]` with `id`, `description`, `shortDescription`, `experimentSituation`, `situationDescription`, `body`, `biome`, `subBiome`, `completedScience`, `totalScience`, `progress`, `isComplete`, `isUnlocked`, `isCollected`, `onboardScience`, `rerunnable`
- `extended.science.summary`:
  - inner object:
    - `available`, `globalCount`, `globalComplete`, `currentCount`, `currentComplete`, `globalProgress`, `currentProgress`

## Swagger / OpenAPI

Complete OpenAPI spec is available at:

- `PluginData\TelemachusTelemetryExtended\openapi.yaml`

The spec documents **decoded logical endpoints** (`/extended/...`) and includes per-endpoint mapping metadata to raw Telemachus datalink keys (`x-telemachus-datalink`).

Since datalink is alias-based, model raw transport as:

- `GET /telemachus/datalink`
- query params are free-form aliases (`<alias>=<telemachus_key>`)
- response is `object` with arbitrary properties (`additionalProperties`)

For each alias key you standardize (for example `sCurrent`), define the property as:
- type: `string` (outer datalink response)
- description: "JSON-encoded payload for `extended.science.current`"

Then define the decoded payload schema separately (`XScienceCurrentPayload`, `ContractsByStatePayload`, etc.) and document that clients must `JSON.parse` the alias value.

## Live tested samples

During documentation update, these endpoints were queried successfully in a live game session:

- `extended.contracts.acceptedCount`
- `extended.contracts.accepted`
- `extended.contracts.accepted[0]`
- `extended.contracts.byState`
- `extended.science.current`
- `extended.science.global`
- `extended.science.summary`

## Build

Quick build (from repo root):

```
.\build.ps1
```

or (double-click friendly):

```
build.cmd
```

This writes:

- `Source\bin\TelemachusTelemetryExtended.dll` (always)
- `GameData\TelemachusTelemetryExtended\Plugins\TelemachusTelemetryExtended.dll` (unless `-NoDeploy`)

KSP root lookup order:
1. `-KspRoot` argument
2. `KSP_ROOT` environment variable
3. Auto-detect from current repo location
4. Fallback: `D:\Games\steamapps\common\Kerbal Space Program`

Examples:

```
.\build.ps1 -NoDeploy
.\build.ps1 -KspRoot "D:\Games\steamapps\common\Kerbal Space Program"
```

Advanced (direct source script):

Run:

```
PluginData\TelemachusTelemetryExtended\Source\build-telemetry-extended.ps1
```

Optional parameters:

```
PluginData\TelemachusTelemetryExtended\Source\build-telemetry-extended.ps1 -KspRoot "D:\Games\steamapps\common\Kerbal Space Program"
PluginData\TelemachusTelemetryExtended\Source\build-telemetry-extended.ps1 -NoDeploy
```

## Release packaging

To build and prepare a distributable mod folder under `release\TelemachusTelemetryExtended`:

```
PluginData\TelemachusTelemetryExtended\Source\package-release.ps1
```

Output structure:

```
release\TelemachusTelemetryExtended\
  GameData\TelemachusTelemetryExtended\Plugins\TelemachusTelemetryExtended.dll
  README.md
  openapi.yaml
```

## Git-ready notes

- Recommended repository root: `PluginData\TelemachusTelemetryExtended`
- Build artifacts are ignored via `.gitignore` (`release\`, editor files).  
  Only `Source\bin\TelemachusTelemetryExtended.dll` is intentionally tracked for release packaging.

## GitHub release automation

This repo includes a GitHub Actions workflow:

- `.github/workflows/release.yml`

What it does:

- Trigger on tag push `v*` (for example `v1.0.0`) or manual run
- Package a CKAN/KSP-style zip with:
  - `GameData\TelemachusTelemetryExtended\Plugins\TelemachusTelemetryExtended.dll`
  - `README.md`
  - `openapi.yaml`
- Upload `.zip` + `.zip.sha256` as workflow artifacts
- On tagged runs, also attach them to the GitHub Release

Important:

- The workflow packages the **committed** `Source/bin/TelemachusTelemetryExtended.dll`.
- Before tagging, rebuild locally and commit the updated DLL.

Tag release example:

```
git add .
git commit -m "Release v1.0.0"
git tag v1.0.0
git push origin main --tags
```

If KSP is running and the output DLL is locked, the script writes a staging build to:

`PluginData\TelemachusTelemetryExtended\Source\bin\TelemachusTelemetryExtended.dll`
