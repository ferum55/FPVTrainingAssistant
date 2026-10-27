# Session contracts 1.0 and 1.1

## Scope and representation

The root JSON object represents one TrainingSession, not a simulator-specific
envelope. Contract 1.0 remains the supported analytical session format. Contract
1.1 adds optional telemetry and neutral replay context for a future simulator
consumer. The importer accepts exactly 1.0 and 1.1; 2.0 and other versions fail
with UnsupportedContractVersion. The TrainingSession default remains 1.0, but
JSON must always supply the version. Existing 1.0 fixtures remain unchanged.

Stage 5.5A implements the extension and assistant-side simulator handoff. The
existing analysis, scoring and local 2D replay also work with 1.1. A 1.1 file
need not include replay context or new telemetry. Producers adding these fields
should declare 1.1. Recognized extension fields are validated in either supported
version, so changing the version to 1.0 cannot bypass their checks. No simulator
exporter or simulator-side replay is implemented here; see
[REPLAY_INTEGRATION.md](REPLAY_INTEGRATION.md).

JSON property names use camelCase; C# properties use PascalCase. SessionJson.Options
centralizes System.Text.Json configuration and is read-only. Import tolerates
property-name casing and ignores unknown properties for compatible future additions.
Generated JSON uses camelCase. Comments, trailing commas, numeric strings, malformed
JSON, and duplicate object properties (ignoring property-name casing) are rejected.
Metadata keys are case-sensitive and must be unique; metadata values are strings.
IDs are nonempty, non-whitespace strings, independent of SQLite row IDs. IDs
are preserved as supplied, compared ordinally, and must not require personal data.
All numeric data must be finite JSON numbers. Do not use NaN or infinity.

The models are sealed data classes with init properties (and mutable List contents).
They provide required-member declarations, nonblank identity checks, assistant
score bounds, nonnull list initialization, and the reference-radius invariant.
SessionJsonStructureValidator checks required JSON fields and types before
deserialization, so defaults cannot hide missing input and guarded model setters
cannot turn ordinary bad data into technical failures. SessionValidator then
checks the deserialized TrainingSession, accumulating semantic issues. The
importer exposes a session only after both validation phases succeed.

## Units and coordinates

| Quantity | Contract unit/convention |
| --- | --- |
| X, Y, Z, reference points | Meters in one shared local Cartesian frame. X and Y are horizontal; Z points up. |
| AltitudeM | Meters relative to the source's declared local altitude datum. |
| SpeedMps | Meters per second; nonnegative speed magnitude. |
| PitchDeg, RollDeg, YawDeg | Degrees; drone telemetry is normalized after validation to [-180, 180). Entity angles must be finite and consumers normalize them for interpolation. |
| BatteryPercent | Percentage 0..100 when available. |
| ControlLinkQualityPercent, VideoLinkQualityPercent | Optional percentages 0..100. |
| TimeSec | Nonnegative elapsed seconds from session start, for samples and events. |
| StartedAtUtc, EndedAtUtc | Explicit UTC ISO 8601 strings ending in Z or +00:00; C# DateTimeOffset. Producers should use Z. |

The assistant contract is already normalized to meters. Unreal Engine centimeters
are not native assistant units. A future exporter/adapter must convert simulator
units and coordinates before producing this data. Use the same frame for a
session and its profile. This is a local mission coordinate system, not geographic
latitude/longitude. PitchDeg is positive nose-up; RollDeg is positive roll to the
right. YawDeg = 0 points along +X, and positive yaw rotates from +X toward +Y.
The importer wraps finite angles modulo 360 into [-180, 180), without imposing
physical attitude limits. An adapter/exporter must convert source coordinates
and angle conventions into these conventions; DroneDiploma is not modified.
The precise altitude datum remains source-declared and must match the profile.

## TrainingSession root

| JSON field | Type | Required/meaning |
| --- | --- | --- |
| contractVersion | string | Required; exactly "1.0" or "1.1". Missing input is rejected despite the model default; other versions give UnsupportedContractVersion. |
| sessionId | string | Required; globally unique in the assistant database. |
| operatorId | string | Required; internal operator identity, no personal information required. |
| profileId | string | Required; identifies the exercise profile. |
| profileVersion | integer | Required; positive version of the profile used for evaluation. |
| exerciseType | string | Required; conventionally ObstacleCourse or TargetMission. Additional nonblank values remain representable. |
| startedAtUtc | string | Required; UTC start timestamp. |
| endedAtUtc | string | Required; UTC end timestamp, not earlier than the start. |
| sourceSimulator | string | Required; nonblank source name, including an explicit synthetic source for examples. |
| success | boolean or null | Optional; null/omitted means unknown. |
| simulatorScore | number or null | Optional; source-owned score in the source's scale. |
| assistantFinalScore | number or null | Optional; assistant-owned score 0..100, absent/null before analysis. |
| telemetry | array | Required, nonnull; chronological telemetry samples. Empty samples may be represented in memory but are insufficient for trajectory analysis. |
| events | array | Required, nonnull; may arrive out of order and are sorted after successful import. An empty array is allowed; exercise-specific warnings apply below. |
| replay | object or null | Added in 1.1; optional environment/scenario identity and recorded dynamic entity tracks. Omitted/null remains valid. No absolute local source path is stored here. |

Duration is calculated as EndedAtUtc - StartedAtUtc and is excluded from JSON with
JsonIgnore. The model's Duration getter throws InvalidOperationException for
reversed timestamps instead of returning a negative value or hiding invalid data
by clamping. The importer detects this before accessing Duration.

Times are finite seconds from session start and must be >= 0. End time must be
>= start time. Telemetry must already be strictly increasing: out-of-order or
duplicate timestamps are errors, not silently sorted. Events can share a timestamp
and are stably sorted by TimeSec after validation, preserving input order for ties.

The upper boundary permits only floating-point overshoot up to
SessionValidator.TimeToleranceSeconds = 0.000001 seconds. Values greater than
Duration.TotalSeconds + this tolerance are errors. Negative times are always
invalid. Accepted values near the boundary are not clamped. Missing/offset-free
timestamps, nonzero UTC offsets, null array items, and nonfinite values are errors.

### Score ownership

SimulatorScore is optional supplementary information. It is not constrained to
0..100 and must never be overwritten by assistant calculations. AssistantFinalScore
and EvaluationResult.Score use 0..100, with larger values meaning better results.
Null AssistantFinalScore means no assistant evaluation has been recorded.
External producers should omit assistantFinalScore or supply null; a populated
assistant score belongs to an assistant-produced record and must not be treated
as a trusted external calculation. Stage 2 accepts a finite supplied value in
0..100, preserves it, and returns an ImportedAssistantScore warning. It performs
no scoring and never substitutes it for SimulatorScore. SimulatorScore must be
finite when supplied, but its source scale remains unrestricted.

## TelemetrySample

Each object requires numeric timeSec, x, y, z, speedMps, altitudeM, pitchDeg,
rollDeg, and yawDeg. Optional batteryPercent, controlLinkQualityPercent, and
videoLinkQualityPercent may be omitted or null to mean unavailable. Required
numbers cannot be omitted or null; zero is a valid value, not a missing-data marker.

Telemetry values describe the flight at TimeSec seconds after the start. Raw
speed, altitude, pitch, and roll remain supplementary data, not universal quality
indicators. A specific exercise criterion may use them explicitly. No scoring
logic belongs in TelemetrySample.

### Optional telemetry added in 1.1

All these fields may be omitted or null. Numeric fields must be finite when
supplied. Existing required telemetry and percentage rules are unchanged.

| JSON field | Type | Unit, meaning and validation |
| --- | --- | --- |
| verticalSpeedMps | number? | m/s, positive upward and negative downward. |
| cameraTiltDeg | number? | Camera pitch offset relative to the body; positive nose-up, degrees, wrapped into [-180, 180) after validation. Add to body pitch using the consumer's correct local rotation composition. |
| cameraFovDeg | number? | Horizontal FOV in degrees, strictly greater than 0 and less than 180. |
| batteryVoltageV | number? | Full pack volts, >= 0. |
| cellVoltageV | number? | Representative/per-cell volts, >= 0. |
| currentA | number? | Amperes; signed current is allowed, including negative values. Source current-sign convention is source-owned. |
| consumedMah | number? | Consumed capacity in milliamp-hours, >= 0. |
| controlRssiDbm | number? | Control radio RSSI in dBm, no arbitrary signal range imposed. |
| failsafe | boolean? | Control-link failsafe state. |
| flightMode | string? | Neutral open mode, e.g. ACRO, ANGLE, HORIZON, ACRO_TRAINER. Unknown strings remain valid. |
| armed | boolean? | Drone armed state. |

These fields describe recorded state and never add implicit scoring rules.
The existing 2D screen retains its original presentation. Stage 5.5B extends the
shared TelemetryReplayService/ReplayState to expose these sensors in the interactive
simulator-interface mock. Optional numbers interpolate only with both endpoints;
camera tilt follows shortest-path angle interpolation. Discrete booleans/flight
mode hold the earlier sample until the next exact timestamp. These playback rules
do not change validation or scoring; see MOCK_SIMULATOR_REPLAY.md.

## ReplayContext (optional, added in 1.1)

| JSON field | Type | Meaning |
| --- | --- | --- |
| environmentId | string | Required nonblank neutral environment ID, e.g. training-ground-01. Identifies the simulator's static environment; no map assets are embedded. |
| scenarioId | string or null | Optional neutral scenario ID, e.g. obstacle-course-basic. |
| entities | array | Required nonnull array of ReplayEntityTrack. Empty is valid for a static exercise. |

EnvironmentId, ScenarioId when supplied, and ArchetypeId when supplied use open
logical names without `/`, `\`, `:`, single quotes or double quotes. This excludes
filesystem paths, Unreal package paths such as /Game/Maps/Training, and Blueprint
object paths. There is no closed registry in the assistant; future environments
and archetypes remain valid logical names. Values are preserved and case-sensitive.

### ReplayEntityTrack

| JSON field | Type | Meaning |
| --- | --- | --- |
| entityId | string | Required nonblank stable session-local ID; unique ordinally within replay.entities. Events can refer to the same identity. |
| entityType | string | Required nonblank open type, e.g. vehicle, infantry, target, objective, movingObstacle. |
| archetypeId | string or null | Optional neutral visual identity, e.g. training-obstacle; future consumer maps it to local visuals. |
| samples | array | Required nonnull, nonempty array of ReplayEntitySample. |
| metadata | object or null | Optional string-to-string dictionary; unique case-sensitive keys, no null values. |

### ReplayEntitySample

Every sample requires numeric timeSec, x, y, z, pitchDeg, rollDeg and yawDeg.
Position uses meters and the drone's shared local frame; angles use the same
degree/sign conventions. All transform numbers must be finite. TimeSec must be
nonnegative, strictly increasing within each individual track, and no later than
session duration plus the same 0.000001-second tolerance as telemetry. Tracks
may have different sample rates and need not cover the entire session. A single
sample is valid. Samples are never silently sorted.

Optional nullable isActive is boolean (spawned/present versus inactive/removed).
Optional nullable state is an open string such as alive, damaged, destroyed,
moving, stopped or evacuating. Unknown types, archetypes and states are retained;
the assistant assigns no gameplay behavior. Optional metadata has the same
string-dictionary rules as track metadata. Null items, missing transform fields,
invalid types, duplicate JSON properties and invalid metadata are rejected before
analysis. Entity transforms are retained as supplied; future replay consumers
normalize finite angles and use shortest-path angle interpolation.

Only replay-relevant dynamic entities are recorded. Static geometry comes from
environmentId. AI and gameplay physics must not re-simulate dynamic positions:
at original time 30 a vehicle at A must be at recorded A during replay, rather
than wherever newly run AI happens to move it. The future simulator applies
recorded transforms/states. This data has no effect on assistant scores,
mistakes, recommendations, route analysis or current 2D trajectory rendering.

## SessionEvent

Each event requires nonblank eventId, numeric timeSec, and nonblank eventType.
Optional entityId identifies a checkpoint, target, obstacle, or other entity.
Optional value is a string; optional metadata is a string-to-string dictionary.
These optional fields may be omitted or null. Metadata values are strings, not
arbitrary nested JSON. Event IDs must be unique within a session.

Known conventional event names (case-sensitive) are:

- CheckpointPassed
- CheckpointMissed
- Collision
- TargetDetected
- FalseTargetDetection
- TargetHit
- TargetDamageZone
- ObjectiveCompleted
- Crash
- MissionCompleted

EventType is an open string, not a closed enum. Unknown nonblank types remain
representable and are not rejected merely for being unfamiliar. Generic structure,
ID, time, and metadata validation still applies. Names are matched case-sensitively;
Stage 3 analysis ignores unknown events for scoring; the imported data retains them.

| Event type | Minimum payload |
| --- | --- |
| CheckpointPassed, CheckpointMissed | Nonblank entityId identifying the checkpoint. |
| TargetDetected, TargetHit | Nonblank entityId identifying the target. |
| TargetDamageZone | Nonblank entityId and nonblank string metadata.damageZone. |
| Collision | entityId optional; metadata may provide additional details. |
| FalseTargetDetection | entityId optional because no target may be present. |
| ObjectiveCompleted, Crash | entityId optional. |
| MissionCompleted | entityId optional; metadata may contain result details. |

The damageZone metadata key is case-sensitive. Other metadata keys and unknown
event types are retained. The importer does not evaluate damage effectiveness,
target results, or mission success. Separate Stage 3 rules evaluate the explicit
training categories and Success flag without changing import behavior.

## Validation and import results

SessionImportService.ImportAsync(filePath, cancellationToken) asynchronously opens
and parses a JSON file, validates its structure, deserializes TrainingSession
using SessionJson.Options, and calls SessionValidator. On success it normalizes
angles and sorts events. It never invokes analysis, WPF, or SQLite.

ValidationIssue contains Code, Path, Message, and Severity (Error or Warning).
Paths use canonical camelCase names such as $.telemetry[1].batteryPercent, even
when input casing differs. ValidationResult snapshots Issues, Errors, and Warnings;
IsValid means no errors. Independent issues accumulate within each validation
phase. Structural errors stop deserialization; semantic validation starts only
when the structure is valid.

SessionImportResult contains Session, Validation, IsSuccess, TechnicalError, and
optional SourceFilePath. Successful file import sets SourceFilePath to the normalized
absolute physical file path. This is runtime state on the import result and
MainViewModel, never a TrainingSession property or neutral JSON field. Import
does not rewrite the file; simulator handoff uses that same original file.
Failed attempts expose no session. IsSuccess requires a validated session, no
errors, and no technical failure. Warnings alone do not prevent success. Ordinary
bad input, malformed JSON, empty/invalid paths, and missing files return validation
issues without TechnicalError. Access failures, IO failures, and unexpected
technical/deserialization failures use TechnicalError. A technical failure may
have no validation issues: consumers must use IsSuccess, not Validation.IsValid
alone. Requested cancellation propagates OperationCanceledException.

Validation covers IDs/text, positive profileVersion, exact contract version,
explicit UTC, duration/order/range of times, finite numeric data, nonnegative
speed, assistant scores and optional percentages in 0..100, unique event IDs,
nonnull collections/items, string metadata, and required event payloads. It does
not impose arbitrary maximum speed, altitude, position, or attitude limits.

Exercise semantics are checked separately from generic data:

- ObstacleCourse requires at least two telemetry samples and ProfileId. No
  checkpoint events gives NoCheckpointEvents warning. Collisions and missed
  checkpoints are never mandatory.
- TargetMission requires ProfileId. No TargetDetected, FalseTargetDetection,
  TargetHit, TargetDamageZone, or MissionCompleted gives NoTargetEvents warning,
  allowing failed/aborted missions without successful interactions.
- An unfamiliar nonblank ExerciseType gives UnknownExerciseType warning and is
  otherwise accepted if the generic contract is valid.

Import does not perform profile lookup or analysis. Stage 3 SessionAnalyzer takes
a caller-supplied profile and validated session for evaluation/recommendations.
Stage 6 SessionRepository enforces database-level duplicate SessionId detection
after analysis, returning a nonfatal Duplicate result without overwriting history.

## Related domain records

These records are modeled now but are not required nested objects in the session
JSON example. Relationships use IDs, avoiding navigation/serialization cycles.
Names below use C# casing; camelCase applies if serialized as JSON later.

| Class | Required data | Optional/default data |
| --- | --- | --- |
| OperatorProfile | OperatorId | DisplayName, Notes may be null. No session collection. |
| ExerciseProfile | ProfileId, Name, ExerciseType | Version defaults to 1 (positive); IsActive defaults to true; ReferencePoints and Criteria default to empty nonnull lists. |
| ReferencePoint | SequenceNo, X, Y, Z, PointType, GreenRadiusM, YellowRadiusM | Radii are constructor arguments and read-only thereafter; sequence numbers order the route. |
| EvaluationCriterion | CriterionId, Name, Description, Weight, RuleType | IsEnabled defaults to true; Threshold and string-to-number Parameters are nullable. |
| EvaluationResult | ResultId, SessionId, CriterionId, Score, nonblank Explanation | MeasuredValue may be null. Represents assistant evaluation only. |
| Recommendation | RecommendationId, SessionId, Category, Text, nonblank Evidence | Generated priority uses 1 highest, 2 medium, 3 lower. The domain default 0 is not emitted by the generator. |

Required text fields are nonblank. ReferencePoint.PointType and criterion.RuleType
are open strings whose supported conventions will be defined with their services.
Recommendation.Evidence must identify which actual result/event caused the advice;
Stage 3 implements generation separately from import; see ANALYSIS_RULES.md.

## Route zones

Order reference points by SequenceNo to form a polyline. Route distance in the
first prototype uses the 2D X/Y plane. Z remains available for telemetry and
replay but does not contribute to route-zone distance.

At every reference point, radii are finite meters with:

```text
GreenRadiusM >= 0
YellowRadiusM >= GreenRadiusM
```

For each telemetry position:

1. Find the nearest reference-polyline segment in X/Y.
2. Calculate perpendicular distance to its closest point, or endpoint distance
   when the projection falls outside the segment.
3. Interpolate GreenRadiusM and YellowRadiusM between the segment endpoints at
   that closest position.

Classify the distance using the interpolated radii:

```text
distance <= green radius                 -> Green
green radius < distance <= yellow radius -> Yellow
distance > yellow radius                 -> Red
```

Zone percentages and average deviation must be time-weighted so uneven sampling
does not distort results. For each consecutive pair i, i+1, the interval is
TimeSec[i+1] - TimeSec[i]. Use the midpoint between their positions for the
interval's zone classification and assign the entire interval duration to that
zone. The last telemetry sample has no standalone duration after it; do not
extrapolate before the first or after the last recorded sample. Average deviation
uses elapsed-time weighting. Maximum deviation is the maximum evaluated midpoint
distance. Stage 3 RouteAnalyzer ignores zero-length segments if other usable
segments exist, rejects fully degenerate routes, requires unique SequenceNo
values, and keeps the first ordered segment on exact distance ties. It does not
extrapolate missing coverage. Import still performs no route calculations.

## Criterion weights

Stored weights need not sum to 1. Only enabled criteria with positive weights
contribute to normalization:

```text
normalizedWeight = criterion.Weight / sum(enabled positive criterion weights)
```

A disabled zero-weight criterion contributes nothing. Stage 3 rejects any enabled
weight <= 0, even if other enabled weights are positive. Negative or nonfinite
stored weights are invalid. If every criterion is disabled or the total enabled
weight is <= 0, analysis fails with AnalysisConfigurationException. No scoring or
weight normalization is implemented in the models. ANALYSIS_RULES.md defines the
service-level formulas; only the final assistant score is rounded to two decimals.

## Profile versions, history, and duplicates

A session retains both ProfileId and ProfileVersion used for evaluation, matching
ExerciseProfile.ProfileId and Version. Editing a profile must create/retain
version context rather than silently changing the meaning of past evaluations.
Historical EvaluationResult records are not automatically recalculated when a
profile is edited. A future explicit reanalysis operation may generate new results
and must retain the profile version associated with each evaluation. Persistence
must retain the corresponding versioned configuration or an equivalent snapshot.

SessionId is globally unique in the assistant database. Stage 6 persistence
rejects an existing SessionId by default, without silently overwriting history.
An explicit replace/reimport operation may be designed separately later.

Stage 6 stores the exact versioned profile and analysis snapshot, including all
1.0/1.1 telemetry/events/replay records. Historical reconstruction never recalculates
scores. The calculated assistant score is stored separately from any nullable
assistant score originally supplied in JSON. SourceFilePath remains runtime-only
and is null when reopening history. See PERSISTENCE_HISTORY.md.

Stage 7 Settings creates new stored versions rather than editing an old evaluation
configuration. IsActive is mutable version-selection metadata, not permission to
substitute a different version into JSON. Imports resolve their exact requested
ProfileId/ProfileVersion even if inactive. Existing snapshots are never automatically
reanalyzed. See SETTINGS_CONFIGURATION.md for activation and working-copy rules.

## Synthetic example

The following is a contract example only. It is not an export from DroneDiploma
and does not imply that any current simulator exports these fields. The same
small fixture is stored in
[example_contract_v1.json](FPVTrainingAssistant/TestSessions/example_contract_v1.json).
It is not a complete laboratory analysis fixture or an embedded exercise profile.

Additional deterministic synthetic fixtures are in FPVTrainingAssistant/TestSessions:
valid_obstacle_course.json, valid_target_mission.json, valid_unknown_event.json,
invalid_missing_session_id.json, invalid_bad_telemetry_time.json, and
invalid_out_of_range_percentage.json. They exercise import/validation only and
make no claims about exports from DroneDiploma.

```json
{
  "contractVersion": "1.0",
  "sessionId": "synthetic-session-001",
  "operatorId": "synthetic-operator-001",
  "profileId": "synthetic-obstacle-course",
  "profileVersion": 1,
  "exerciseType": "ObstacleCourse",
  "startedAtUtc": "2026-10-26T10:00:00Z",
  "endedAtUtc": "2026-10-26T10:00:10Z",
  "sourceSimulator": "SyntheticContractExample",
  "success": true,
  "simulatorScore": 750.0,
  "assistantFinalScore": null,
  "telemetry": [
    {
      "timeSec": 0.0, "x": 0.0, "y": 0.0, "z": 2.0,
      "speedMps": 0.0, "altitudeM": 2.0,
      "pitchDeg": 0.0, "rollDeg": 0.0, "yawDeg": 0.0,
      "batteryPercent": 100.0,
      "controlLinkQualityPercent": 100.0,
      "videoLinkQualityPercent": 98.0
    },
    {
      "timeSec": 4.0, "x": 8.0, "y": 1.0, "z": 2.5,
      "speedMps": 2.5, "altitudeM": 2.5,
      "pitchDeg": -5.0, "rollDeg": 3.0, "yawDeg": 10.0,
      "batteryPercent": 98.0
    },
    {
      "timeSec": 10.0, "x": 18.0, "y": 0.0, "z": 2.0,
      "speedMps": 0.0, "altitudeM": 2.0,
      "pitchDeg": 0.0, "rollDeg": 0.0, "yawDeg": 0.0,
      "batteryPercent": 95.0
    }
  ],
  "events": [
    {
      "eventId": "synthetic-event-001", "timeSec": 4.0,
      "eventType": "CheckpointPassed", "entityId": "checkpoint-01"
    },
    {
      "eventId": "synthetic-event-002", "timeSec": 6.0,
      "eventType": "Collision", "entityId": "obstacle-02",
      "value": "minor", "metadata": { "note": "Synthetic demonstration event" }
    },
    {
      "eventId": "synthetic-event-003", "timeSec": 10.0,
      "eventType": "CheckpointPassed", "entityId": "checkpoint-02"
    }
  ]
}
```
