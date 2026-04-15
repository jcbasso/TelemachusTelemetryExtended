# TelemachusTelemetryExtended

Custom Telemachus plugin that adds contracts telemetry and `[x] Science!` telemetry.

## Scene support

- Flight-scoped keys are evaluable only in **Flight** scene.
- Global-scoped keys are evaluable outside flight scenes when Telemachus global plugin scope support is present.

## Exported API keys

### Extended contracts

- `extended.contracts.accepted`  
  Full accepted-contract list as a JSON array, or a single item when called as `extended.contracts.accepted[index]` (returned as a direct object/array in Telemachus datalink).
- `extended.contracts.byState`  
  Full current-contract listing grouped by state, with stable keys:
  `generated`, `offered`, `offerExpired`, `declined`, `cancelled`, `active`, `completed`, `deadlineExpired`, `failed`, `withdrawn`, `other` (returned as a direct object in Telemachus datalink).

### Extended [x] Science integration

- `extended.science.current`  
  Current filtered science list (based on `[x] Science!` checklist filter/window state when available, returned as a direct object in Telemachus datalink).
- `extended.science.global`  
  Full global science list from `[x] Science!` (`AllScienceInstances`, returned as a direct object in Telemachus datalink). Registered via global plugin scope.

### Extended plugin integration

- `extended.plugins.registered`  
  Only plugins registered in Telemachus plugin registry (internal + external plugin API registrations).
- `extended.plugins.loaded`  
  Full loaded assembly list from KSP runtime (for debugging).

### Extended vessels integration

- `extended.vessels.orbits`  
  Full orbit dataset for all loaded vessels, including GUIDO-ready orbital elements and body metadata. Registered via global plugin scope.

Flight-scoped keys are:
- `extended.contracts.accepted`
- `extended.contracts.byState`
- `extended.science.current`

Global-scoped keys are:
- `extended.science.global`
- `extended.plugins.registered`
- `extended.plugins.loaded`
- `extended.vessels.orbits`

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
http://127.0.0.1:8085/telemachus/datalink?plugins=extended.plugins.registered
http://127.0.0.1:8085/telemachus/datalink?loaded=extended.plugins.loaded
http://127.0.0.1:8085/telemachus/datalink?vessels=extended.vessels.orbits
```

## Response structure (important)

Telemachus `/telemachus/datalink` always returns an object with your alias on the left:

```json
{ "alias": "<value>" }
```

For these plugin keys, `<value>` is already a structured object/array (no double-parse required).

### zsh + jq

```zsh
curl -s "http://127.0.0.1:8085/telemachus/datalink?sCurrent=extended.science.current" \
| jq '.sCurrent'
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
- `extended.plugins.registered`:
  - inner array with:
    - `typeName`, `assembly`, `assemblyVersion`, `origin` (`internal`/`external`), `commands[]`
- `extended.plugins.loaded`:
  - inner array with:
    - `name`, `version`, `fullName`, `location`
- `extended.vessels.orbits`:
  - inner object:
    - `available`, `ut`
    - `vessels[]` with:
      - `id`, `name`, `type`, `situation`, `isActive`, `isTarget`
      - `body`, `bodyRadius`, `latitude`, `longitude`, `altitude`
      - `orbit` (`sma`, `eccentricity`, `inclination`, `lan`, `argumentOfPeriapsis`, `trueAnomaly`, `meanAnomalyAtEpoch`, `epoch`, `period`, `ApA`, `PeA`)

## Swagger / OpenAPI

Complete OpenAPI spec is available at:

- `openapi.yaml`

The spec documents **decoded logical endpoints** (`/extended/...`) and includes per-endpoint mapping metadata to raw Telemachus datalink keys (`x-telemachus-datalink`).

Since datalink is alias-based, model raw transport as:

- `GET /telemachus/datalink`
- query params are free-form aliases (`<alias>=<telemachus_key>`)
- response is `object` with arbitrary properties (`additionalProperties`)

For each alias key you standardize (for example `sCurrent`), define the property as the direct schema (`object`/`array`) for that endpoint payload.

## Live tested samples

During documentation update, these endpoints were queried successfully in a live game session:

- `extended.contracts.accepted`
- `extended.contracts.accepted[0]`
- `extended.contracts.byState`
- `extended.science.current`
- `extended.science.global`
- `extended.plugins.registered`
- `extended.plugins.loaded`
- `extended.vessels.orbits`

Latest runtime check confirms direct datalink value types:

- `extended.contracts.accepted` -> array
- `extended.contracts.byState` -> object
- `extended.science.current` -> object
- `extended.science.global` -> object
- `extended.plugins.registered` -> array
- `extended.plugins.loaded` -> array
- `extended.vessels.orbits` -> object

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
.\Source\build-telemetry-extended.ps1
```

Optional parameters:

```
.\Source\build-telemetry-extended.ps1 -KspRoot "D:\Games\steamapps\common\Kerbal Space Program"
.\Source\build-telemetry-extended.ps1 -NoDeploy
```

## Release packaging

To build and prepare a distributable mod folder under `release\TelemachusTelemetryExtended`:

```
.\Source\package-release.ps1
```

Output structure:

```
release\TelemachusTelemetryExtended\
  GameData\TelemachusTelemetryExtended\Plugins\TelemachusTelemetryExtended.dll
  README.md
  openapi.yaml
```

## Git-ready notes

- Recommended repository root: `_dev\TelemachusTelemetryExtended`
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

`Source\bin\TelemachusTelemetryExtended.dll`
