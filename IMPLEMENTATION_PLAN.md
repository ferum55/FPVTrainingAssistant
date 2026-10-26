# FPVTrainingAssistant — implementation plan

The product requirements and boundaries are recorded in [PROJECT_CONTEXT.md](PROJECT_CONTEXT.md).
Each stage should leave a buildable application. Keep changes small, use MVVM,
and avoid adding dependencies before they are needed.

Only the initial scaffold and documentation are delivered now. All functionality
below remains planned, including the models in Stage 1.

## Stage 1 - project structure and models

- Establish FPVTrainingAssistant.sln and the .NET 10 WPF project.
- Separate Models, Services, Data, ViewModels, Views, and TestSessions.
- Add the context, plan, and Visual Studio/.NET ignore rules.
- In the next development step, define the required conceptual classes and
  relationships with nullable reference types and no simulator dependencies.
- Define model ownership and distinguish simulator score from calculated score.

Acceptance: the solution builds; the eventual models cover the conceptual model
without business logic in views. Current delivery covers the scaffold only.

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

## Stage 4 - basic WPF UI

- Add view models, commands, and bindings for import and session selection.
- Present validation errors and loading state clearly.
- Show operator, exercise, time/duration, final and partial scores, explanations,
  mistakes, important events, and recommendations.
- Keep analysis and data access outside view code-behind.

Acceptance: an instructor can import a file and understand its result through
the UI; failed imports leave a usable application.

## Stage 5 - trajectory visualization/replay

- Add a 2D top-down path view with reference route, checkpoints, and zone colors.
- Support chronological navigation and playback based on telemetry time.
- Display reconstructed position and pitch/roll/yaw at the selected time.
- Handle uneven timestamps and practical telemetry sizes without blocking UI.

Acceptance: recorded and reference paths are visible, route zones are distinct,
and navigation follows the telemetry chronologically. Video is not required.

## Stage 6 - SQLite persistence/history

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
