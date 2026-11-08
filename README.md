# DiGi.GIS.YOLO.UI

DiGi.GIS.YOLO.UI provides the headless prediction runner and WebAPI-backed pipeline orchestrator for Year Built prediction using YOLO and ML.NET.

## The Year Built prediction pipeline

Part of the master plan in [DiGi.YOLO#1](https://github.com/DiGi-Organization/DiGi.YOLO/issues/1). One unattended run, sourced entirely from `api.digiproject.uk`:

```
building_2d + orto_datas -> YOLO detections -> building_data
                         -> ML feature table -> predicted year
                         -> year_built_data + building_data."Predicted year built"
```

`Modify.RunYearBuiltPredictionsAsync` is the orchestrator; `DiGi.GIS.YOLO.UI.ConsoleApp` is the headless run surface. `DiGi.GIS.PostgreSQL.UI` will host the same orchestrator as a tray task.

### Why the pipeline lives here

The orchestrator was first built in `DiGi.GIS.PostgreSQL.UI` and moved here on 2026-09-02.

- **Not in `DiGi.GIS.PostgreSQL.UI`**, because a console runner referencing it inherits `UseWPF`, `UseWindowsForms` and the ASP.NET shared framework for a process that draws nothing.
- **Not in `DiGi.GIS.YOLO`**, which is kept dependency-light for callers that only want the detection translation; the orchestrator needs `DiGi.GIS.WebAPI`, `DiGi.GIS.PostgreSQL` and `System.Drawing`.
- **`.UI` here means "run surface", not WPF.** Everywhere else in the workspace the suffix means a desktop application; this repository holds a library and a console executable, and no window.

### Why `net10.0-windows7.0`

`Modify.SavePredictionImage` - the one encoder the inference export and the training dataset builder share - decodes and re-encodes the stored orthophoto bytes through `System.Drawing.Image`, which is Windows-only from .NET 7 onward. That fixes the target framework, and `NoWarn;CA1416` plus `[SupportedOSPlatform("windows")]` follow from it. The runner cannot be hosted on Linux until that re-encode moves off `System.Drawing` — and it cannot simply be replaced, because the detector's weights are frozen and the export has to stay byte-identical to the one they were trained against.

### Configuration

Three files, all resolved by `Query.ConfigurationFilePath` against the application output. `CopyUserFiles` runs after `CopyFiles`, so anything in the git-ignored `user files` folder overwrites the committed default of the same name.

| File | Committed | Purpose |
|---|---|---|
| `files/GIS_WebAPI_Client.conf` | yes, with `Key=""` | The deny-by-default placeholder. The real key goes in `user files/GIS_WebAPI_Client.conf` and is never committed. |
| `files/YearBuiltPredictionPipelineOptions.json` | yes | The option set, with every write step off and no county named. Copy it to `user files/YearBuiltPredictionPipelineOptions.json` and edit that. |
| `files/YOLOTrainingDatasetOptions.json` | yes | The training dataset option set (`--dataset`, `--check-labels`, `--evaluate-detector`), with no county, no output directory and `CountOnly` on. Copy it to `user files/YOLOTrainingDatasetOptions.json` and edit that. |

**`CountyIds` are county row identifiers, never county codes.** A code is the four character territorial value (`"2212"`); an identifier is a database row running into six figures, and a county whose territory is in several pieces has **one identifier per piece** — name every one of them, so each written row is filed under the part its reference belongs to. Code `2212` (słupski) is identifiers `73482` and `73485`. Getting this wrong used to produce a green run that exported nothing, detected nothing and scored nothing; `Query.UnknownCountyIds` now stops the run and says which identifiers were meant.

Every write step is off in the template on purpose: the pipeline writes deployed data, so a first pass over a county is a read-only one.

### Exit codes

| Code | Meaning |
|---|---|
| 0 | The run completed and no step reported a failure. |
| 1 | The options file could not be read, names no county or no scratch directory, narrows the model's feature contract, names a `ReferencesFilePath` that does not read as a manifest, or sets `ChunkSize` below 1. |
| 2 | The environment preflight failed — this machine cannot run the detector, or the year built model is missing. |
| 3 | No Web API authorization key was found. |
| 4 | A step failed while running. |
| 5 | The run was cancelled. |

Each run logs the **SHA-256 of the detector weights** with its start line and adds it to `YearBuiltPredictionResult.Messages` (`Model SHA-256: …`). Stored detections carry only the run stamp, so this line is what ties a run to the detector that wrote it once `model.pt` has been replaced in place. Weights that exist but cannot be read to hash them are refused (exit code 2).

### Bounded image folders — `ChunkSize`

A county is never exported into one folder. Its buildings are sorted ordinally and split into chunks of at most `ChunkSize` buildings (default 5000, about 55 000 images). Each chunk is exported, detected and recorded on its own ([#30](https://github.com/DiGi-Organization/DiGi.GIS.YOLO.UI/issues/30)):

```
<ScratchDirectory>/<countyId>/
├── chunks/000000/       the chunk's {reference}_{year}.jpeg, removed once its marker is written
├── chunks/000000.json   marker: the chunk's buildings, the ones it had imagery for, the weights' SHA-256, Confidence
├── chunks/000000.bbrf   the detector's results for the chunk
└── results.bbrf         the chunk results joined in order, written once every chunk is scored
```

- **Why.** One flat folder of 1.4 M files collapsed the export of m. st. Warszawa (55417) from about 200 to 6.5–8 images/s, on a machine generating NTFS 8.3 short names. Turning that off (`fsutil 8dot3name set C: 1`) restored 204 images/s, but it is an administrator setting kept per machine, and it was back on a few days later. A bounded folder does not depend on it. It also bounds the disk: at most one chunk of imagery exists at a time when `CleanScratchDirectory` is on.
- **Resume.** With `Resume` on, a chunk whose marker names the same buildings, the same weights (SHA-256) and the same `Confidence` is neither exported nor detected again. A chunk without a matching marker keeps the imagery of its own buildings, so an interrupted export continues, and drops imagery of buildings that moved to another chunk. That happens when `ChunkSize` or the manifest changed. The weights are part of the match because `model.pt` is replaced in place.
- **Score-only runs are unchanged.** They read `results.bbrf` and take the buildings the detector was handed from the markers, since the chunk folders are gone by then. A detection run deletes `results.bbrf` before its first chunk and joins it only after the last, so a run that stopped part way leaves no results to score.
- **A county folder from before #30** (a flat `images/` folder and no `chunks/`) is still read as written when `ExportImages` is off.
- The detector starts once per chunk: about 31 starts for Warszawa at the default.

### Writing detections with new weights

The detection write (`UpdateDetections`) **replaces** each scored building's detection columns across `Years` (default 2008..2025). Every building whose imagery the detector was handed is written. A building it did not fire on is written with those columns set to NULL. Without this, a building the new weights never fire on would keep the previous detector's values, false positives included, beside the new detector's values for its neighbours ([#21](https://github.com/DiGi-Organization/DiGi.GIS.YOLO.UI/issues/21)). Scoring still covers only the buildings the detector fired on.

### Narrowing a run to a dataset's buildings — `ReferencesFilePath`

`ReferencesFilePath` names a `dataset_references.tsv` manifest written by `--dataset`. Every step of the run is then restricted to the manifest's buildings within the named `CountyIds`. This is the detection rewrite a regressor retrain needs ([#22](https://github.com/DiGi-Organization/DiGi.GIS.YOLO.UI/issues/22)): 25 008 labelled buildings take about an hour, where their 217 counties would take weeks.

A manifest that is missing, has no header or names no building refuses the run before any request is sent (exit code 1, step `ReferencesFilePath`). The tray application never sets this option.

Detection-only rewrite of the labelled buildings with candidate weights. `Score` and the two year-built writes are off, so no prediction is stored while the regressor still expects the old detector:

```json
{
  "CountyIds": [ "...the 217 county identifiers of the dataset..." ],
  "ScratchDirectory": "scratch",
  "ModelPath": "user files/YOLO/models/model_train9_fresh.pt",
  "ReferencesFilePath": "D:/YOLO/train9_dataset/dataset_references.tsv",
  "ExportImages": true,
  "RunPrediction": true,
  "Score": false,
  "UpdateDetections": true,
  "UpdateYearBuiltData": false,
  "UpdatePredictedYearBuilt": false
}
```

## The YOLO training dataset

Part of [#12](https://github.com/DiGi-Organization/DiGi.GIS.YOLO.UI/issues/12), built in [#13](https://github.com/DiGi-Organization/DiGi.GIS.YOLO.UI/issues/13). The `train8` dataset is lost, so the detector's training set is **rebuilt from the deployed database** through `api.digiproject.uk`. Three console modes share one options file, `YOLOTrainingDatasetOptions.json`:

| Mode | Entry point | What it does |
|---|---|---|
| `--dataset [options.json]` | `Modify.AppendYOLOTrainingDatasetAsync` | With `CountOnly` it counts: labelled buildings, the split, the Legacy agreement table, bounded entries, cross-part duplicates and a size estimate. It requests no orthophoto and writes nothing. Without `CountOnly` it builds a fresh dataset into `OutputDirectory`, and the build can be resumed. |
| `--check-labels [options.json]` | `Modify.CheckYOLOTrainingDatasetLabels` | Runs `ModelPath` (the current detector) over a seeded sample of positive Train/Validate images. Reports the IoU of each label box with the best detection (mean, median, share ≥ 0.5, per county) and writes overlays to `ReportsDirectory/label_check`. |
| `--evaluate-detector [options.json]` | `Modify.EvaluateYOLODetectors` | Runs every file in `WeightsPaths` over the same Test images. Scores each first detection year against its label (MAE, RMSE, exact share) for all Test buildings and for the **clean** subset (Test ∧ ¬Legacy). Prints one row per weights file per subset. Nothing is written to the API. |

Run them in this order: `--dataset` with `CountOnly` (post the output on #12), then `--dataset`, then `--check-labels` (stop on a low IoU), then train, then `--evaluate-detector`.

**Precondition:** the Year Built building-data update has run since the last user edits. The labels are read from the `User year built` column that update writes.

### Data sources

| Data | Table | Read with |
|---|---|---|
| Label | `building_data` column `User year built` | `Query.UserYearBuiltsAsync`: paged by reference, then `DiGi.GIS.IO.Query.YearBuiltLabels` (most frequent exact user year, the regressor's rule). A page that fails fails the whole county part. |
| History, Test buildings only | `year_built_data` | `Query.StoredYearBuiltDatasAsync` with `fallbackbyreference=true`. It returns every row per reference and the references of pages that failed. |
| Footprint | `building_2d` | `Query.Building2DsAsync`: sent with the county first, then misses are retried without it (server fallback by reference). |
| Orthophotos | `orto_datas` | `Query.OrtoDatasAsync`: same fallback as the footprints. |
| Cross-part duplicates | `building_2d` | `Query.ReferenceDuplicatesAsync`. The endpoint is global, so `CountOnly` filters the rows to the named parts. |

### Labelling rule

- **Eligible:** only references with a label. A reference found under several named county parts is built once, under the lowest identifier. Conflicting labels are counted.
- **Split:**
  - A reference that `DiGi.GIS.IO.Query.Holdout(reference, HoldoutDenominator)` holds out goes to **Test only**. These are the same buildings the regressor holds out.
  - The remaining references are sorted ordinally. A seeded `Random(Seed)` then puts a `ValidateWeight` share of them in Validate and the rest in Train.
- **Images:** one per orthophoto year, the first of each year in date order. Each is saved by `Modify.SavePredictionImage`, the encoder the inference export uses, as `{reference}_{year}.jpeg` with an invariant-culture year.
- **Positive** (`year >= label`): one box of class `Building` (index 0). The box is the footprint's bounding box (a clone) grown by `Offset` metres, projected with `OrtoData.ToOrto`, and **clamped to the image** (`Query.PixelBoundingBox`). It is normalised by the saved image's **pixel** size. If the box has no area inside the image, the image is not written.
- **Negative** (`year < label`): the image is registered with an empty label file.
- **Duplicates:**
  - Identical photo bytes under two years with **conflicting** labels are both dropped.
  - Identical bytes with **agreeing** labels are kept once, at the earliest year.
  - Everything dropped or merged is counted.

### The `Legacy` rule

`train8` saw an unknown set of buildings. A Test building is **Legacy** when either of two records places it there (`Query.LegacySource`):

- the list in `LegacyReferencesFilePath` names it. This is `DiGi.GIS.ML/Data/Data_2025.05.27.tsv`; copy it into `user files/`.
- any user entry of any stored row is undated, or dated before `LegacyCutoff` (`2025-05-23T00:00:00Z`, `train8`'s save date).

The two are combined as a union, so doubt removes a building from the clean subset rather than adding one.

The manifest records the result in `LegacySource`: `tsv`, `timestamp`, `both`, empty (clean), or `unknown`. `unknown` means the county part's history could not be read in full; those buildings count as Legacy, never clean, and the step is reported as failed. Train and Validate buildings carry only the list part, because their Legacy flag feeds no metric.

### Output and resume

The dataset's layout:

- `conf.yaml`: an absolute `path:` and `names: 0: Building`.
- `images/{train,val,test}` and `labels/{train,val,test}`.
- `dataset_references.tsv`, the **manifest**, with columns `Reference, CountyId, Category, Label, Legacy, LegacySource`.

The manifest is also the resume journal:

- A building's images and label files are written first, and its row is appended after them.
- A building the manifest names is complete, and a resumed run skips it.
- The files of a building it does not name (left half-written by a stopped run, matched by the reference before the last `_`) are removed and rebuilt.
- A building with no footprint or no imagery is not journaled, so a later run looks for it again.

The build refuses:

- a non-empty `OutputDirectory` that is not a dataset it started;
- an existing dataset when `Resume` is off.

### Options — `YOLOTrainingDatasetOptions`

| Member | Default | Used by |
|---|---|---|
| `CountyIds` | none — required | `--dataset` |
| `OutputDirectory` | none — required, absolute | all |
| `CountOnly` | `false` (the committed template: `true`) | `--dataset` |
| `Resume` | `true` | `--dataset` |
| `LegacyReferencesFilePath` | `user files/Data_2025.05.27.tsv` | `--dataset` |
| `LegacyCutoff` | `2025-05-23T00:00:00Z` | `--dataset` |
| `Offset` | `1` (metres) | `--dataset` |
| `ValidateWeight` | `0.1` | `--dataset` |
| `HoldoutDenominator` | `5` (the regressor's) | `--dataset` |
| `Seed` | `0` | `--dataset`, `--check-labels` |
| `MaxConcurrentRequests` | `8` | `--dataset` |
| `ReferenceBatchSize` | `10000` (clamped to the endpoint cap) | `--dataset` |
| `ReferenceDuplicateLimit` | `100000` | `--dataset` with `CountOnly` |
| `PythonPath`, `WorkingDirectory` | `null` | `--check-labels`, `--evaluate-detector` |
| `ModelPath` | `user files/YOLO/models/model.pt` | `--check-labels` |
| `Confidence` | `0.1` (production) | `--check-labels`, `--evaluate-detector` |
| `LabelCheckSampleSize` / `LabelCheckOverlayCount` | `500` / `20` | `--check-labels` |
| `ReportsDirectory` | `user files/reports` | `--check-labels` |
| `WeightsPaths` | `null` | `--evaluate-detector` |
| `Years` | `null` (2008–2025) | `--evaluate-detector` |

### Exit codes of the dataset modes

The prediction run's codes, with these meanings:

| Code | `--dataset` | `--check-labels`, `--evaluate-detector` |
|---|---|---|
| 0 | completed, no failed step | completed, no failed step |
| 1 | the options cannot be read, have no absolute `OutputDirectory` or no county, the scope is unknown, the legacy list cannot be read, or the output folder is refused | the options or the dataset (manifest, `conf.yaml`, images) cannot be read, or no weights are named |
| 2 | — | the weights or the interpreter are missing |
| 3 | no Web API key | — (these modes read nothing from the API) |
| 4 | a step failed while running (a county's labels or history, footprints, the final write) | the detector failed |
| 5 | cancelled; the manifest lets a re-run continue | cancelled |

### Disk and time

`CountOnly` estimates:

- **Requests:** one orthophoto read per building, plus the paged footprint reads.
- **Images:** 8 per building.
- **Disk:** about 16 kB per image.

These are rough figures from one sample building, meant to give the order of magnitude. A county of 30 000 labelled buildings is therefore about 30 000 orthophoto requests, 240 000 images and 4 GB. At `MaxConcurrentRequests = 8`, requests are the bottleneck, so measure the first county before scheduling the rest.

## Retraining the detector — `--train`

`--train [path-to-YOLOTrainingRunOptions.json]` runs the retraining as one run, stopping at the first failed step. The steps always run in this order, and `Steps` narrows them (null runs all):

1. `Dataset` — `--dataset`: builds the training dataset, or appends to the folder that already exists (resumable).
2. `LabelCheck` — `--check-labels`.
3. `Train` — `DiGi.YOLO` `Modify.Train` from `StartWeightsPath`, a `.pt` checkpoint (an earlier detector) or the base `yolo26x.pt`. With `ResumeTraining` it continues the run's own `weights\last.pt` instead.
4. `Validate` — `Modify.Validate` on the Test split. Without `Train` it measures the start weights, the baseline a candidate is compared with.
5. `Evaluate` — `--evaluate-detector` over `DatasetOptions.WeightsPaths` plus the trained weights.

The trained weights are copied to `<ProjectDirectory>\<RunName>\<RunName>.pt`, a new file that is never overwritten and never named `model`. The start weights and the copy are printed and logged with their **SHA-256**, and the copy is re-hashed and compared with the digest the training reported, so every row of the comparison table names exactly which file it measures. Every path is made absolute, and everything that can be known without starting a process — a missing start file, an unusable interpreter, a run name that is taken, a `ProjectDirectory` inside a `YOLO\models` folder, an empty Validate split — is refused before the first step, with the option it concerns as the step name.

The `Train` step needs a non-empty Validate split, because ultralytics trains against it: a small dataset has none, since the non-Test buildings are drawn one by one against `ValidateWeight`, and with the defaults (`Seed 0`, `ValidateWeight 0.1`) the first Validate building is the 16th non-Test one. A run that trains on the dataset as it stands is checked in the preflight (before the interpreter is probed); a run that still builds the dataset is checked once it is final, after the `Dataset` step. A `ResumeTraining` run is never checked — its checkpoint was trained on a dataset that already held a Validate split. The refusal names the split and the remedy, and the `Dataset` step reports its Train / Validate / Test building counts, warning when Validate is 0.

With no working directory named, the interpreter preflight runs in its own scratch folder (`%TEMP%\DiGi_YOLO_Preflight`) and never writes into the dataset directory, so a new dataset directory stays empty for the `Dataset` step to build. The training itself still uses the dataset directory — the folder holding `conf.yaml` — once the dataset exists.

Progress lines are the same `[PROGRESS]` / `[INFO]` lines as the other modes. The nested `DatasetOptions` is a plain object, exactly as in a `YOLOTrainingDatasetOptions` file.

### Options — `YOLOTrainingRunOptions`

| Member | Default |
|---|---|
| `DatasetOptions` | none — required, with an absolute `OutputDirectory`; `PythonPath` and `WorkingDirectory` of the run take precedence over its own |
| `StartWeightsPath` | none — required for `Train` and `Validate`; ignored when `ResumeTraining` is set |
| `ResumeTraining` | `false` — continue the interrupted run named by `ProjectDirectory` + `RunName` instead of starting a new one (see below) |
| `RunName`, `ProjectDirectory` | none — required for `Train` (the folder is absolute) |
| `Epochs` / `Patience` / `ImageSize` / `Batch` / `Seed` | `150` / `50` / `640` / `16` / `0` (the `DiGi.YOLO` README); ignored when `ResumeTraining` is set |
| `AutoResumeCount` | `0` — the runner's own retries after a stall or a crash; the tray sets `3` (see below) |
| `InactivityTimeout` | `null` — the `DiGi.YOLO` default, 15 minutes; a `TimeSpan` such as `00:15:00` overrides it, and `0` disables the limit |
| `Device`, `PythonPath`, `WorkingDirectory` | `null` |
| `Steps` | `null` — all; the committed template lists only `Dataset`, and counts |

### Recovering an interrupted run — `ResumeTraining`

Set `ResumeTraining` to `true` to continue a run whose training was stopped instead of starting a new one. The run keeps its name: it is identified by `ProjectDirectory` + `RunName` and continues `<ProjectDirectory>\<RunName>\weights\last.pt` at the next epoch, in the same folder. Nothing is copied or renamed by hand, and after the training the runner copies, re-checks, validates and evaluates exactly as a fresh run does.

- `StartWeightsPath`, `Epochs`, `Patience`, `ImageSize`, `Batch` and `Seed` are **ignored** and logged as ignored — the checkpoint restores them, and the epoch ceiling is fixed by the checkpoint. A different ceiling is a new run, not a resume. `Device`, `PythonPath`, `WorkingDirectory`, `DatasetOptions` and `Steps` still apply.
- `Steps` must name `Train` and must **not** name `Dataset`: a resume never rebuilds the dataset the checkpoint was trained on. `null` (all steps) includes `Dataset`, so list the steps explicitly, for example `["Train", "Validate", "Evaluate"]`.
- A resumed run is **not bit-identical** to an uninterrupted one (the dataloader RNG restarts). The log and the result say `Resuming … from epoch N of M`, and the final table's `Start` row reads `Start (resume of epoch N)`.

The run is refused before the training starts, with the reason named by its option (exit code `1`), when:

| Check | Refusal |
|---|---|
| `Train` step not selected | `ResumeTraining` without training |
| `Dataset` step selected | a resume never rebuilds the dataset it was trained on |
| `<ProjectDirectory>\<RunName>` missing, or no `weights\last.pt` | nothing to resume |
| `<ProjectDirectory>\<RunName>\<RunName>.pt` exists | the run completed; choose a new run name |
| the checkpoint cannot be read, is finished, or records a dataset that is gone | the checkpoint is not resumable |
| the checkpoint records a different project or name | the folder was moved or renamed; ultralytics would write elsewhere |

### Stalls and automatic resume — `AutoResumeCount`

A training can stop producing output while staying alive — a data-loader deadlock holds the GPU with every process at 0 % CPU — or its process can die with a driver fault. `DiGi.YOLO` ends a run whose output stays silent for `InactivityTimeout` (15 minutes by default) and reports it as stalled rather than cancelled. `AutoResumeCount` makes the runner continue such a run by itself, up to that many times, without an operator.

- Only a **stall** or a **crash** (a non-zero exit code from a process that started) is resumed. A refusal raised before a process started, a finished checkpoint and a stop requested with Ctrl+C are never resumed.
- Each resume first copies `weights\last.pt` to `weights\last_autoresume<N>_<yyyyMMdd_HHmmss>.pt`, then continues from `last.pt` with the epoch ceiling the checkpoint holds. The copy keeps the previous checkpoint recoverable if the process is killed while saving.
- The log says `Training stalled at epoch E (last output hh:mm:ss) - automatic resume N of M`, or `Training exited with code X at epoch E - automatic resume N of M`, preceded by the last 30 lines of the crashed attempt's error output (`  | …`, its traceback), or by `The attempt that exited with code X wrote no error output - …` when a process ended from outside wrote none. The attempt that ends the run is logged the same way, ending `- no automatic resume left (M of M used)` (or `- automatic resume is off (AutoResumeCount 0)`), and the closing `Resumed automatically N time(s): Stalled at epoch E (hh:mm:ss), …` names the same interrupted epochs; the final table names each resume, and the result records them in `AutoResumes`. The tray reads those lines for its live status.
- The default is `0` — no retries — because a library must not retry unasked; the tray offers `3` for an unattended run. `InactivityTimeout` is a `TimeSpan` such as `00:15:00`; leave it unset for the `DiGi.YOLO` default, and note that `0` disables the limit rather than meaning "immediately".
- A run still dies with the tray or a power cut: automatic resume shortens a stall or a crash, it does not make a run survive. That recovery is `ResumeTraining` above.

### Exit codes of `--train`

The prediction run's codes, plus two:

| Code | Meaning |
|---|---|
| 0 | every step that was asked for completed |
| 1 | an option cannot be used (options, dataset, start weights, run name, project folder, resume) or the dataset step refused its scope |
| 2 | the interpreter or weights are missing, or the interpreter cannot run the training |
| 3 | no Web API key (only when `Dataset` runs) |
| 4 | the dataset build, the label check or the evaluation failed while running |
| 5 | cancelled; earlier outputs are left as they are and the dataset manifest lets a re-run continue |
| 6 | the training did not produce weights |
| 7 | the trained weights could not be validated on the Test split |

`Query.YOLOTrainingRunExitCode` maps a `YOLOTrainingRunResult` to these codes; failed steps are listed as `YOLOTrainingStep.<Step>` so a dataset split named `Train` is never read as a failed training.

## 💻 Coding Guidelines for Developers & AI Agents

All developers and AI agents follow these always-applicable rules (Visual Studio 2026, C#). Task-specific detail lives in
the guidelines listed in §9 — read the one matching the task.

### 1. General coding standards
1. **English only** — identifiers and comments.
2. **No `var`** unless the compiler forces it; target-typed `new()` (`PointNode pointNode = new();`, avoids IDE0090) and
   collection expressions (`List<int> numbers = [];`, avoids IDE0028).
3. **Naming:** camelCase starting with the type name, `_`-suffixed qualifier when needed (`PointNode pointNode_Base`); an
   acronym type lowercases only its first letter (`gISPostgreSQLConverterManager`). Collections: pluralized element type
   (`filterConditions`, `point3Ds` — not `listConditions`). A property named after its unique, descriptive type
   (`public AggregateFunction AggregateFunction { get; set; }`). Primitives: plain camelCase.
4. **Zero warnings/analyzer messages.**
5. **Block-scoped namespaces** (`namespace DiGi.Domain { ... }`); file-scoped are prohibited (`.editorconfig`). Every
   repository builds at `LangVersion latest` with `AnalysisLevel latest-recommended` (template props); core libraries
   target `netstandard2.0` — use `System.Threading.Lock` only in net9+ projects.
6. **Parameter line breaks (`<= 7` rule, `DIGI0001`):** ≤ 7 parameters on one line however long; ≥ 8 one per line. Line
   length is never the trigger. Call sites prefer one line; multi-line for complex expressions, lambdas, initializers.
   ```csharp
   public void Calculate(double centerX, double centerY, double radius, double? storeyHeight = null)
   ```
7. **Async method names end with `Async`.**
8. **`CancellationToken` is always the LAST parameter (CA1068)** — every method and overload; add new optional parameters
   before it and reorder the `<param>` tags. At call sites **name every argument after a skipped default**
   (`commandTimeout: 30`, `cancellationToken: cancellationToken`) so an inserted parameter fails to compile instead of
   rebinding. Detect: `grep -rnE "CancellationToken [a-zA-Z_]+( = default)?, " --include=*.cs .`
   ```csharp
   public static async Task<bool> ClearAsync(NpgsqlConnection? npgsqlConnection, string tableName, int commandTimeout = 30, CancellationToken cancellationToken = default)
   ```
9. **Simplify member access (IDE0002) — then rebuild.** C# binds the first segment innermost-outwards with no fallback:
   from `DiGi.GIS.PostgreSQL.UI.Classes`, `WebAPI` is `DiGi.GIS.WebAPI`, so `DiGi.WebAPI.Query.GetAsync` keeps its prefix.
   When same-named extension methods can both apply, call the intended one fully qualified — the `using` decides silently.
10. **Separate projects**, not one monolithic solution.
11. **Output optimization:** no filler; only the necessary code, logic or explanation.
12. **Temporary code carries `TODO [MarkerName]:`** — a PascalCase name of the **migration**, reused at every site so one
    `grep` is the deletion checklist, stating the observable **removal condition** and the tracking issue. A workaround
    for another DiGi repository's defect is temporary too (removal condition = the upstream fix). Never tag permanent code.
13. **`ToString()` is not a key** — identity strings render through `CultureInfo.InvariantCulture` (`"R"` with `-0.0`
    folded, `G29` for `decimal`); `pl-PL` files `[0,5, 12,25]` and nothing throws. Reference: `DiGi.Typology.Visual.Query.Key`.
14. **A member resolved by name through reflection is a contract on the whole hierarchy** — document it at both ends,
    probe `GetMethod` rather than reason about it.
15. **No caching in an optimization unless the request explicitly allows it** — remove allocations and repeated
    enumeration first; propose a cache with its measurement and ask.
16. **Log through `Serilog.Modify.Log`** with structured templates (`"{Type}: … {CountyId}"`, never interpolation);
    `Log(exception, …)` always logs at Error.
17. **Rethrow only a requested cancellation:**
    `catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }` before any general catch.

### 2. Architecture (DiGi.Core pattern)
Data models are anemic; behavior is static extension methods. Never a service/manager class for behavior (process-wide
registries are `*Manager`s on `Settings` or DI singletons).
- **`/Classes`** (`[Project].Classes`), **`/Interfaces`**, **`/Enums`**. Category folders (`Classes/Options`,
  `/BackgroundTask`, `/Result`, `Convert/ToX`) add **no** namespace segment — it would change the `_type` discriminator.
- **`Query`** (read-only, property-like names: `BoundingBox()`, only `Is`/`Has`/`Try` prefixes), **`Modify`** (mutates),
  **`Create`** (builds), **`Convert`** (`/Convert/To[TargetArea]/[TargetType].cs`, `ToSystem_String`, `ToEPW_DateTime`) —
  `Convert` is the first choice for any conversion, never an instance method on the model.
- **Interface-contract members are instance methods on the class** — geometry primitives (`Polygon2D`, `Circle2D`, …
  implementing `IBoundable2D`, `ITransformable2D`, `IClosedCurve2D`) are deliberately not anemic; never move them.
- **Constructors stay cheap** (assign and clone only); validation and cleanup go in a `Create` factory named after the
  type, returning `null` for invalid input — materialise → clean up → then validate.
- **One public method per file** in `/Query`, `/Modify`, `/Create` (overloads together); nested types in
  `[Outer].[Inner].cs`; **no private methods** in these classes (public helper or local function).
- **`IResult` types end in `Result`** (`TerrainPointDensityResult`); the name is wire contract, so existing violations are
  not renamed opportunistically.
- Enums start with `Undefined` (`-1` only where `0` is real); tolerances come from `DiGi.Core.Constants.Tolerance`
  (`Distance` 1e-6, `MicroDistance`, `MacroDistance`, `Angle`).

### 3. Project assets — `files/` vs `user files/` (never commit secrets)
Committed, non-sensitive deployment assets go in `files/`; **secrets, credentials, machine-specific settings and
generated reports go in the git-ignored `user files/`** (`[Uu]ser [Ff]iles/` in `.gitignore`; verify with
`git check-ignore -v "user files/x.conf"`). Both are copied flat into the output (`CopyUserFiles` runs after `CopyFiles`,
so `user files/` wins) — and `user files/` **ships** with every deploy, so training data and model variants live outside
the workspace. A file a build copies into a project (`gltf-viewer-core.js`, `gis-theme.css`) is edited only at its source.

### 4. Host dependencies — `HintPath` drops a library's NuGet packages
DiGi projects reference each other by `HintPath`, which NuGet and MSBuild cannot follow: a host must **re-declare every
package its libraries need, at the same version** (unless its framework provides it), or it fails at run time with a
`FileNotFoundException` per item — a **partial result reported as success**. `dotnet test` never rebuilds the library
under test: build it first. Verify with `CheckHostDependencies.ps1`.

### 5. References (`IReference` / `IUniqueReference`)
**Never compare two interface-typed references with `==`** (a silent reference comparison; accessors clone per call).
Use `Core.Query.Equals(reference_1, reference_2)`.

### 6. Serialization (`SerializableObject`)
A serializable class implements `I<Project>SerializableObject`, keeps `private readonly` fields tagged
`[JsonInclude, JsonPropertyName(nameof(Prop))]`, exposes them as `[JsonIgnore]` get-only properties, uses
`DateTimeOffset` for dates, and has exactly three constructors:
```csharp
public Holiday(string? name, string? date)
{
    this.name = name;
    this.date = date;
}

public Holiday(Holiday? holiday) : base(holiday)
{
    if (holiday != null)
    {
        name = holiday.name;
        date = holiday.date;
    }
}

public Holiday(JsonObject? jsonObject) : base(jsonObject) { }
```
Copy constructor: primitive lists `new List<T>(source)`, nested objects `Core.Query.Clone(item)` element by element.

### 7. Documentation and tests
- **XML docs:** every public member, params in signature order, one `<summary>`, no blank lines (`<para>`), no docs on
  `partial` declarations. Read `documentation/API/<Assembly>/` before opening `.cs` source.
- **xUnit:** `[ProjectName].xUnit`, `public partial class Facts` in `/Facts`, a `<summary>` on every fact, shared fixtures
  in `DiGi.Test/files/`, written reports in `DiGi.Test/user files/reports/`. Every serializable class gets a fact ending in
  `Core.xUnit.Query.SerializationCheck(instance)` **plus** a direct copy-constructor assertion. Tolerance facts test just
  inside and just outside; benchmarks are measured in isolation with a warm-up. Slow facts are `[MediumFact]`/`[LongFact]`.
- **Interactive front ends** are verified in a real browser (Playwright), asserting rendered output, not state flags.

### 8. Branches and releases
Work is committed on the active version branch (the highest bare SemVer branch, e.g. `0.8.5`) — no feature branch,
worktree or pull request unless asked; push only when asked. **A version bump / release runs only on an explicit request:**
merge the version branch into `main`, bump the patch (`0.8.2` → `0.8.3`), branch it off `main`, update
`Directory.Build.props` `<Major>`/`<Minor>`/`<Build>`, push both (`git push -u origin <version_branch>`).

### 9. Full guideline set
Canonical, task-specific guidelines live in `DiGi.Maintenance/documentation/AI Guidelines/` (relative to the workspace
root) and are mirrored per repository as skills under `.agents/skills/<skill-name>/SKILL.md`.

| Guideline | Read it when |
|---|---|
| `Coding - General.md` | Writing or editing any C# code. |
| `Coding - Serialization.md` | Creating or changing a serializable type or options class; debugging a JSON round trip. |
| `Coding - Host Dependencies.md` | Adding a NuGet package; a host failing with `FileNotFoundException` or a partial result; `CS0012`/`CS0246` build walls. |
| `Coding - Project Setup.md` | New project or repository, `.editorconfig`, `Directory.Build.props`, analyzers. |
| `Coding - Automatic Tests.md` | Writing xUnit facts. |
| `Coding - Performance.md` | Optimizing, benchmarks, parity tests, the wiki `Benchmark.md` page. |
| `Coding - References.md` | Comparing or keying an `IReference` / `IUniqueReference`. |
| `Coding - Geometry.md` | Orienting, closing, triangulating, hashing or sweeping geometry. |
| `Coding - ComputeSharp.md` | Writing or changing a ComputeSharp `IComputeShader`. |
| `Coding - Grasshopper.md` | Grasshopper/Rhino plug-in components. |
| `Coding - Background Tasks.md` | Tray-application background tasks and their dialogs. |
| `Coding - WPF Deferred Loading.md` | A WPF window needing slow data; WPF layout checks. |
| `Coding - YOLO.md` | The YOLO Year Built detector. |
| `Coding - Laya.md` | The Laya decision model. |
| `Coding - WebAPI GLTF.md` | Web APIs on the `DiGi.GLTF` 3D framework. |
| `Coding - WebAPI Contracts.md` | Changing a WebAPI route/parameter/controller or writing an HTTP client. |
| `Coding - WebAPI Simple Authorization.md` | API-key tiered authorization. |
| `Coding - Deployed WebAPI.md` | Testing against `api.digiproject.uk`; which server runs what. |
| `Coding - Browser Testing.md` | Verifying interactive front-end behaviour in a real browser. |
| `Coding - GIS Administrative Data.md` | Anything keyed by a county code or id; the Subdivision layer. |
| `Coding - PostgreSQL.md` | PostgreSQL/Npgsql code, production vs development databases. |
| `Coding - PostgreSQL Distributed Queue Processing.md` | Distributed claim/acknowledge update queues. |
| `XML Documentation.md` | Reading generated API docs; adding or auditing XML docs. |
| `GitHub - Branches and Commits.md` | Where to commit, the pre-commit sweep, plan files, pulling all repos, releases. |
| `GitHub - Issues.md` | Creating, editing, verifying and closing issues; dependencies. |
| `GitHub - Labels.md` | Labels and the mandatory `ai: *` tier. |
| `GitHub - Sub-Issues.md` | Sub-issues for a multi-repository feature. |
| `GitHub - Workspace Setup.md` | Folder layout and new-machine setup. |
| `GitHub Wiki.md` | Editing wiki pages. |
| `Maintenance - Scripts.md` | `DiGi.Maintenance/Scripts`; scripted edits and line endings. |
| `Maintenance - AI Guidelines Propagation.md` | Adding, splitting or removing a guideline. |

> **One data rule worth knowing before it bites you:** a GIS county `code` does **not** identify one row — 406 county
> rows (one per polygon part) cover 380 codes. Key every write and lookup by county **`id`**, give every
> `LIMIT`/`FirstOrDefault` an `ORDER BY`, never deduplicate those rows, resolve every item's part against `building_2d`
> whatever number of ids arrived, and read building-keyed tables with `fallbackByReference: true`
> (`Coding - GIS Administrative Data.md`).
