# FPV Training Assistant — defense summary

The prototype helps an **FPV instructor** reduce repetitive session review and
apply consistent evaluation criteria. An internal OperatorId is sufficient;
personal information is optional.

```text
External simulator -> structured session data -> FPV Training Assistant
```

This Windows application uses C#, .NET 10, WPF/MVVM and local SQLite. Models,
Services, Data, ViewModels and Views separate neutral data, business rules,
persistence and presentation. It is independent of Unreal Engine versions.

The instructor imports JSON, reviews validation, then sees the matched exercise
profile's analysis and automatically saved history snapshot. ObstacleCourse uses
checkpoint/collision evidence, time and recommended-route deviation. TargetMission
uses correct/false detections, confirmed hits, damage categories and mission result.
Enabled positive weights produce the assistant's 0..100 score; each criterion
retains its measured value and explanation. Mistakes and recommendations carry
actual session evidence. A simulator's own score remains separate.

The X/Y trajectory shows recorded movement, route zones and heading with time
navigation/replay. Contract 1.0 remains supported; 1.1 adds optional sensors/camera
data, environment identity and recorded entity tracks. Open 3D Replay opens an
interactive **Simulator Replay Prototype** with FPV/Third Person presentations,
minimap and event log, using actual recorded data. It is a WPF interface mock,
not a rendered DroneDiploma/Unreal world. DroneDiploma is unchanged.

SQLite retains sessions, telemetry/events, profile configurations and explainable
analysis snapshots. History supports operator metadata, statistics, progress and
reopening results/replays without recalculating scores. Settings edits detached
copies and saves new immutable profile versions; activation never upgrades a
session's explicit version. Replay defaults affect newly opened controllers.

Final verification: **465/465 tests pass**, build has **0 warnings/errors**. Unit,
service, ViewModel, persistence and laboratory integration tests retain the
82.67/90.00 baselines. All six Lab 9 scenarios and complete restart/persistence
were exercised in WPF; the 6001-sample workflow was measured headlessly. See
[QUALITY_REVIEW.md](QUALITY_REVIEW.md) for evidence and limits.

Recommended short demonstration:

1. Use a clean isolated demo directory; import 02 to show one clear validation error.
2. Import 01, then 03; show scores, criterion explanations and evidence-based guidance.
3. Open 03 trajectory and timeline; show green/yellow/red exposure.
4. Show target 04/05 results, then 09 mock FPV/Third Person, minimap and events.
5. Import 06–08; select demo-operator-01 in History and show 56.00 → 82.67 → 100.00.
6. Reopen a saved result/replay; show Settings/new profile version and retained v1.
7. Restart with the same data directory to show retained metadata, history and settings.

[DEMO_SCENARIOS.md](DEMO_SCENARIOS.md) gives exact fixtures, actions and screenshot
targets. Every Demo JSON is synthetic, not an established DroneDiploma export.

Accepted limits: two exercise types; no real simulator exporter/Unreal integration,
video, cloud or authentication; SQLite is not application-encrypted; no graphical
route dragging, automatic historical reanalysis, session deletion or export UI.
These boundaries keep claims aligned with an implemented university prototype.
