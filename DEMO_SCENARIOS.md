# Final laboratory demonstration — Stage 8

All files in `FPVTrainingAssistant/TestSessions/Demo/` are **synthetic prototype
data**, identified by `sourceSimulator: SyntheticLaboratoryDemo`. They were not
exported by DroneDiploma. This demonstration does not implement or claim a
DroneDiploma JSON exporter, simulator integration or Unreal Engine 3D replay.
The replay prototype shows recorded data in technical FPV/Third Person views;
it does not render a simulator world, run AI/physics or record video.

## Dataset and expected results

Use the numbered Demo folder for the final presentation. Existing sibling
fixtures remain regression inputs and are not required for this walkthrough.
Both projects copy the nested Demo JSON directory to build/test output; no
installation into arbitrary user folders or absolute fixture paths is required.
The native picker starts in the output TestSessions folder; enter its Demo folder.

Every valid file requests profile version **1**, available on clean initialization:
`synthetic-obstacle-course` or `synthetic-target-mission`. Settings version 2 is
optional demonstration work, never a prerequisite. All valid SessionIds are unique.

| Lab scenario / purpose | Fixture in Demo | Contract | Action | Expected visible result |
| --- | --- | --- | --- | --- |
| 1 — valid import | 01_valid_obstacle_course.json | 1.0 | Import | Result opens, score **100.00**, three passed checkpoints, saved-to-history message. |
| 2 — invalid import | 02_invalid_session.json | 1.0 | Import | One `RequiredField` error at `$.sessionId`; no analysis/result/history save. |
| 3 and 5 — obstacle errors and explanations | 03_obstacle_course_with_errors.json | 1.0 | Import, inspect criteria/guidance, View trajectory | **77.42**; 2 passed / 1 missed, 1 collision; green/yellow/red **80/5/15%**, average deviation **1.09 m**, maximum **4.50 m**; route and event-based guidance. |
| 4 — strong target mission | 04_target_mission.json | 1.0 | Import | **90.00**; correct detection, confirmed hit, `central` damage zone and success; four explained target criteria. |
| 4 and 5 — weaker target mission | 05_target_mission_with_errors.json | 1.0 | Import, inspect mistakes/guidance | **17.50**; one correct and one false detection, no confirmed hit, explicit failure; evidence-based recommendations. |
| 6 — weak history performance | 06_history_progress_01.json | 1.1 | Import | **56.00**; three missed checkpoints and one collision. |
| 6 — medium history performance | 07_history_progress_02.json | 1.1 | Import | **82.67**; two passed, one missed checkpoint and one collision. |
| 6 — strong history performance | 08_history_progress_03.json | 1.1 | Import, open History | **100.00**; all three checkpoints passed, no collision; three-session progress available. |
| Replay / obstacle baseline | 09_replay_obstacle_course_v1_1.json | 1.1 | Import, Open 3D Replay | **82.67**; extended telemetry, recorded moving obstacle, minimap and chronological events in the labeled mock. |
| Replay / target baseline | 10_replay_target_mission_v1_1.json | 1.1 | Import, Open 3D Replay | **90.00**; recorded target, detection/hit/damage events, extended telemetry and both mock camera presentations. |

The canonical invalid file is otherwise the valid 01 data with only SessionId
omitted. Other invalid sibling fixtures are for automated validation regressions.
Each valid session has 21 chronological drone samples over 10 seconds. The 1.1
files also contain one six-sample dynamic track. Every event/sample is within the
session duration, and UTC timestamps use Z. Mission-frame coordinates are meters;
elapsed time is seconds and attitude is degrees, as in SESSION_CONTRACT.md.
The neutral replay environment is `synthetic-training-ground-01`, with scenarios
`obstacle-course-basic` / `target-mission-01`; these IDs do not imply map assets.

Scores are calculated by the existing analyzer. No Demo file supplies
AssistantFinalScore. SimulatorScore 750 is a separate synthetic source-scale value,
not an assistant score. The `central` damage label uses the existing Default category
score 50, explaining the strong target score 90 rather than 100. The error obstacle
path deliberately leaves the recommended route; the baseline replay path stays
green. These examples serve different purposes and need not have the same score.

## Clean launch, reset and restart

On Windows with .NET 10, from the repository root:

```powershell
dotnet build FPVTrainingAssistant.sln
$demoDirectory = Join-Path $env:TEMP ("FPVTrainingAssistant-demo-" + [guid]::NewGuid().ToString("N"))
dotnet run --project FPVTrainingAssistant --framework net10.0-windows -- --data-directory "$demoDirectory"
```

The explicit developer option selects `<directory>/assistant.db`, creating schema
2, baseline v1 profiles and default preferences on first launch. It does not reset,
seed sessions, copy the normal database or import files automatically. The
directory must be absolute; malformed launch arguments produce a startup message
instead of silently selecting the normal database. There is no reset UI button.

For a **restart check**, close the application and its mock windows, then repeat
the run command with the **same** `$demoDirectory`. Record that path if changing
PowerShell sessions. History and settings should remain. To **reset the demo**,
close the application and assign a new GUID directory before launching again.
This procedure needs no deletion, SQL edits or modification of user training data.

Without this option, the normal database remains at
`%LOCALAPPDATA%/FPVTrainingAssistant/assistant.db`. Do not reset that database merely
to rehearse: use an isolated directory. The output executable accepts the same
`--data-directory` arguments. Normal development launch behavior is unchanged.

The same SessionId cannot be saved twice. Reimporting a Demo file shows a duplicate
warning while retaining its analyzed result; history is not overwritten. Start
with a fresh demo directory when presenting first-time import/save behavior.

## Short live demonstration order

1. Launch with a fresh directory and show the start/import screen.
2. Import 02. Show the single missing-ID error; History is still empty.
3. Import 01. Show the accepted result, separate assistant/source scores and saved status.
4. Import 03. Explain checkpoints, collision, route criterion and weighted final score.
   Scroll to mistakes/recommendations and their evidence.
5. Open View trajectory. Show the recorded path, recommended route/zones, metrics,
   Play/Pause, a sample step and timeline seek. Contract 1.0 supports this 2D replay.
6. Import 04, then 05. Contrast actual target events and the resulting explanations.
7. Import 09 and Open 3D Replay. Point out **Simulator Replay Prototype / Mock
   integration preview**. Seek to 5 seconds; switch FPV / Third Person and show
   minimap, moving entity and occurred/upcoming events. Close the mock when done.
8. Optionally repeat with 10, seeking to 8 seconds to show the target damage event.
9. Import 06, 07 and 08. Open History and explicitly select **demo-operator-01**.
10. Show the three sessions, statistics and chronological score-progress chart.
    Open a saved result, trajectory and its Contract 1.1 mock replay.
11. Open Settings. Inspect baseline criteria and route table. Optionally change
    collision penalty to 40, Save as New Version and activate v2. Old results
    remain v1; imported files explicitly requesting v1 still use v1.
12. Set replay defaults to 2x and minimap off; Save application settings. Close
    all app windows and restart with the same directory. Show retained history,
    a saved result/trajectory/mock and persisted Settings.

Progress uses only **demo-operator-01**, avoiding dilution by other demo cases:
21 Oct 2026 **56.00**, 23 Oct **82.67**, 25 Oct **100.00**. Count **3**, average
**79.56**, best/latest **100.00**. Other cases use **demo-review-operator**. IDs
and optional display names are synthetic; no personal data is needed. All input
timestamps are UTC; History displays local time, including daylight-saving changes.

## Screenshot checklist for the laboratory report

These are suggested captures, not assertions that report images have been inserted.
Do not include unrelated desktop windows or personal data.

1. Import/start screen, before importing data.
2. Valid ObstacleCourse Session Result (01), with score and saved status.
3. Error obstacle trajectory (03), showing path, route zones and deviation metrics.
4. Invalid-session validation error (02), with `$.sessionId` visible.
5. TargetMission Session Result (04); optionally include 05 evidence/guidance.
6. Mock replay FPV mode (09 or 10), with the prototype label visible.
7. Mock replay Third Person with minimap/event log (09 at 5 s, or 10 at 8 s).
8. History for demo-operator-01 with three sessions and progress chart.
9. Settings/profile criteria editor.
10. Optional Settings selector showing retained v1 and active v2.

Report files are not modified by this task. Custom-drawn chart/map appearance is
visually inspected; automated tests verify data/ViewModel state, not pixel images.

## Actual running-application verification

Verified on Windows with .NET SDK 10.0.403 on 27 October 2026, using the built WPF
application, native JSON picker, real SQLite storage and final Demo files. UI
controls were exercised and their resulting text/state inspected; trajectory and
history chart were also visually inspected. These observations are separate from
the headless integration tests.

| Laboratory scenario | Actual outcome | Observed evidence |
| --- | --- | --- |
| 1 — valid import | **PASS** | 01 opened Session Result at 100.00, exact profile v1 and "Session saved to history." |
| 2 — invalid import | **PASS** | 02 showed only RequiredField at $.sessionId, no result; History showed zero sessions. A subsequent valid import recovered normally. |
| 3 — obstacle analysis | **PASS** | 03 showed 77.42, checkpoint 66.67, collision 80, route 82.50 and time 100; trajectory visibly showed route departure and green/yellow/red context with 80/5/15% metrics. |
| 4 — target analysis | **PASS** | 04 showed 90.00 with detection/hit/central damage; 05 showed 17.50, one false confirmation, no hit and mission failure. |
| 5 — explainability | **PASS** | Criterion measurements/explanations and mistake/recommendation evidence were readable for 03 and 05. Obstacle weighted score = round(66.6667×0.4 + 82.5×0.3 + 80×0.2 + 100×0.1) = 77.42. |
| 6 — history/progress | **PASS** | 06–08 saved under one operator. Three rows, 79.56 average, 100.00 best/latest and rising 56/82.67/100 chart; Open Result used a historical snapshot, with trajectory and mock available. |

Both 09 and 10 opened the actual mock window. Obstacle seek to 5 seconds preserved
time when switching to Third Person and showed latest/upcoming events. Target seek
to 8 seconds showed target-01 and TargetDamageZone as latest. Metadata, extended
sensors and recorded entities were present. No simulator process was launched.

The application was fully closed and restarted against the same demo directory.
History/progress, saved v1 result, 2D trajectory and stored 1.1 mock reopened.
Settings retained active obstacle v2 with penalty 40, speed 2x, minimap off and event
log on. Historical v1 results stayed unchanged; the reopened trajectory/mock used
2x and the mock offered Show Minimap / Hide Event Log as expected.

Read-only database inspection after closing found **9 sessions, 2 operators,
189 telemetry samples, 31 events, 36 criterion results, 5 replay contexts/tracks,
30 entity samples, 3 profile versions and zero foreign-key violations**. All nine
session/operator/profile joins were valid. The normal per-user database was not
used or reset. Verification artifacts/database stayed outside the repository.

## Automated verification and remaining scope

`LaboratoryScenarioTests` covers all six scenarios using real local services and
temporary SQLite databases, enumerates all ten manifest files, checks the exact
invalid reason, score components, evidence, history recreation without analysis,
settings/version preservation and both final mock ViewModels. It retains baseline
82.67/90.00 assertions. `DemoDataDirectoryTests` protects isolated launch/restart
and invalid argument handling. Existing 436 tests are retained; Stage 8 adds 26,
for **462/462 passing**. Solution build: **0 warnings, 0 errors**.

Stage 8 is complete. Stage 9 may address general visual polish, broader performance
and usability review, and final defense/report preparation. No real simulator
integration, video recording, cloud backend or authentication is implemented.
