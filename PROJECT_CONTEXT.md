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

### Telemetry

Each sample supports timestamp/elapsed time, X/Y/Z position, speed, altitude,
pitch, roll, yaw, and battery percentage or other available battery data.
Control-link quality and video-link quality are optional.

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

## Decisions to resolve during implementation

- Define a versioned JSON contract, units, coordinate axes, angle conventions,
  timestamp/time-zone rules, and required versus optional fields before import.
- Distinguish any simulator-provided score from the assistant's calculated final
  score; define score range, weight normalization, and disabled-criterion behavior.
- Define route interpolation, 2D/3D distance, zone thresholds/boundaries, and sample
  versus time weighting, especially for unevenly spaced telemetry.
- Specify event payloads, checkpoint/target identity, target results/damage zones,
  and behavior for unknown event types.
- Decide duplicate-session handling and how profile edits affect historical
  evaluations so comparisons remain understandable and reproducible.

These are specification details to settle in the relevant stages, not implemented
features or claims about simulator exports.

## Initial delivery scope and repository rules

The first delivery creates only the .NET 10 WPF solution, initial folders, this
context, the implementation plan, and a Visual Studio/.NET .gitignore, then builds
the solution. Models, import, analysis, persistence, configuration, replay, sample
session contents, and automated tests belong to later stages.

Repository: https://github.com/ferum55/FPVTrainingAssistant.git

Before work, verify `git remote -v`, `git config --local --get user.name`, and
`git config --local --get user.email`. Expected author: `ferum55`,
`ferko.roman59@gmail.com`. Stop and report a mismatch. Do not modify Git author or
origin, create another GitHub repository, use another GitHub account or the GitHub
account connected to Codex. Do not silently substitute .NET 9 if .NET 10 is absent.
The initial task authorizes no commits or pushes.
