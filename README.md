# FPV Training Assistant

A local Windows desktop prototype for an FPV instructor. Import structured
training sessions, review explainable scores and recommendations, replay recorded
telemetry, and compare an operator's progress without requiring personal data.

Built with C#, .NET 10, WPF/MVVM, System.Text.Json and Microsoft.Data.Sqlite.

## Build, test and run

On Windows, install the .NET 10 SDK with Windows Desktop support. From the
repository root:

```powershell
dotnet restore
dotnet build FPVTrainingAssistant.sln
dotnet test FPVTrainingAssistant.sln
dotnet run --project FPVTrainingAssistant --framework net10.0-windows
```

You can also launch `FPVTrainingAssistant.exe` from the Windows build output.
Normal instructor workflows use the UI and Windows file picker. NuGet restore
needs packages to be available; application processing itself has no network
dependency. The additional net10.0 library target supports headless tests.

## Features and architecture

```text
External simulator -> structured session file -> FPV Training Assistant
```

- Import/validation before analysis; obstacle-course and target-mission exercises.
- Weighted 0..100 assistant scores, criterion explanations, mistakes and
  evidence-based recommendations; simulator scores retain their own scale.
- X/Y trajectory, recommended route/zones and chronological telemetry replay.
- Interactive simulator replay prototype with FPV/Third Person presentations,
  recorded entities, minimap and event log.
- SQLite history, operator display name/notes and a score-progress chart.
- Configurable criteria, typed parameters, route-point table and immutable
  profile versions; saved replay defaults initialize new replay views.

Models hold neutral data; Services own import, analysis and replay calculations;
Data owns SQLite and repositories; ViewModels own presentation state/commands;
Views contain WPF presentation and UI-specific rendering/timers. Business scoring
does not live in XAML code-behind. History reopens saved evaluation snapshots.

Contract **1.0** supplies analytical telemetry/events. **1.1** additionally supports
optional camera/sensor values, neutral environment identity and dynamic tracks.
Both are supported; imported sessions resolve their exact profile ID/version.

## Demonstration and local data

The ten numbered JSON files in `FPVTrainingAssistant/TestSessions/Demo/` are
synthetic prototype data, not DroneDiploma exports. They are copied to
`TestSessions/Demo/` beside build output. The picker initially opens TestSessions.
[DEMO_SCENARIOS.md](DEMO_SCENARIOS.md) supplies the walkthrough, expected results,
clean rehearsal/restart procedure and screenshot checklist.

Default database: `%LOCALAPPDATA%\FPVTrainingAssistant\assistant.db`.
For an isolated developer/demo run, pass an absolute directory:

```powershell
$demoDirectory = Join-Path $env:TEMP ("FPVTrainingAssistant-demo-" + [guid]::NewGuid().ToString("N"))
dotnet run --project FPVTrainingAssistant --framework net10.0-windows -- --data-directory "$demoDirectory"
```

Reuse the same directory on restart; choose a new directory for a fresh rehearsal.
The override does not delete/reset data or import sessions automatically. Database
schema 1 upgrades to 2 without deleting Stage 6 history. Use the application's
History and Settings screens to work with data.

## Prototype boundary

Open 3D Replay opens an **interactive mock using actual loaded session data**.
It does not launch DroneDiploma, render an Unreal world or record video. A preserved
future launcher uses `DroneDiploma.exe -ReplayMode -ReplayFile=...`; production
integration/exporter work is outside this prototype. No Unreal assets are included.

Other limits: synthetic demonstration JSON; no cloud synchronization, authentication
or authorization; SQLite is not encrypted by the application; no graphical route
dragging, automatic historical reanalysis, session deletion or export UI. Current
analysis supports two exercise types and the documented rules, not arbitrary new
rules entered through Settings. There is no AI/ML assessment service.

## Further reading

- [UI guide](UI_GUIDE.md) and [Settings](SETTINGS_CONFIGURATION.md).
- [Session contract](SESSION_CONTRACT.md) and [analysis rules](ANALYSIS_RULES.md).
- [Trajectory/replay](TRAJECTORY_REPLAY.md), [mock replay](MOCK_SIMULATOR_REPLAY.md)
  and [future simulator boundary](REPLAY_INTEGRATION.md).
- [Persistence/history](PERSISTENCE_HISTORY.md), [quality review](QUALITY_REVIEW.md),
  [defense summary](DEFENSE_SUMMARY.md) and [implementation plan](IMPLEMENTATION_PLAN.md).
