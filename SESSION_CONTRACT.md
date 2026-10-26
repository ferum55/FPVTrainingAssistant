# Session contract 1.0

## Scope and representation

The root JSON object represents one TrainingSession, not a simulator-specific
envelope. Stage 2 implements JSON file import and validation in the service layer.
Analysis, scoring, persistence, and application UI remain outside this stage.

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
| PitchDeg, RollDeg, YawDeg | Degrees, normalized after successful validation to [-180, 180). |
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
| contractVersion | string | Required; exactly "1.0". Missing input is rejected despite the model default; another value gives UnsupportedContractVersion. |
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
future analysis may preserve/display unknown events without scoring them.

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
target results, or mission success; those belong to future analysis rules.

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

SessionImportResult contains Session, Validation, IsSuccess, and TechnicalError.
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

Profile lookup, database-level duplicate SessionId detection, evaluation, and
recommendation generation remain later-stage responsibilities.

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
| Recommendation | RecommendationId, SessionId, Category, Text, nonblank Evidence | Priority is an integer defaulting to 0. Its ranking convention remains to be defined with recommendation generation. |

Required text fields are nonblank. ReferencePoint.PointType and criterion.RuleType
are open strings whose supported conventions will be defined with their services.
Recommendation.Evidence must identify which actual result/event caused the advice;
no recommendation generation is implemented now.

## Route zones (future analysis policy only)

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
uses elapsed-time weighting. Maximum deviation is the maximum observed route
distance. Equal-distance segment ties, degenerate segments, and insufficient
route coverage still need decisions with the analyzer. No route calculations
are implemented in this stage.

## Criterion weights (future scoring policy only)

Stored weights need not sum to 1. Only enabled criteria with positive weights
contribute to normalization:

```text
normalizedWeight = criterion.Weight / sum(enabled positive criterion weights)
```

A zero-weight criterion contributes nothing. Negative or nonfinite configured
weights are invalid. If every criterion is disabled or the total enabled weight
is <= 0, analysis must fail with a clear configuration error. No scoring or
weight normalization is implemented in the models.

## Profile versions, history, and duplicates

A session retains both ProfileId and ProfileVersion used for evaluation, matching
ExerciseProfile.ProfileId and Version. Editing a profile must create/retain
version context rather than silently changing the meaning of past evaluations.
Historical EvaluationResult records are not automatically recalculated when a
profile is edited. A future explicit reanalysis operation may generate new results
and must retain the profile version associated with each evaluation. Persistence
must retain the corresponding versioned configuration or an equivalent snapshot.

SessionId is globally unique in the assistant database. Future import/persistence
must reject an existing SessionId by default, without silently overwriting history.
An explicit replace/reimport operation may be designed separately later.

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
