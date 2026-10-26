# Analysis rules — Stage 3

## Entry points and result meaning

SessionAnalyzer.Analyze(session, profile) accepts an already validated
TrainingSession and an in-memory ExerciseProfile. Call SessionValidator first
(SessionImportService already does this on successful import). The analyzer does
not read JSON, perform IO, invoke WPF, access SQLite, or call external services.

The returned SessionAnalysisResult contains session/profile IDs and profile version,
FinalScore, ExerciseMetrics, criterion results, mistakes, recommendations, optional
route metrics, and a summary. IsSuccessful means analysis completed; it does not
mean that the mission succeeded. Invalid configuration throws
AnalysisConfigurationException. Route evidence with insufficient samples or
non-increasing times throws ArgumentException rather than producing artificial
zero deviations. No partial successful analysis is returned on failure.

Inputs are unchanged, including both SimulatorScore and AssistantFinalScore.
SessionAnalysisResult.FinalScore is the newly calculated assistant score. This
keeps TrainingSession's init properties and imported/historical values intact.
SimulatorScore has a source-defined scale and never participates in these rules.

No randomness, clock, generated GUIDs, or culture-sensitive formatting is used.
ResultId = "{SessionId}:{CriterionId}". Recommendation IDs use the stable suffixes
listed below. Result collections are returned as read-only snapshots.

## Configuration and weights

ProfileId, Version, and ExerciseType must match the session exactly, using ordinal,
case-sensitive string comparisons. Only ObstacleCourse and TargetMission are
analyzable. The generic importer can still accept other exercise types. Profiles
must be active, named, and have at least one enabled criterion with unique,
nonblank criterion IDs. Enabled criteria require names and descriptions.

All stored weights must be finite and nonnegative. **Every enabled weight must
be > 0**, including when other criteria have positive weights. Disabled zero-weight
criteria are allowed and do not participate. Unsupported enabled RuleType values,
incompatible rules, nonfinite parameters/thresholds, and invalid rule parameters
are configuration errors. Disabled rule-specific settings are not evaluated.

```text
normalizedWeight = criterion.Weight / sum(enabled criterion weights)
FinalScore = sum(criterionScore * normalizedWeight)
```

The implementation divides weights by their maximum before summing, an
algebraically equivalent normalization that avoids overflowing very large weights.
Criterion scores are clamped to 0..100. Intermediate scores/metrics are not
rounded. FinalScore is clamped to 0..100 and rounded to two decimal places with
MidpointRounding.AwayFromZero. Higher scores mean better recorded performance.

RuleType and parameter keys are case-sensitive. Unrecognized finite parameters
are retained but unused. The generic EvaluationCriterion.Threshold is not used
by these rules; named Parameters below configure scoring. No arbitrary speed or
altitude criteria are introduced.

## Supported criterion rules

| RuleType | MeasuredValue | Formula and configuration |
| --- | --- | --- |
| CheckpointAccuracy | Accuracy percentage | passed / (passed + missed) * 100; zero when there are no checkpoint outcomes. Explanation includes passed and missed counts. ObstacleCourse only. |
| CollisionAvoidance | Collision count | clamp(100 - collisions * PenaltyPerCollision, 0, 100). Default penalty 20; configured penalty must be finite and >= 0. |
| RouteAdherence | GreenTimePercent | GreenTimePercent + YellowZoneFactor * YellowTimePercent. Red contributes zero. Default factor 0.5; valid 0..1. Explanation includes all zone percentages and average/maximum deviation. |
| CompletionTime | Session duration in seconds | 100 at/below TargetTimeSec, zero at/above MaximumTimeSec, otherwise 100 * (MaximumTimeSec - duration) / (MaximumTimeSec - TargetTimeSec). Both parameters required; target > 0 and maximum > target. |
| TargetIdentification | Detection accuracy percentage | correct / (correct + false) * 100; zero for no attempts. Explanation includes both counts. TargetMission only. |
| TargetHit | TargetHit count | 100 if any TargetHit event exists, otherwise zero. Detection does not imply a hit. TargetMission only. |
| TargetDamage | Best damage-category score | Best score among explicit TargetDamageZone events, using the mapping below; zero for no such events. TargetMission only. |
| MissionSuccess | 1 for true, 0 otherwise | Success == true gives 100; false and null give zero, with distinct explanations. |

CollisionAvoidance, RouteAdherence, CompletionTime, and MissionSuccess can be used
for either supported exercise. Correctness-specific rules must match the exercise.
CompletionTime is capped at 100 and has a lower weight in the default obstacle
profile; finishing faster never compensates without bound for mistakes.

ExerciseMetrics exposes checkpoint passed/missed/total counts and accuracy,
collision count, session duration, correct/false/total detection attempts and
accuracy, hit count, recorded damage-zone labels, and nullable mission success.
Counts use event occurrences, not inferred checkpoints, unique entities, or
telemetry-based target interactions. Unknown event types do not affect counts.

## Damage-zone training categories

Zone label comparisons are case-insensitive; metadata.damageZone is the
case-sensitive contract key. Labels must already be nonblank under session
validation. These values describe simulator training assessment categories,
not a real-world physical effectiveness model.

| Labels | Parameter | Default score |
| --- | --- | --- |
| critical | DamageScore_Critical | 100 |
| engine | DamageScore_Engine | 90 |
| weapon, gun, barrel | DamageScore_Weapon | 80 |
| track, tracks | DamageScore_Track | 70 |
| hull | DamageScore_Hull | 60 |
| Any other nonblank label | DamageScore_Default | 50 |

Configured mapping values must be finite and within 0..100. The event with the
highest configured score wins, so configuration may change category ordering.
Equal scores keep the first event ordered by TimeSec, then EventId ordinally.
Explanation identifies the selected label and score. TargetHit alone does not
invent a damage zone; an explicit damage-zone event does not invent TargetHit.
If damage is recorded without a hit event, only the explicit damage category is
evaluated; the TargetHit criterion still scores zero.

## Route geometry and time weighting

RouteAnalyzer.Analyze(telemetry, referencePoints) uses meters in X/Y only. Z has
no influence on deviation. Reference points are ordered by unique SequenceNo.
At least two points and one non-zero X/Y segment are required. All points/radii
must be finite, with 0 <= GreenRadiusM <= YellowRadiusM. ReferencePoint's
constructor also rejects invalid radii before analysis configuration is built.

Zero-length segments (duplicate coordinates) are ignored when another usable
segment exists. A fully degenerate route is rejected. For each consecutive
telemetry pair A, B:

```text
deltaTime = B.TimeSec - A.TimeSec
midpoint = (A.position + B.position) / 2
t = clamp(dot(midpoint - P0, P1 - P0) / squaredLength(P1 - P0), 0, 1)
nearestPoint = P0 + t * (P1 - P0)
distance = Euclidean XY distance(midpoint, nearestPoint)
greenRadius = P0.GreenRadiusM + t * (P1.GreenRadiusM - P0.GreenRadiusM)
yellowRadius = P0.YellowRadiusM + t * (P1.YellowRadiusM - P0.YellowRadiusM)
```

Evaluate every non-zero route segment and select the minimum distance. Exact
distance ties keep the first segment in SequenceNo order. Projection is clamped
to the endpoints; varying radii are interpolated at that projected position.
The implementation scales projection arithmetic and uses a stable Euclidean
distance calculation to reduce floating-point overflow.

```text
distance <= greenRadius                  -> Green
greenRadius < distance <= yellowRadius   -> Yellow
distance > yellowRadius                  -> Red
```

Assign the whole deltaTime to the midpoint's zone:

```text
TotalEvaluatedTimeSec = sum(deltaTime)
ZoneTimePercent = zoneTime / TotalEvaluatedTimeSec * 100
AverageDeviationM = sum(distance * deltaTime) / TotalEvaluatedTimeSec
MaximumDeviationM = maximum midpoint distance
```

Average deviation is implemented with an equivalent incremental weighted mean.
Unequal sampling intervals therefore contribute proportionally to elapsed time,
not equally per sample. Percentages total approximately 100 within floating-point
precision. The last sample has no duration after it. Do not extrapolate uncovered
time before the first or after the last sample. Total evaluated route time can
differ from session duration. Maximum deviation covers evaluated midpoints,
not individual sample positions or the continuous flight path.

Fewer than two samples, nonpositive intervals/zero evaluated time, or nonfinite
X/Y/time data are rejected. Numeric geometry outside representable floating-point
precision fails clearly instead of returning NaN metrics. No artificial physical
maximum position or speed is imposed.

SessionAnalyzer requires a usable route for enabled RouteAdherence. It also
returns route metrics when at least two reference points and telemetry samples
are supplied without that criterion. A supplied route is validated when evaluated;
an incomplete route without an enabled route criterion is left unevaluated.

## Mistakes and evidence

Mistake detection is independent of which criteria contribute to the final score.
Each event-level mistake retains event ID in Evidence, TimeSec, and EntityId when
provided. Aggregate mistakes contain counts or route metrics; they never invent
unrecorded events. Event mistakes are ordered by TimeSec, then EventId ordinally.

| Exercise | Code | Trigger |
| --- | --- | --- |
| ObstacleCourse | MISSED_CHECKPOINT | Each CheckpointMissed event. |
| ObstacleCourse | COLLISION | Each Collision event. |
| ObstacleCourse | LOW_CHECKPOINT_ACCURACY | Recorded checkpoint accuracy < 80%, including no recorded outcomes (0%). Individual missed checkpoints and the aggregate accuracy describe different evidence. |
| ObstacleCourse | ROUTE_DEVIATION | RedTimePercent > red-zone mistake threshold, default 10%. Configure through SessionAnalyzer(redZoneMistakeThresholdPercent); valid 0..100. Equality does not trigger a mistake. |
| TargetMission | FALSE_TARGET_DETECTION | Each FalseTargetDetection event. |
| TargetMission | TARGET_NOT_IDENTIFIED | No TargetDetected events recorded. |
| TargetMission | TARGET_NOT_HIT | No TargetHit events recorded; detections alone do not establish hits. |
| TargetMission | MISSION_FAILED | Success is explicitly false. Unknown success is not labeled failure. |

## Recommendations

RecommendationGenerator groups actual mistake evidence into the following rules.
One recommendation is generated per matching group, even when multiple events
or the missed-checkpoint/low-accuracy pair support it. Distinct evidence strings
are combined in ordinal order. No supporting mistake means no recommendation.

| Mistake codes | ID suffix after "{SessionId}:rec:" | Priority |
| --- | --- | --- |
| MISSED_CHECKPOINT, LOW_CHECKPOINT_ACCURACY | checkpoint-accuracy | 2 |
| COLLISION | collision-avoidance | 1 |
| ROUTE_DEVIATION | route-adherence | 1 |
| FALSE_TARGET_DETECTION | false-target-detection | 1 |
| TARGET_NOT_IDENTIFIED | target-search | 2 |
| TARGET_NOT_HIT | target-hit | 2 |
| MISSION_FAILED | mission-failed | 1 |

Priority convention: **1 = highest, 2 = medium, 3 = lower**. Current rules use
1 and 2; 3 is reserved for lower-priority guidance. Results are ordered by
priority, then RecommendationId ordinally. Each contains session ID, category,
professional instructor-facing text, and nonblank evidence.

## Default profiles

DefaultExerciseProfiles returns fresh, independent in-memory profiles, Version 1:

- default-obstacle-course: CheckpointAccuracy 40, RouteAdherence 30,
  CollisionAvoidance 20, CompletionTime 10. Time parameters are 60/180 seconds.
- default-target-mission: TargetIdentification 35, TargetHit 30,
  TargetDamage 20, MissionSuccess 15.

The generic obstacle template has **no course route** and cannot be analyzed
with its enabled RouteAdherence until the caller supplies the actual reference
points (optional factory argument) or explicitly disables/removes that criterion.
No course geometry is assumed. Tests use dedicated synthetic route/profile
builders. Stage 5 expands the two demonstration trajectories and updates synthetic
course geometry while preserving their expected final scores. These fixtures
do not claim simulator exports.

## Current limitations and verification

The rules are deterministic prototype assessment rules, not adaptive AI. Only
the eight named rules and two exercises are implemented in the engine. Stage 4
layers a WPF review UI and a small built-in synthetic profile provider over this
engine; Stage 5 adds top-down route drawing and telemetry replay without changing
these formulas (see UI_GUIDE.md and TRAJECTORY_REPLAY.md). A profile editor,
SQLite, historical reanalysis, and persistence remain unimplemented. Profile version and configuration
snapshots still need storage in Stage 6. Course-specific expected checkpoints,
target identity reconciliation, continuous path extrema, and missing-telemetry
coverage penalties are not inferred.

Build and verification commands:

```text
dotnet build FPVTrainingAssistant.sln
dotnet test FPVTrainingAssistant.sln
```

Tests cover route boundaries, interpolation, weighted timing/deviation, degenerate
geometry, rule formulas, profile errors, explanations, immutable inputs,
deterministic IDs/recommendations across cultures, and import-to-analysis use of
synthetic fixtures. All Stage 2 import/validation tests are retained.
