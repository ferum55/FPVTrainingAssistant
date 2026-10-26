# FPV Training Assistant — Stage 4 UI guide

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

| Navigation | Stage 4 behavior |
| --- | --- |
| Import session | Opens the start/import screen. Choose JSON file or the header Import session action opens the standard Windows picker. Ctrl+O is also bound to import. |
| Session result | Available after successful analysis; returns to the current result. Disabled before a result exists and during processing. |
| Trajectory | Disabled; drawing and replay are not implemented. |
| History | Disabled; no persistence or history storage exists. |
| Settings | Disabled; no profile editor exists. |

The start screen explains the workflow, contract version 1.0, supported exercises,
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
6. A successful result automatically opens Session result.

While processing, import and navigation commands are disabled. The async command
also prevents duplicate execution. Cancelling the picker leaves the current
session/result untouched. Selecting a new file starts a new review and clears
the previous result, so a failed new import cannot display an old score. Moving
between Import session and Session result otherwise retains the current result.
This is a single-session workspace, not an in-memory history implementation.

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
SequenceNo 2: (18, 0, 2)
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
views; business logic stays in services. No third-party UI/MVVM/DI packages are used.

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

## Current limits

Only the current session/result is held in memory; closing the application loses
it. There is no SQLite, saved history, progress chart, trajectory drawing/replay,
profile editing, authentication, or simulator integration/exporter. Imported
files are read without modification. Built-in profiles support only their exact
synthetic identities and version 1. Large event/result lists have a straightforward
scrolling card layout rather than a dedicated high-volume event browser.
