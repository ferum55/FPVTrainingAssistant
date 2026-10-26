# FPVTrainingAssistant — project context

## Purpose and users

FPVTrainingAssistant is an independent Windows desktop assistant for an FPV
instructor. Operators perform exercises in an external simulator; the instructor
imports structured session data for analysis. The application must reduce manual
review, apply consistent evaluation criteria, identify mistakes and weak skills,
generate recommendations, retain training history, and show progress over time.

Operator identification uses an internal OperatorId. Personal information is not
required; a display name is optional.

## Technology and architectural boundary

- C#, .NET 10, WPF, nullable reference types, and sensible compiler warnings.
- MVVM with Models, Services, Data, ViewModels, and Views separated.
- System.Text.Json for prototype JSON import; SQLite for local persistence.
- Straightforward, maintainable C# suitable for a university prototype.
- Async APIs where useful, without unnecessary infrastructure.

The integration boundary is:

```text
External simulator -> structured session data -> FPVTrainingAssistant
```

The assistant is simulator-independent and must not depend on Unreal Engine or
its version. DroneDiploma is a reference simulator implemented in Unreal Engine
4.27 with C++ and Blueprints, but it must not be modified for this prototype.
Do not copy Unreal Engine code, libraries, or third-party simulator assets into
this repository. Do not introduce cloud services, authentication servers,
microservices, remote database servers, or unnecessary web technologies.

Business rules belong in services, not XAML code-behind. Code-behind is reserved
for UI-specific behavior. Scoring rules must not be hard-coded into the UI.

## Required prototype behavior

### JSON session import

The instructor selects a JSON session file. Deserialize it, validate its structure
and required data, then analyze it only after validation succeeds. Reject malformed,
invalid, or incomplete input with a clear error and without crashing. The contract
must allow other compatible simulators to supply equivalent neutral data later.

### Operators and sessions

Operator profiles contain an internal ID, optional display name, notes, and training
history. Sessions contain:

- Session ID and operator ID.
- Exercise/profile ID and exercise type.
- Start time, end time, and duration.
- Source simulator and success/result state.
- Final score, telemetry samples, and session events.

Contract 1.0 separates nullable SimulatorScore (source-owned, source scale) from
nullable AssistantFinalScore (assistant-owned, 0..100). Never overwrite the
simulator score. EvaluationResult also represents assistant scoring on 0..100.
Sessions retain ProfileId and ProfileVersion used for evaluation. Duration is
calculated from timestamps, not duplicated in serialized state, and cannot be
negative. See [SESSION_CONTRACT.md](SESSION_CONTRACT.md) for the contract.

### Telemetry

Each sample supports timestamp/elapsed time, X/Y/Z position, speed, altitude,
pitch, roll, yaw, and battery percentage or other available battery data.
Control-link quality and video-link quality are optional.

Normalized contract units are meters for position/altitude, meters per second for
speed, degrees for angles, percentages 0..100 when available, and elapsed seconds
from session start for telemetry/events. Absolute timestamps are UTC ISO 8601.
Use a local Cartesian frame with X/Y horizontal and Z up. The assistant receives
meters, not Unreal Engine centimeters; a future adapter/exporter must perform
any conversion before supplying data.

The local mission frame does not imply latitude/longitude. Positive pitch is
nose-up, positive roll is rightward, and yaw 0 points along +X with positive yaw
toward +Y. Successful import normalizes finite angles into [-180, 180). Exporters
are responsible for translating source conventions; DroneDiploma is not modified.

Raw speed, altitude, pitch, and roll are supplementary metrics. They are not
universal standalone quality indicators; use them for scoring only when a
specific exercise criterion requires them.

### Events

Support checkpoint passed, checkpoint missed, collision, target detected, false
target detection, target hit, target damage zone, objective completed, crash,
and mission completed. Design event types and associated data so additional types
can be added later.

### Configurable exercise profiles

Initially support two exercise types:

| Type | Task | Evaluation evidence |
| --- | --- | --- |
| ObstacleCourse | Fly through gates, checkpoints, narrow sections, and obstacles. | Passed/missed checkpoints, collisions, completion time, and trajectory. Correct execution and fewer mistakes matter more than raw speed. |
| TargetMission | Find the specified target, confirm detection, and attack/hit it. | Correct/false detection, target hit, damage zone, and target result events supplied by the simulator. |

### Recommended route analysis

Exercise profiles support a predefined recommended route using reference points
and configurable green, yellow, and red zones. Compare recorded positions with
that route. Report the percentage of samples/time in each zone, maximum deviation,
and average deviation. Keep the calculation understandable and configurable.

For the initial prototype, use the nearest segment of the ordered reference-point
polyline in the X/Y plane, with perpendicular/endpoint distance. Interpolate green
and yellow radii between that segment's endpoints at the closest position.
Radii satisfy GreenRadiusM >= 0 and YellowRadiusM >= GreenRadiusM. Classify
distance <= green as Green, green < distance <= yellow as Yellow, and distance
> yellow as Red. Z remains available for telemetry/replay but is excluded from
this route distance. Zone percentages and average deviation are time-weighted;
maximum deviation is the maximum evaluated midpoint distance. Stage 3 implements
these rules in RouteAnalyzer; Stage 1 did not include route calculations.

The time-weighting policy assigns each consecutive sample interval to the
zone of the midpoint between its positions. The final sample has no standalone
duration. Average deviation uses elapsed-time weighting. Stage 2 documents this
policy; Stage 3 implements it without a visualization UI.

### Trajectory and replay

Display the recorded flight path, reference route/checkpoints, and distinct route
zones. Allow chronological navigation through telemetry. Reconstruct replay from
timestamp, position, pitch, roll, and yaw. A simple 2D top-down view is sufficient;
video recording is not required.

### Criteria and explainable evaluation

Each configurable criterion supports ID, name, description, weight,
threshold/configuration, rule type, and enabled state. Calculate partial criterion
scores and a final session score. Retain measured values, score, and an explanation
for each result so the instructor can understand why the score was produced.

Stored weights need not sum to 1. Scoring normalizes enabled positive
weights as criterion.Weight / sum(enabled positive criterion weights). If all
criteria are disabled or total enabled weight <= 0, analysis must fail with a
clear configuration error. Stage 3 additionally rejects every enabled criterion
whose weight is <= 0, even if other weights are positive. Disabled zero weights
are allowed. No scoring is implemented in the domain models.

### Recommendations

Generate recommendations from detected mistakes and weak results. Connect each
recommendation to actual session evidence. Examples include checkpoint accuracy,
collision reduction, following the recommended route, target identification,
reducing false target confirmations, and improving attack approach accuracy.

### Session result screen

Show operator, exercise, date/time, duration, final score, criterion scores,
detected mistakes, important events, and recommendations.

### History and configuration

Persist sessions locally in SQLite. The instructor must be able to list sessions
for an operator, reopen a previous session, compare scores, and view progress over
time. Provide UI to inspect and edit exercise profiles and evaluation settings.

Profile edits do not automatically recalculate historical EvaluationResult
records. Retain the profile version/configuration used for evaluation; a future
explicit reanalysis operation may generate new results. SessionId is globally
unique in the assistant database. Future imports reject duplicates by default,
without silently overwriting historical sessions. An explicit replace/reimport
operation may be considered separately.

## Conceptual model

Keep the following class names; introduce supporting classes only where useful:

| Class | Responsibility |
| --- | --- |
| OperatorProfile | Internal operator identity, optional display name, notes. |
| TrainingSession | Session identity, exercise, timing, source, outcome, and related data. |
| TelemetrySample | Time, position, attitude, and supplementary flight metrics. |
| SessionEvent | Extensible event type, time, and associated event data. |
| ExerciseProfile | Exercise type, recommended route, zones, and evaluation settings. |
| ReferencePoint | A reference route point/checkpoint. |
| EvaluationCriterion | Configurable rule, weight, thresholds, and enabled state. |
| EvaluationResult | Measured values, criterion score, and explanation for a session. |
| Recommendation | Evidence-based training guidance for a session. |
| SessionAnalyzer | Uses a TrainingSession and ExerciseProfile to produce EvaluationResult and Recommendation records and a final score. |

Relationships:

- OperatorProfile 1 -> many TrainingSession.
- TrainingSession many -> 1 ExerciseProfile.
- TrainingSession 1 -> many TelemetrySample, SessionEvent, EvaluationResult,
  and Recommendation.
- ExerciseProfile 1 -> many ReferencePoint and EvaluationCriterion.
- EvaluationCriterion 1 -> many EvaluationResult.

## Persistence requirements

Use an understandable relational SQLite schema with foreign keys. Persist at least:

```text
operators
training_sessions
telemetry_samples
session_events
exercise_profiles
reference_points
evaluation_criteria
evaluation_results
recommendations
```

Preserve local data safely and handle failures clearly. There is no remote
database server requirement.

## Reference simulator capabilities and limits

DroneDiploma currently contains flight telemetry, mission time/results, score,
drone usage, signal and battery state, crash state, target damage information,
and functional damage zones. This describes simulator data, not an established
JSON export contract.

Obstacle-course checkpoints, full trajectory recording, and target-detected
confirmation may be represented by prepared JSON fixtures. Documentation must
not claim the current DroneDiploma build already exports these fields. No
simulator changes are required for this laboratory prototype.

## Laboratory work 9 acceptance scenarios

Prepared JSON test files must demonstrate:

| Test | Action | Expected result |
| --- | --- | --- |
| 1 | Import a valid session. | Accepted and analyzed. |
| 2 | Import invalid data. | Clear validation error; analysis does not start. |
| 3 | Analyze an obstacle-course session. | Checkpoint, collision, and route metrics with evaluation. |
| 4 | Analyze a target mission. | Detection/hit events and corresponding evaluation. |
| 5 | Generate score and recommendations. | Final result with explanations grounded in session evidence. |
| 6 | View operator history. | Previous sessions and progress are visible. |

## Quality goals

The laboratory report evaluates efficiency, flexibility, security, reliability,
usability, maintainability, scalability/extensibility, and testability. Keep these
claims defensible through clear boundaries, validation, configurable rules,
local data safety, explainable results, and focused automated tests. Compatibility
and simulator independence are also explicit design goals.

## Contract decisions and remaining details

[SESSION_CONTRACT.md](SESSION_CONTRACT.md) defines supported contract versions 1.0
and 1.1, camelCase
JSON fields, required/optional data, normalized units, UTC timestamps, separate
score ownership, route zones, weights, profile history, and duplicate handling.
IDs are nonempty strings independent of database-generated row IDs. EventType
and ExerciseType remain open strings, with constants for known names; unknown
nonblank types remain representable, and unknown events are not rejected merely
because they are unfamiliar. Relationships use IDs without serialization cycles.

Stage 2 resolves angles, explicit UTC timestamps (Z or +00:00), strictly increasing
telemetry times, stable sorting of events including simultaneous events, a
0.000001-second upper-duration tolerance, and known-event minimum payloads.
TargetDamageZone requires entityId and nonblank metadata.damageZone. Unknown
events remain valid. Unknown exercise types and missing checkpoint/target evidence
produce warnings; ObstacleCourse requires at least two telemetry samples.

Stage 3 ignores zero-length route segments when other usable segments exist,
rejects fully degenerate routes, and resolves exact distance ties by the first
segment in SequenceNo order. The route maximum is the maximum interval-midpoint
deviation. Missing coverage is not extrapolated or penalized implicitly.

The precise source altitude datum and richer exercise-specific target results
remain future integration details. Damage-zone scores are configurable training
categories, not claims about real-world effectiveness or simulator exports.

## Initial delivery scope and repository rules

The first delivery creates only the .NET 10 WPF solution, initial folders, this
context, the implementation plan, and a Visual Studio/.NET .gitignore, then builds
the solution. Models, import, analysis, persistence, configuration, replay, sample
session contents, and automated tests belong to later stages.

The follow-up Stage 1 delivery adds the nine plain domain/data classes, known
event/exercise name constants, SESSION_CONTRACT.md, and a preliminary synthetic
contract example in TestSessions. It does not add SessionAnalyzer, an importer,
full contract validation, SQLite, analysis algorithms, UI functionality, replay,
history, or configuration UI. The full laboratory fixtures and automated test
suite remain planned for later stages.

Stage 2 adds asynchronous JSON import, centralized strict/case-tolerant serialization,
structural and semantic validation with errors/warnings, synthetic fixtures, and
xUnit tests. Failed imports expose no session; malformed input is a validation
failure, while IO/access failures use TechnicalError. The existing domain models
remain unchanged. The main project targets net10.0-windows for WPF and net10.0
as a library excluding UI files, allowing net10.0 tests to reference it directly.
Run the desktop scaffold with `dotnet run --project FPVTrainingAssistant --framework
net10.0-windows`. At the end of Stage 2, scoring and analysis were still planned.

Stage 3 adds deterministic SessionAnalyzer, RouteAnalyzer, focused criterion
evaluation, evidence-based mistakes and recommendations, and default in-memory
profile factories. [ANALYSIS_RULES.md](ANALYSIS_RULES.md) defines the eight rules,
formulas, weights, configuration errors, route geometry, and recommendation
priorities (1 highest, 2 medium, 3 lower). The caller supplies a validated session
and matching profile/version. New assistant scores are returned in
SessionAnalysisResult.FinalScore; the input session and SimulatorScore remain
unchanged. IsSuccessful reports analysis completion, independently of mission
success. Generic ObstacleCourse defaults require caller-supplied course geometry
before their enabled route criterion can be evaluated. At Stage 3, UI, replay,
persistence, and history remained planned; analysis adds no WPF or database dependencies.

Stage 4 adds the English instructor-facing WPF import/result UI with lightweight
MVVM, standard Windows file selection, recoverable error/warning states, and
separate score presentation. A temporary ExerciseProfileProvider supplies explicit
version-1 synthetic profiles matching the demonstration fixtures, including a
clearly synthetic obstacle route. It never rewrites session/profile IDs to bypass
compatibility. Existing import and analysis behavior is unchanged; small service
interfaces support headless ViewModel tests. UI_GUIDE.md records the workflow,
profile geometry, live verification, and limitations. Only the current review is
kept in memory. At Stage 4, trajectory/replay, persistence/history, progress charts,
and profile editing remained planned and their navigation was disabled.

Stage 5 adds a top-down X/Y trajectory/recommended-route view, finite layered
zone context, a current yaw marker, telemetry interpolation, chronological replay,
sample stepping, timeline seek, 0.5x/1x/2x speeds, and synchronized recent events.
Route metrics come directly from the existing analysis result. Analytical Red
remains unbounded; the finite red drawing margin is visual only. Geometry is
cached and rescales on viewport resize. The synthetic obstacle route has turns
and the two main demo fixtures have 21 samples each, preserving score expectations.
TRAJECTORY_REPLAY.md records mapping, angular interpolation, verification, and
limits. This is neither video nor 3D replay. History, persistence, progress charts,
and configuration editing remain planned; History/Settings stay disabled.

Stage 5.5A adds optional contract 1.1 replay data while retaining 1.0 support.
Telemetry gains nullable vertical speed, camera tilt/horizontal FOV, pack/cell
voltage, signed current, consumed capacity, control RSSI, failsafe, flight mode
and armed state. ReplayContext holds neutral environment/scenario IDs and optional
dynamic tracks; tracks record entity identity/type/archetype, timed transforms,
active/state values and metadata. New data is validated and retained without
changing scoring, recommendations or existing 2D replay algorithms.

ReplayCapabilityService reports drone, environment and dynamic-track availability
plus local launch prerequisites. Successful imports retain the absolute source
path only in runtime SessionImportResult/MainViewModel state. Open 3D Replay on
Session result uses FPVTRAINING_SIMULATOR_EXE and safely passes the original file
as `-ReplayMode` and `-ReplayFile=<absolute path>`. Missing prerequisites produce
nonfatal in-app messages; View trajectory still opens local 2D replay. Two synthetic
1.1 fixtures coexist with unchanged 1.0 fixtures. Verification: 320/320 tests,
0 build warnings/errors; obstacle/target scores remain 82.67/90.00.

[REPLAY_INTEGRATION.md](REPLAY_INTEGRATION.md) specifies conceptual source mapping
and future Stage 5.5B in the separate DroneDiploma repository: CLI/file validation,
neutral map/visual lookup, recorded transforms/states instead of gameplay AI/physics,
FPV with body-relative camera tilt/horizontal FOV, third person, minimap, event log,
and synchronized timeline. No simulator code/exporter or 3D rendering is implemented
here. Static worlds remain simulator-owned; source paths stay outside JSON.
Stage 6 persistence/history and Stage 7 configuration editing remain planned.

Repository: https://github.com/ferum55/FPVTrainingAssistant.git

Before work, verify `git remote -v`, `git config --local --get user.name`, and
`git config --local --get user.email`. Expected author: `ferum55`,
`ferko.roman59@gmail.com`. Stop and report a mismatch. Do not modify Git author or
origin, create another GitHub repository, use another GitHub account or the GitHub
account connected to Codex. Do not silently substitute .NET 9 if .NET 10 is absent.
The initial task authorizes no commits or pushes.
