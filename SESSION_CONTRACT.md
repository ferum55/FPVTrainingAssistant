# Session contract 1.0

## Scope and representation

The root JSON object represents one TrainingSession, not a simulator-specific
envelope. This is a preliminary contract definition; no file importer, complete
contract validator, analyzer, or persistence is implemented in Stage 1.

JSON property names use camelCase; C# properties use PascalCase. Future
System.Text.Json serialization must explicitly use JsonNamingPolicy.CamelCase.
IDs are nonempty, non-whitespace strings, independent of SQLite row IDs. IDs
are preserved as supplied, compared ordinally, and must not require personal data.
All numeric data must be finite JSON numbers. Do not use NaN or infinity.

The models are sealed data classes with init properties (and mutable List contents).
They provide required-member declarations, nonblank identity checks, assistant
score bounds, nonnull list initialization, and the reference-radius invariant.
These basic construction checks are not a complete contract validator: UTC,
cross-field consistency, time ordering, and other contract rules below must be
checked by the future Stage 2 importer before analysis.

## Units and coordinates

| Quantity | Contract unit/convention |
| --- | --- |
| X, Y, Z, reference points | Meters in one shared local Cartesian frame. X and Y are horizontal; Z points up. |
| AltitudeM | Meters relative to the source's declared local altitude datum. |
| SpeedMps | Meters per second; nonnegative speed magnitude. |
| PitchDeg, RollDeg, YawDeg | Degrees. |
| BatteryPercent | Percentage 0..100 when available. |
| ControlLinkQualityPercent, VideoLinkQualityPercent | Optional percentages 0..100. |
| TimeSec | Nonnegative elapsed seconds from session start, for samples and events. |
| StartedAtUtc, EndedAtUtc | UTC ISO 8601 strings ending in Z; C# DateTimeOffset. |

The assistant contract is already normalized to meters. Unreal Engine centimeters
are not native assistant units. A future exporter/adapter must convert simulator
units and coordinates before producing this data. Use the same frame for a
session and its profile. Positive rotation directions, angle wrapping, and the
precise altitude datum still need agreement before adapters/replay are implemented.

## TrainingSession root

| JSON field | Type | Required/meaning |
| --- | --- | --- |
| contractVersion | string | Required; "1.0". The model defaults to 1.0 for new in-memory sessions; producers must write it explicitly. Future import must report unsupported versions clearly. |
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
| events | array | Required, nonnull; chronological events. An empty array is allowed when no events occurred; exercise-specific sufficiency is checked later. |

Duration is calculated as EndedAtUtc - StartedAtUtc and is excluded from JSON with
JsonIgnore. The model's Duration getter throws InvalidOperationException for
reversed timestamps instead of returning a negative value or hiding invalid data
by clamping. The future importer must detect this before accessing results.

Times must lie within [0, Duration.TotalSeconds]; samples/events must be
chronological, and their IDs where present must be consistent. Exact policies
for repeated telemetry times and minimum exercise evidence remain Stage 2 work.

### Score ownership

SimulatorScore is optional supplementary information. It is not constrained to
0..100 and must never be overwritten by assistant calculations. AssistantFinalScore
and EvaluationResult.Score use 0..100, with larger values meaning better results.
Null AssistantFinalScore means no assistant evaluation has been recorded.
External producers should omit assistantFinalScore or supply null; a populated
assistant score belongs to an assistant-produced record and must not be treated
as a trusted external calculation by the future importer.

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
representable and must not be rejected merely for being unfamiliar. Future
analysis may preserve/display them without scoring them. Value/metadata payload
semantics for each exercise, including target results and damage zones, still
require definition before the corresponding analysis rules are implemented.

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
does not distort results. Maximum deviation is the maximum observed route
distance. Exact interval weighting, equal-distance segment ties, degenerate
segments, and insufficient route/sample data must be specified with the analyzer.
No route calculations are implemented in this stage.

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
