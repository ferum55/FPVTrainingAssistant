# Final prototype quality review — Stage 9

Reviewed on 27 October 2026. This is evidence about the implemented university
prototype, not a claim of certification, enterprise security or production-scale
capacity. Historical stage documents retain their delivery-specific test counts;
this document records the final review.

## Quality attributes

| Attribute | Implemented support and concrete evidence | Limits |
| --- | --- | --- |
| Efficiency | Local processing; asynchronous import and worker-thread analysis/SQLite work. [TrajectoryMap](FPVTrainingAssistant/Views/Controls/TrajectoryMap.cs) and [ReplayMinimap](FPVTrainingAssistant/Views/Controls/ReplayMinimap.cs) cache frozen static drawings until scene/viewport changes. Telemetry/entity interpolation uses binary searches; recent events use binary bounds. Mock event rows update only when their presentation state changes. | JSON/domain data and history lists are materialized in memory. The measurements below are one prototype workload, not a throughput or frame-rate guarantee. |
| Flexibility | [SessionJson](FPVTrainingAssistant/Services/SessionJson.cs) supports contracts 1.0/1.1; open event/entity names retain compatible future data. [SettingsViewModel](FPVTrainingAssistant/ViewModels/SettingsViewModel.cs) exposes weights, enabled criteria, supported parameters and route geometry through detached copies. Profile versioning retains historical meaning. | Two exercise types and eight scoring rules are implemented. New rule algorithms, adapters or exercise types require code and tests. |
| Security | Internal OperatorId, optional personal metadata, no cloud upload or authentication server. Structural/semantic input validation precedes analysis. [SqliteData](FPVTrainingAssistant/Data/SqliteData.cs) binds SQL parameters; tests include SQL-like identifiers. Runtime source paths stay outside session JSON/database. | SQLite has no application encryption or access-control UI. There is no user authentication/authorization. Local operating-system permissions remain the boundary. JSON is not authenticated/signed and input size is not bounded by a streaming quota. |
| Reliability | [SessionImportService](FPVTrainingAssistant/Services/SessionImportService.cs) exposes no session on invalid input. Transactions save complete snapshots; duplicate SessionIds do not overwrite history. [DatabaseInitializer](FPVTrainingAssistant/Data/DatabaseInitializer.cs) migrates schema 1 to 2 and preserves seeds/preferences. Foreign keys, WAL and recoverable errors support local data safety. | No application backup/restore UI or fault-tolerant multi-user deployment. A local storage failure can leave the current analysis unsaved; the UI explains that condition. |
| Usability | Guided import and visible errors/warnings; separate assistant/source scores; criterion measurements/explanations, textual recommendation priorities, trajectory/replay and History/Settings. Shared dark dropdown/selected-row styling, readable disabled navigation, friendly parameter labels and early validation cards were checked in WPF. | English UI; the OS controls file-picker language. Dense sessions require scrolling. No formal user study or broad accessibility certification was performed. |
| Maintainability | Models/Services/Data/ViewModels/Views separation and lightweight MVVM. Analysis remains independent of WPF/SQLite; shared validators own rules. Repositories own SQL; views own rendering/timers. Documentation links the contract, scoring and persistence policies. | A straightforward prototype rather than a plugin framework; some UI-specific templates remain local to their screens. |
| Scalability / extensibility | Simulator-neutral IDs, optional 1.1 data, versioned profiles, isolated data-path abstraction and service interfaces. Existing `ix_sessions_operator_time` serves operator/time queries; composite primary keys support many child lookups. | SQLite is local, with no server/cloud or concurrent-user architecture. History is not paginated. Unindexed result/recommendation scans may matter at larger database sizes; no speculative indexes/schema bump were added in Stage 9. |
| Testability | Portable net10.0 tests cover models, validation, services, geometry, replay, ViewModels, temporary SQLite databases and cross-service laboratory scenarios. [FinalReviewTests](FPVTrainingAssistant.Tests/FinalReviewTests.cs) protects event-notification efficiency, simultaneous/backward-seek semantics and a 6001-sample round trip. | Headless tests cannot replace visual review or establish WPF rendering frame rate. Test timing is deliberately not a pass/fail threshold. |

## Measured workload and resource review

The targeted FinalReviewTests large-session run used **6001 telemetry samples,
600 events, a recorded entity track, real JSON import/validation, persisted v1
profile resolution, analysis, SQLite save/reconstruction and 3000 mock seeks**.
The dataset is generated in temporary test storage; final Demo files are unchanged.

Observed on Windows with .NET SDK 10.0.403 / runtime 10.0.14 during this Stage 9
review (single targeted headless run):

| Phase | Observed time |
| --- | ---: |
| Import / validation | 280.2 ms |
| Analysis | 14.4 ms |
| SQLite save | 93.3 ms |
| SQLite reconstruction | 44.0 ms |
| Mock construction and 3000 seeks | 38.6 ms |

These are measured observations, not portable benchmark targets. They exclude
WPF rendering, disk durability certification and long-running memory profiling.
CPU load, cache state, build mode and storage can change them. Reproduce with:

```powershell
dotnet test FPVTrainingAssistant.Tests/FPVTrainingAssistant.Tests.csproj --filter FullyQualifiedName~FinalReviewTests.LargeSession --logger "console;verbosity=detailed"
```

Static map/path drawing remains cached across ticks and rebuilds on resize or new
scene. Drone/entity transforms still come from recorded samples. The event log
formerly scanned/notified every row every frame; it now finds the latest boundary
with binary search and updates crossed rows plus the previous/latest row. Tests
verify no row notifications inside an unchanged interval and correct simultaneous
events/backward seeks. Large jumps still update all crossed rows, as required.

[SimulatorReplayMockWindow](FPVTrainingAssistant/Views/SimulatorReplayMockWindow.xaml.cs)
stops its DispatcherTimer, removes subscriptions and disposes its ViewModel on
close. The window adapter releases its closed-window reference. Trajectory unload
stops its timer, detaches the ViewModel and pauses playback. Database connections,
commands, readers and transactions use disposal scopes; pooling is disabled.
Actual mock close/reopen and full application shutdown/restart were exercised.
No static mutable session store or new indexing/migration framework was added.

## Running WPF review

Final Demo files were exercised through the real Windows picker on fresh isolated
schema-2 storage. All six laboratory outcomes passed again; see the final table in
[DEMO_SCENARIOS.md](DEMO_SCENARIOS.md). The normal user database was not reset or
used for this review. UI automation exercised real controls; custom chart/map and
all screenshot targets were also visually inspected. There are no pixel assertions.

- MainWindow was checked at normal size, its 1080×680 logical minimum and maximized.
  Mock replay was checked at normal size, its 1100×700 minimum and maximized.
  Windows display scaling was 125%; dimensions above are WPF logical units.
- Validation code/path/message remain visible at minimum size. Results separate
  NN.NN / 100 assistant scores from source-scale values. Criterion/evidence text
  wraps and pages scroll. Source paths are file information, not required commands.
- The route path/zones, heading, legend, resize, timeline seek and 2x selector were
  checked. Red analytically remains everything outside YellowRadiusM; its drawn
  band remains finite. No RouteAnalyzer calculation changed.
- FPV and Third Person remain distinct and honestly labeled prototypes. At minimum
  height, compact HUD rows leave the central reticle visible. Minimap/event-log
  show/hide preserves time; latest/occurred/upcoming text accompanies color.
- History includes readable selected rows, operator identity, three-session
  progress, filtering and metadata editing. Empty history, no matching filter,
  no mistakes/recommendations and target no-route states were exercised. Other
  optional-data empty states were reviewed in code and existing replay tests.
- Negative collision penalty prevented saving with a friendly message. Correcting
  it to 40 allowed creating/activating v2. Existing v1 sessions remained v1.
- Full shutdown/restart retained nine sessions, Demo Operator / Synthetic final
  review metadata, active obstacle v2, 2x speed, minimap off and event log on.
  Stored v1 result, trajectory and 1.1 mock reopened without the source file.
  Read-only inspection found 189 telemetry rows, 31 events, 36 criterion results,
  five replay contexts/tracks, 30 entity samples and zero foreign-key violations.

The screenshot checklist covers Import, results, validation error, trajectory,
FPV, Third Person/minimap/log, History and Settings/versioning. Captures used only
synthetic data and stayed outside Git; report documents were not edited. Scrolling
to the relevant card is expected when preparing a focused report screenshot.

## Recovery, diagnostics and final checks

Live review covered invalid-to-valid recovery, empty/filter states and invalid
settings-to-valid-save recovery. Automated service/ViewModel regressions additionally
cover malformed JSON, unsupported contract, unavailable profile, duplicate import,
database failure/fallback and missing replay context. Errors stay in-app and leave
another valid operation available. Parser exception internals were removed from
normal invalid-JSON messages; internal persistence Trace diagnostics remain.

The TODO/FIXME/HACK/NotImplementedException and UI-placeholder search found no
unfinished normal workflow. Debug solution configurations and TestSessions names
are intentional, not instructor-facing debug screens. Prototype wording remains
where it truthfully identifies the mock.

Final complete command results: `dotnet build FPVTrainingAssistant.sln` — **0
warnings, 0 errors**; `dotnet test FPVTrainingAssistant.sln` — **465 passed, 0 failed,
0 skipped**. This retains 462 pre-Stage-9 cases and adds three focused regressions.
Two existing assertions were updated solely for two-decimal percentage display.
Baseline obstacle/target scores remain **82.67 / 90.00**. Scoring formulas, session
contract, database schema and final Demo JSON were not changed.

Remaining accepted limits: no DroneDiploma exporter/integration, Unreal 3D world,
video, cloud synchronization, authentication, application encryption, graphical
route dragging, automatic historical reanalysis, session deletion or export UI.
DroneDiploma was neither modified nor launched.
