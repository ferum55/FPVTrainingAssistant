# FPVTrainingAssistant — implementation plan

The product requirements and boundaries are recorded in [PROJECT_CONTEXT.md](PROJECT_CONTEXT.md).
Each stage should leave a buildable application. Keep changes small, use MVVM,
and avoid adding dependencies before they are needed.

Stages 1 through 6, including Stages 5.5A/5.5B, are delivered: the scaffold, domain models, contracts,
JSON import/validation, deterministic analysis, WPF review UI, top-down trajectory
and telemetry replay, replay-capable contract 1.1, assistant-side simulator handoff,
an interactive simulator-interface mock, synthetic examples, and focused xUnit tests.
SQLite snapshots, operator metadata, history/reopening and native score progress
are also delivered. Real simulator integration, configuration editing and later
stages remain planned.

## Stage 1 - project structure and models

- Establish FPVTrainingAssistant.sln and the .NET 10 WPF project.
- Separate Models, Services, Data, ViewModels, Views, and TestSessions.
- Add the context, plan, and Visual Studio/.NET ignore rules.
- Define the required conceptual classes and
  relationships with nullable reference types and no simulator dependencies.
- Define model ownership and distinguish simulator score from calculated score.

Acceptance: the solution builds; the nine domain/data models cover the conceptual
model without WPF dependencies or business logic in views. SessionAnalyzer is
implemented in Stage 3. Contract decisions are documented in SESSION_CONTRACT.md;
the preliminary sample does not implement import or claim simulator export support.

## Stage 2 - JSON contract/import/validation

- Specify a versioned, simulator-neutral contract with units, axes, timestamps,
  identifiers, event payloads, and required/optional fields.
- Use System.Text.Json to deserialize files behind an import service.
- Validate structure, identifiers, timing, numeric values, exercise compatibility,
  and required telemetry/event data before invoking analysis.
- Define unknown-event and duplicate-session policies; handle malformed input,
  inaccessible files, and incomplete sessions with actionable messages.

Acceptance: valid files yield validated sessions; invalid files return clear
errors without crashes or analysis execution.

Delivered with structural checks before deserialization, accumulated semantic
issues, documented angle/time/event conventions, stable event ordering, and
portable test fixtures. Database duplicate enforcement remains Stage 6; no
analysis or UI is invoked. Verification: `dotnet build FPVTrainingAssistant.sln`
and `dotnet test FPVTrainingAssistant.sln`.

## Stage 3 - analysis engine

- Implement SessionAnalyzer independently of WPF and persistence.
- Add configurable criteria for ObstacleCourse and TargetMission.
- Calculate checkpoint/collision and target detection/hit/damage metrics.
- Define reference-route distance, zone boundaries, and sample/time weighting;
  calculate zone percentages and average/maximum deviation.
- Calculate weighted scores, measured values, explanations, mistakes, and
  recommendations tied to evidence.
- Prioritize correct execution over speed; use raw flight metrics only where
  a criterion explicitly requires them.

Acceptance: known inputs produce understandable, deterministic results for both
exercise types, including disabled criteria and boundary cases.

Delivered with eight RuleTypes, isolated profile validation, interval-midpoint
route metrics weighted by elapsed time, criterion explanations, stable result IDs,
mistake evidence, deduplicated recommendations, and independent default profile
factories. ANALYSIS_RULES.md records the exact formulas and limitations. Tests
retain all Stage 2 coverage and add route, obstacle, target, configuration, and
determinism checks. No WPF UI, persistence, or replay is added.

## Stage 4 - basic WPF UI

- Add view models, commands, and bindings for import and session selection.
- Present validation errors and loading state clearly.
- Show operator, exercise, time/duration, final and partial scores, explanations,
  mistakes, important events, and recommendations.
- Keep analysis and data access outside view code-behind.

Acceptance: an instructor can import a file and understand its result through
the UI; failed imports leave a usable application.

Delivered with a dark WPF shell, import and result DataTemplates, portable MVVM
infrastructure/state, a standard file picker, explicit synthetic profile lookup,
in-app errors/warnings, separate assistant/simulator scores, criterion explanations,
mistakes, recommendations, and chronological events. UI_GUIDE.md documents usage,
profile identities/route, architecture, verification, and limits. All four requested
fixtures were checked in the running WPF application, including recovery after
invalid imports. Stage 4 verification: build has 0 warnings/errors; all 203 tests
pass, retaining the previous 177. Future navigation stays disabled; no Stage 5+
functionality is included.

## Stage 5 - trajectory visualization/replay

- Add a 2D top-down path view with reference route, checkpoints, and zone colors.
- Support chronological navigation and playback based on telemetry time.
- Display reconstructed position and pitch/roll/yaw at the selected time.
- Handle uneven timestamps and practical telemetry sizes without blocking UI.

Acceptance: recorded and reference paths are visible, route zones are distinct,
and navigation follows the telemetry chronologically. Video is not required.

Delivered with portable projection/replay/visual-zone services, TrajectoryViewModel,
a cached native WPF map, real-time clock adapter, timeline/sample stepping,
0.5x/1x/2x playback, shortest-path angle interpolation, events, existing route metrics,
and reset/pause behavior across navigation/imports. Two synthetic demo fixtures
have 21 samples; the obstacle route has turns while existing score expectations
remain unchanged. TRAJECTORY_REPLAY.md distinguishes analytical unbounded Red
from finite visual context. Live checks covered both exercises, controls, resize,
new-session reset, and a temporary 6000-sample path. Stage 5 verification: 0 build
warnings/errors and 245/245 tests, including all 203 previous tests. No Stage 6+
features are added.

## Stage 5.5A - replay-capable contract and simulator handoff (delivered)

- Accept explicit versions 1.0 and 1.1 without changing existing fixture versions.
- Add nullable extended telemetry, neutral ReplayContext and dynamic entity tracks.
- Validate new numeric types/ranges, neutral IDs and per-track times/transforms.
- Retain the normalized source file path only in runtime import/application state.
- Report drone/environment/entity/local-launch capability independently of scoring.
- Read FPVTRAINING_SIMULATOR_EXE; safely hand off the original source file using
  `-ReplayMode` and one `-ReplayFile=<absolute path>` argument without a shell.
- Add Open 3D Replay separately from View trajectory, with recoverable in-app
  messages for missing environment, configuration or source file.
- Add two deterministic synthetic 1.1 fixtures and tests using process doubles.

Delivered and specified in REPLAY_INTEGRATION.md and SESSION_CONTRACT.md. Build:
0 warnings/errors. Tests: 320/320, retaining all 245 prior cases and adding 75.
Obstacle/target scores remain 82.67/90.00; analysis services and local 2D replay
algorithms are unchanged. No simulator modifications, 3D renderer, exporter,
database, network or settings editor are included.

## Stage 5.5B - mock simulator replay prototype (delivered)

The laboratory scope replaces the previously planned real simulator integration
with a local interactive WPF preview. DroneDiploma is not changed or launched.

- Inject MockSimulatorReplayLauncher through ISimulatorReplayLauncher; preserve
  the tested external launcher for future production use.
- Distinguish loaded Contract 1.1 preview capability from external file/executable
  availability; no simulator configuration is required for the mock.
- Open one Simulator Replay Prototype window with real imported metadata/counts,
  technical FPV and Third Person views, a compact collapsible minimap and event log.
- Reuse existing telemetry/playback services and time semantics; extend ReplayState
  with optional sensors. Interpolate entities using the shared angle/position rules.
- Support play/pause, restart, sample steps, seek and 0.5x/1x/2x. Preserve state
  when switching cameras/panels, and detach the clock/subscriptions on close.
- Keep Contract 1.0 analysis and 2D replay available with a clear preview limitation.

Acceptance met: both replay fixtures open; actual telemetry/entities/events follow
the selected time. Build: 0 warnings/errors; 364/364 tests, retaining all 320 previous
cases. Live WPF checks covered cameras, controls/speeds, panels, single-window
activation, safe close and both exercise types. MOCK_SIMULATOR_REPLAY.md records
architecture, entity/state rules, real-versus-mocked data and the demonstration.
No Stage 6 work is included.

## Future production simulator integration (separate repository)

- Parse and independently validate the replay command/file in DroneDiploma.
- Resolve neutral environment/scenario/archetype IDs to local maps and visuals.
- Apply recorded drone/entity transforms and states with linear position and
  shortest-path angle interpolation, disabling normal AI/physics as drivers.
- Define optional sensor/camera defaults, unknown visual fallback and partial-track
  coverage without re-simulating AI.
- Implement FPV camera with body-relative tilt/horizontal FOV, third-person camera,
  show/hide minimap, chronological event log, and play/pause/seek at 0.5x/1x/2x.
- Obtain optional reference routes from compatible simulator profile/scenario data;
  session JSON currently does not embed the assistant's route geometry.
- Verify end-to-end handoff, synchronization, seeks and errors using both new
  synthetic files. Any exporter requires separate authorization and source-unit
  verification; current DroneDiploma is not claimed to export the contract.

Acceptance: the separate simulator reconstructs the recorded scene and views
without gameplay resimulation; normal simulator mode remains separate. This is
not implemented by the laboratory mock. REPLAY_INTEGRATION.md retains the precise
handoff and conceptual source-unit mapping.

## Stage 6 - SQLite persistence/history (delivered)

- Add a suitable SQLite provider and a local database behind Data services.
- Create the nine required tables with foreign keys and clear initialization.
- Save operators, sessions, telemetry, events, profiles, criteria, results, and
  recommendations atomically; apply the duplicate-session policy.
- Retain sufficient profile/evaluation context for reproducible historical results.
- Add operator ID, optional name and notes editing, history, reopening, score
  comparison, and progress views.
- Handle storage failures and protect existing data.

Acceptance: imported results survive restart, operators' histories remain
separate, old sessions reopen, and score progress is visible.

Delivered with Microsoft.Data.Sqlite 10.0.14, a per-user LocalAppData database,
schema_version=1, fifteen relational application tables, per-connection foreign
keys, WAL, atomic complete saves and nonfatal duplicate/failure results. Versioned
profiles and stored analysis snapshots are retained without automatic reanalysis.
History supports operator name/notes, scoped/filterable sessions, existing result/
trajectory/mock views and fixed 0..100 native WPF progress. Source paths are never
persisted. Import/analysis remain usable when initialization fails.

Verification: 0 build warnings/errors; 395/395 tests including all 364 previous
cases. Fresh storage, both contract versions, duplicates, multiple score sessions,
operator metadata, full process restart and both persisted replay views were
checked in WPF. Read-only database inspection confirmed counts/foreign-key joins.
PERSISTENCE_HISTORY.md records the schema, policies, async rationale, demonstration
and limits. Stage 7 settings/profile editing remains unimplemented.

## Stage 7 - configuration

- Add UI to inspect and edit exercise profiles, reference points, route zones,
  criterion descriptions, weights, thresholds, rule types, and enabled states.
- Validate configuration before saving and explain invalid values.
- Apply an explicit policy for profile revisions and historical reanalysis.

Acceptance: instructors can change supported settings without editing source;
new analyses follow valid settings and history retains understandable context.

## Stage 8 - sample sessions and automated tests

- Prepare clearly labeled synthetic JSON sessions for both exercise types,
  malformed/incomplete input, weak results, and repeated operator sessions.
- Cover all six laboratory scenarios in PROJECT_CONTEXT.md.
- Add focused automated tests for validation, scoring, route calculations,
  recommendation evidence, persistence, and history where appropriate.
- Keep samples neutral; do not imply DroneDiploma already exports their fields.

Acceptance: deterministic fixtures and tests demonstrate the six scenarios.
Add focused tests earlier when needed to validate the service under development;
this stage completes the demonstration set and regression coverage.

## Stage 9 - final polish and laboratory demonstration

- Improve navigation, error messages, empty states, readability, and replay use.
- Review realistic import/analysis performance and local storage reliability.
- Document setup, contract, scoring, profile editing, data location, limitations,
  and the simulator boundary.
- Prepare a repeatable walkthrough of all six laboratory scenarios.
- Collect concrete evidence for the laboratory quality attributes; avoid claims
  beyond the implemented behavior.

Acceptance: the .NET 10 application builds, relevant automated checks pass, and
the instructor can complete the laboratory demonstration using prepared data.
