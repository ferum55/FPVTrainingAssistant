# FPV Training Assistant — UI guide (Stages 4–6)

## Start the application

On Windows with .NET 10 and the Windows Desktop runtime/SDK installed:

```text
dotnet run --project FPVTrainingAssistant --framework net10.0-windows
```

Alternatively run FPVTrainingAssistant.exe from the Windows build output.
The application opens centered at 1300 x 800 logical pixels and allows resizing
down to 1080 x 680. Interface text is English. The native Windows file picker
uses the operating system's language.

## Layout and navigation

The dark desktop shell has a left sidebar, a screen heading, an Import session
action, and a status bar. Shared WPF resources define colors, panels, controls,
focus feedback, and validation issue presentation. Session content scrolls
vertically; the shell and navigation remain visible.

| Navigation | Current behavior |
| --- | --- |
| Import session | Opens the start/import screen. Choose JSON file or the header Import session action opens the standard Windows picker. Ctrl+O is also bound to import. |
| Session result | Available after successful analysis; returns to the current result. Disabled before a result exists and during processing. |
| Trajectory | Enabled after analysis with at least two telemetry samples. Opens top-down replay; also available through View trajectory on Session result. |
| History | Enabled when local storage initializes, even without a current import. Browse operators, reopen snapshots and compare score progress. |
| Settings | Disabled; no profile editor exists. |

The start screen explains the workflow, contract versions 1.0/1.1, supported exercises,
and the synthetic built-in demo profiles. The picker initially opens the
TestSessions directory copied beside the executable. JSON is the primary file
filter; an all-files filter is also available.

## Import workflow

1. Choose Import session or Choose JSON file and select a JSON document.
2. The application displays the chosen file name/path and a busy indicator.
3. SessionImportService performs the existing structural and semantic checks.
4. ExerciseProfileProvider looks up an exact ProfileId/ProfileVersion/ExerciseType
   match in its small built-in catalog.
5. SessionAnalyzer runs off the UI thread. Its formulas and recommendation rules
   are unchanged from Stage 3.
6. The successful analysis is automatically saved to local history in one transaction.
7. Session result opens with a saved, duplicate or unsaved status. Save failure does not discard analysis.

While processing, import and navigation commands are disabled. The async command
also prevents duplicate execution. Cancelling the picker leaves the current
session/result untouched. Selecting a new file starts a new review and clears
the previous result, so a failed new import cannot display an old score. Moving
between Import session and Session result otherwise retains the current result.
The active review still contains one session; saved sessions are reopened separately
through History. No session is silently overwritten on a repeated import.

Trajectory navigation keeps the current session. Leaving that screen pauses
playback. A new import replaces its path/route/time state; cancelling file selection
does not replace the session. History is enabled when storage is ready; Settings
remains disabled until Stage 7.

## Built-in profiles and synthetic route

The provider supports these explicit, case-sensitive version-1 identities:

| ProfileId | ExerciseType | Configuration |
| --- | --- | --- |
| synthetic-obstacle-course | ObstacleCourse | Default obstacle criteria/weights plus the synthetic route below. |
| synthetic-target-mission | TargetMission | Default target mission criteria/weights. |

The obstacle demo route has ordered points in meters:

```text
SequenceNo 0: (0, 0, 2)
SequenceNo 1: (8, 0, 2)
SequenceNo 2: (8, 8, 2)
SequenceNo 3: (18, 8, 2)
GreenRadiusM = 1.5; YellowRadiusM = 3 at every point
```

This is synthetic prototype data chosen for the prepared fixtures, not a real
course or an export from DroneDiploma. Generic DefaultExerciseProfiles remain
course-independent. The provider creates fresh configurations, assigns the explicit
catalog identities, and never changes an imported session's IDs or version.
Unknown IDs, other versions, and incompatible types produce a clear profile
unavailable message. There is no fallback that scores a different course silently.

## Validation errors, technical failures, and warnings

Invalid data stays on the import screen with a failure heading, selected file,
and diagnostic list. Each issue shows severity, code, canonical field path, and
message. Choose another file starts another attempt. Malformed JSON, unsupported
contract versions, missing data, and invalid telemetry use existing validation
issues; they do not invoke analysis.

File access/IO failures show concise technical guidance. Unexpected failures do
not expose exception stack traces to the instructor. Profile lookup failures
and AnalysisConfigurationException also appear in the application rather than
closing it. The interface remains usable for another import.

Warnings are retained separately. A successfully analyzed session with warnings
shows an Imported with warnings banner and inspectable issue details on its result
screen. Warnings alone are not fatal. An unfamiliar exercise may import with a
warning but still fail profile lookup because only the two built-in demo types
have analysis configurations; its warnings remain visible on the failure screen.

## Result screen

The overview displays operator ID, exercise, session ID, source simulator, UTC
start date/time, duration in seconds, and explicit session outcome. No operator
name or personal information is required.

Assistant score is shown prominently as NN.NN / 100 with a read-only progress
indicator. It comes from SessionAnalysisResult.FinalScore. Simulator score is a
separate labeled value on the source's own scale; null displays Not provided.
Neither the input SimulatorScore nor imported AssistantFinalScore is overwritten.
There are no undefined qualitative performance labels.

Criterion results map CriterionId back to the selected profile's criterion name.
Every row includes score / 100, measured value (or Not provided), and the original
engine explanation. The profile name, ID, and version are also displayed.

Detected mistakes show category, description, evidence, and recorded time/entity
when available. Empty results show No significant mistakes were detected.

Recommendations show priority, category, text, and supporting evidence. Numeric
priorities map to 1 = High, 2 = Medium, 3 = Low. The presentation orders by priority
ascending, preserving original order among equal priorities. Empty results show
No additional recommendations for this session.

Session events are ordered chronologically, preserving input order at equal
times. Rows show elapsed seconds, event type, entity, optional Value, and readable
metadata key-value pairs. Unknown events remain visible. Dictionaries are not
rendered through Dictionary.ToString(). An empty list has an explicit message.

All presentation numbers use invariant English formatting: scores/measured values
and elapsed event times use two decimals; session duration uses one decimal and
seconds. Engine explanations retain their original units and invariant formatting.

## MVVM and test boundaries

MainViewModel owns current screen, latest imported session, resolved profile,
analysis result, warnings, and busy/error state. ImportSessionViewModel exposes
the start/error presentation; SessionResultViewModel and its item view models
format results without recalculating domain rules. ViewModelBase, RelayCommand,
and AsyncRelayCommand provide lightweight reusable infrastructure.

Interfaces for import, analysis, profile lookup, and file selection allow simple
test doubles. The existing concrete import/analyzer implementations only gain
interface declarations; their behavior is unchanged. FileDialogService alone
depends on WPF/Microsoft.Win32 and is excluded from the net10.0 library target.
ViewModels use portable ICommand and are tested without real windows.

App.OnStartup manually constructs dependencies and assigns MainWindow.DataContext.
The views use ContentControl/DataTemplates for navigation. Code-behind initializes
views; TrajectoryView additionally adapts DispatcherTimer/Stopwatch to the portable
replay ViewModel. TrajectoryMap contains WPF drawing only. Business calculations
stay in services. No third-party UI/MVVM/DI packages are used.

## Verification performed for Stage 4

Build and tests:

```text
dotnet build FPVTrainingAssistant.sln
dotnet test FPVTrainingAssistant.sln
```

Result: 0 build warnings/errors; all 203 tests pass, including all 177 existing
Stage 2/3 tests and 26 new headless workflow/presentation cases. New checks include
both successful exercises, invalid-import analysis gating, compatible profiles,
navigation, warnings, independent score mapping, stable recommendation/event
ordering, metadata formatting, busy/retry/cancel behavior, and error recovery.

The actual Windows WPF application was also launched. Standard file-picker actions
were operated through desktop accessibility/native controls and the rendered
screens were visually inspected. No pixel assertions or UI automation test suite
were added. This live check caught and fixed a read-only ProgressBar binding that
the headless tests could not detect; it now explicitly uses OneWay binding.

| Requested file | Observed live result |
| --- | --- |
| valid_obstacle_course.json | Imported, analyzed, and navigated to Session result; assistant 82.67 / 100, simulator 750.00; criteria, mistakes, evidence-based recommendations, and events displayed. |
| valid_target_mission.json | Imported and analyzed; assistant 90.00 / 100, simulator 750.00; target criteria and chronological TargetDetected/TargetHit/TargetDamageZone events displayed, including damageZone: central. Empty mistake/recommendation states displayed. |
| invalid_missing_session_id.json | Remained on import screen; Error / RequiredField at $.sessionId. No result navigation available. |
| invalid_bad_telemetry_time.json | Remained on import screen; Error / TelemetryTimeOrder at $.telemetry[1].timeSec. |

A valid obstacle file was opened again after the failures, confirming recovery.
Normal and minimum-size layouts, scrolling, and disabled future navigation were
visually reviewed at 125% Windows display scaling.

## Trajectory and replay (Stage 5)

Open View trajectory on a result or Trajectory in the sidebar. The X/Y map fits
the recorded path, recommended route, and visible zones without distorting aspect
ratio. It rescales on resize. +X points right and +Y up; the current marker shows
yaw. Reference labels identify ordered route points. The right column contains
current telemetry, existing route metrics when available, and events from the
previous two seconds; scroll the column to inspect its lower cards.

Use Play/Pause, Restart, Step back/forward, the timeline, and 0.5x/1x/2x speed.
Seeking and stepping pause playback. Steps use real sample timestamps. Replay
stops at the last sample without looping; Restart returns to the first sample
paused. Missing sensor values show Not available. No route is valid and displays
a clear message while flight replay continues to work.

The visual red band is finite context; analytically every point outside the
yellow radius is Red. The UI does not rescore the route. See
[TRAJECTORY_REPLAY.md](TRAJECTORY_REPLAY.md) for projection, interpolation, angular
wrapping, rendering approximation, controls, verification, and limitations.

Stage 5 verification: 245/245 tests pass and build has 0 warnings/errors, retaining
all 203 Stage 4 tests. Both valid fixtures have 21 synthetic samples; their scores
remain 82.67 and 90.00. Live checks covered replay controls/speeds, events, metrics,
resizing, no-route TargetMission, new-session reset, and a temporary 6000-sample
path. The Stage 4 verification above remains a record of that delivery.

## Open 3D Replay — interactive prototype (Stage 5.5B)

The Session result screen has two independent actions: View trajectory opens the
existing local 2D screen; Open 3D Replay opens Simulator Replay Prototype, an
interactive WPF mock of the future external replay interface. It is unavailable before a result is loaded or
while importing. Once a result exists, clicking can explain unmet prerequisites
without losing that result or the ability to use 2D replay.

Try replay_obstacle_course_v1_1.json or replay_target_mission_v1_1.json in
TestSessions. Both are synthetic 21-sample files with camera/battery/link/flight
state extensions, a neutral environment/scenario and a moving entity track.
Their scores remain 82.67 and 90.00. Existing 1.0 sessions still import and replay
locally; the mock preview requires Contract 1.1 replay context, a neutral environment
and at least two drone samples.

No simulator executable, environment variable or surviving source file is needed
after successful import. The mock uses actual loaded data and does not launch
DroneDiploma. The preserved future external launcher uses FPVTRAINING_SIMULATOR_EXE
and `-ReplayMode -ReplayFile="absolute path"`; that protocol remains documented in
[REPLAY_INTEGRATION.md](REPLAY_INTEGRATION.md), outside current laboratory wiring.

The shell displays an in-app status message above the current screen:

- Contract 1.0: "3D replay preview requires Contract 1.1 replay context. Analysis and 2D trajectory remain available."
- No replay environment: "This session does not contain the environment information required for 3D replay."
- Insufficient samples: "At least two telemetry samples are required for replay preview."
- Successful open/activation: "Replay prototype initialized successfully. Mock integration preview; no external simulator was started."

The window displays actual session/environment/scenario/source and counts. FPV
shows a technical reticle/HUD; Third Person shows a stylized drone with recorded
heading, position and attitude. Both are explicitly mock visualizations without
a rendered simulator world. Recorded telemetry includes available 1.1 sensors;
missing optional values display N/A. The right-hand telemetry list scrolls.

Use Play/Pause, Restart, Step Back/Forward, timeline seek and 0.5x/1x/2x. The first
and last recorded drone timestamps bound playback. Seek/steps pause; Restart returns
to the beginning paused; replay stops at the end without looping. Camera switching
and panel toggles preserve time and playing state. The minimap uses the actual
flight path, optional profile route and active entity positions. Hide Minimap frees
viewport width; Hide Event Log frees viewport height.

The event log is chronological with Occurred/Upcoming indicators and Latest
highlighting, plus readable metadata. Seeking updates events and entities together.
Reopening the same session activates the existing mock window; closing stops its
clock and leaves the main review and trajectory state available. A new import
clears launch status and replaces runtime source/capability. Files remain unchanged;
absolute paths never enter TrainingSession JSON.

Stage 5.5B verification: 0 build warnings/errors and 364/364 tests, retaining all
320 previous cases. Both 1.1 fixtures were opened in the real WPF mock, including
camera changes, controls/speeds, panels, event/entity data, duplicate-window
activation and safe close during playback. The 1.0 obstacle fixture kept score
82.67 and local 2D replay while displaying the explicit preview limitation.
No external simulator was started. [MOCK_SIMULATOR_REPLAY.md](MOCK_SIMULATOR_REPLAY.md)
records exact behavior, lifetime, interpolation and demonstration results.

## History, operators and progress (Stage 6)

SQLite storage is created automatically at
`%LOCALAPPDATA%\FPVTrainingAssistant\assistant.db`. It survives restart and does not
modify imported JSON files. A successful import shows Session saved to history.
An existing SessionId shows This session already exists in history and was not
saved again. A storage failure shows that analysis completed but could not be saved;
the current result/replay remains usable. Initialization failure disables History
and clearly reports unavailable storage while allowing import and analysis.

Open History and choose an operator. The selector shows optional display name,
stable ID, total session count, last date and average score. Expand Operator details
to edit optional DisplayName and Notes, then Save operator details. OperatorId is
read-only and no personal information is required.

The session table is newest first and shows local date/time, exercise, session ID,
duration, assistant score, independent simulator score and outcome. Choose All
exercises, ObstacleCourse or TargetMission to filter. Count, average, best and
latest scores and the native 0..100 progress chart follow that filter; chart points
are chronological. Empty operators/history/filter results have clear messages.
Refresh or navigating back to History loads current saved data without restarting.

Select a row and Open Result. The existing screen displays Historical session and
uses stored scores, criterion explanations, mistakes/recommendations and route
metrics. It does not rerun analysis. View trajectory uses saved telemetry; a 1.1
session with context also opens the existing mock through Open 3D Replay. Neither
requires the original JSON file. The future real external launcher would need an
explicit file export/reconstruction; that is not included.

For a short progress demonstration import history_progress_01.json through
history_progress_03.json. Their recorded events produce 56.00, 82.67 and 100.00 for
one synthetic operator on different dates. They do not bypass scoring by supplying
assistant scores. Existing obstacle/target fixtures retain 82.67/90.00.

Stage 6 verification: build has 0 warnings/errors; 395/395 tests pass. Live checks
covered fresh empty history, automatic saves, both contract versions, duplicates,
operator metadata, filters and chart. A complete shutdown/restart preserved the
operator details and reopened result/trajectory/mock data. See
[PERSISTENCE_HISTORY.md](PERSISTENCE_HISTORY.md) for exact schema and evidence.

## Current limits

The current selected screen/replay position is held in memory, while saved sessions
and operator metadata survive restart in SQLite. There is no settings/profile editor,
automatic reanalysis, session deletion/export UI, video/3D renderer, authentication,
real simulator-side replay implementation or exporter.
Stage 5.5B supplies an interactive interface mock with actual recorded data; real
DroneDiploma FPV/third-person 3D rendering remains future production work. The
original 2D screen retains its existing presentation. Imported
files are read without modification. Built-in profiles support only their exact
synthetic identities and version 1. Large event/result lists have a straightforward
scrolling card layout rather than a dedicated high-volume event browser.
