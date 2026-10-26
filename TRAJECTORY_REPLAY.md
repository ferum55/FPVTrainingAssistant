# Trajectory visualization and flight replay — Stage 5

## Purpose and boundaries

The Trajectory screen reconstructs recorded flight state from telemetry. It is
2D top-down replay, not video or a 3D simulator. It uses the active imported
session, resolved profile, and completed analysis. Scoring/validation algorithms
are unchanged. No SQLite, history, progress charts, or profile/settings editor
is introduced.

After analysis with at least two telemetry samples, open Trajectory in the sidebar
or View trajectory on Session result. Otherwise navigation is disabled. Returning
to Session result preserves the session and pauses replay. A new import replaces
the old scene; a successful import initializes the first recorded time, 1x speed,
and paused state. Cancelling the picker does not replace the active session.

## Components and performance

- Point2D, ReplayState, and RouteZoneSegment are plain data without WPF primitives.
- TrajectoryProjectionService creates an aspect-preserving world/view transform.
- TelemetryReplayService snapshots/validates the ordered samples once, uses binary
  search, interpolates state, and locates real sample-step times. It has no timer.
- RouteZoneVisualizationService creates subdivided visual bands and their bounds.
- TrajectoryViewModel owns portable replay state/commands, cached world geometry,
  recent events, and the original analysis route metrics.
- TrajectoryMap is one native WPF FrameworkElement. It caches frozen static
  StreamGeometry/DrawingGroup data and draws the current marker dynamically.
- TrajectoryView adapts DispatcherTimer/Stopwatch to the portable ViewModel and
  stops/detaches its clock when unloaded.

Static geometry is rebuilt only for a changed scene or viewport, not each frame.
There is no WPF control per telemetry sample. Event presentation is cached and
updated only when its selected time-window slice changes.

## Coordinates, projection, and resizing

The normalized mission frame uses meters: X points right and Y up. Z remains
available as telemetry but does not affect the map. This is not latitude/longitude.
Projection includes all flight positions plus the actual visual-zone bounds.
One scale preserves aspect ratio; unused space is centered; Y is inverted for
WPF's downward screen axis:

```text
viewX = centerViewX + (worldX - centerWorldX) * scale
viewY = centerViewY - (worldY - centerWorldY) * scale
```

Centered coordinates are normalized internally to handle very large/small finite
ranges. Half-ranges avoid overflowing max-min. Negative coordinates, straight
vertical/horizontal paths, equal X/Y values, single points, and subnormal ranges
are covered without division by zero. Single points are centered; empty scenes
have a safe message. No fixed world-coordinate range is assumed.

Service padding defaults to 30 device-independent pixels; the map requests 36.
Padding is capped at one quarter of the smaller viewport dimension when necessary.
Drawing uses the actual viewport size and recomputes on resize. Axis indicators
label +X/+Y, with bounds in meters. Reference labels R0, R1, etc. denote SequenceNo;
they do not invent associations with checkpoint-event EntityId values.

## Route and zone rendering

Reference points are ordered by SequenceNo; zero-length segments are skipped.
If no usable route exists, replay still works and displays No recommended route
is available for this exercise. TargetMission receives no artificial route.

Each route segment is split into 12 subsegments by default. Green/yellow radii
are linearly interpolated at each subsegment midpoint; rounded stroke widths
are twice those radii. Draw all red strokes, all yellow, all green, the dashed
reference line, recorded path, reference points, and marker in that order.
This is a practical approximation of varying-width polyline buffers.

```text
VisualRedMarginM = max(2, 0.5 * interpolated YellowRadiusM)
VisualRedRadiusM = interpolated YellowRadiusM + VisualRedMarginM
```

Constructor options configure subdivision count, minimum margin, and margin
factor. TrajectoryViewModel can receive a configured visual-zone service; its
actual output determines the cached bounds. There is no settings UI yet.

**Analytical Red is unbounded:** every position outside YellowRadiusM is Red.
The finite red stroke is only a readable outside-route warning band. Space beyond
the visible band is not analytically safe. Neither this margin nor the drawing
approximation is used in RouteAnalyzer/SessionAnalyzer. Their exact analytical
classification and time weighting remain unchanged.

The legend names recorded trajectory, recommended route/reference points,
current position/heading, Green, Yellow, and Red context. Shared resources define
semantic colors. The route-metric card uses SessionAnalysisResult.RouteAnalysis
directly: Green/Yellow/Red time percentages, average and maximum deviation. It is
absent when metrics are null; no scoring is recalculated in the ViewModel.
The telemetry/metrics/events column scrolls independently on smaller windows.

## Replay interpolation

Replay bounds are first and last telemetry TimeSec, not necessarily zero and
session duration. The timeline uses actual bounds; the header separately shows
full session duration. Out-of-range time clamps to an endpoint. Empty sequences
produce no state; a single sample can be displayed but playback/stepping is
disabled. The service rejects nonfinite required data and duplicate/descending
sample times at construction.

Between surrounding samples A and B:

```text
factor = (time - A.TimeSec) / (B.TimeSec - A.TimeSec)
value = (1 - factor) * A.value + factor * B.value
```

X/Y/Z, speed, and altitude interpolate linearly. Optional battery/control/video
percentages interpolate only when both endpoints supply them; otherwise the
intermediate value is null and the UI displays Not available. Exact samples
return their own optional values. No missing sensor values are invented.

All attitude angles follow the shortest normalized difference:

```text
delta = normalize(normalize(B) - normalize(A))
angle = normalize(normalize(A) + factor * delta)
normalize range = [-180, 180)
```

Thus 179 -> -179 crosses the -180/180 boundary instead of rotating through zero.
An exact 180-degree tie takes the negative direction. Positive pitch is nose-up,
roll is rightward, and yaw rotates from +X toward +Y. The marker rotates by
-YawDeg in screen coordinates. Pitch/roll/Z are numeric telemetry, not 3D graphics.

## Controls and clock

| Control | Behavior |
| --- | --- |
| Play / Pause | Starts/stops elapsed-time playback. |
| Restart | First recorded timestamp; remains paused. |
| Step forward/back | Pauses and selects the strictly next/previous real sample timestamp. Remains at boundaries. |
| Timeline | Seeks to actual session-relative seconds, updates marker/state/events, and pauses. Press Play to resume. |
| Speed | 0.5x, 1x, 2x; default 1x. Changing speed does not reset position. |

The UI clock targets about 30 Hz and advances by Stopwatch-measured elapsed time
multiplied by speed. It assumes neither fixed timer intervals nor uniform telemetry.
AdvanceBy supports deterministic tests without a real clock. At the final sample,
playback stops and retains the final marker; it never loops automatically. Play
is unavailable there until Restart or a seek away from the end. Navigation away
pauses the clock and preserves the current position.

## Current / recent events

The inclusive deterministic event window is:

```text
CurrentTimeSec - 2 <= event.TimeSec <= CurrentTimeSec
```

Future events are not shown early. Seeking selects a new window without changing
stored data. Binary bounds locate the slice of cached chronological event rows;
equal-time order is stable. Rows show time, type, and entity. Empty intervals have
an explicit message.

## Synthetic data

Both valid demonstration fixtures now have 21 samples at 0.5-second spacing over
0..10 seconds. Events, success, duration, score ownership, and criteria are unchanged.
The built-in synthetic obstacle route has turns:

```text
R0 (0, 0, 2) -> R1 (8, 0, 2) -> R2 (8, 8, 2) -> R3 (18, 8, 2)
GreenRadiusM = 1.5; YellowRadiusM = 3
```

Small lateral variation remains entirely Green for evaluated intervals, preserving
assistant score 82.67. TargetMission uses a curved path without a reference route;
its score remains 90.00. This is a synthetic demo-profile revision before storage
exists, not a historical migration. Generic default profiles and analysis formulas
are unchanged. Other contract/invalid/unknown-event fixtures are retained.
No data is claimed to be an export from DroneDiploma.

## Contract 1.1 and external handoff (Stage 5.5A)

The existing 2D services, angle/position interpolation and route calculations are
unchanged. Both supported contract versions 1.0 and 1.1 supply the same required
flight samples. New nullable telemetry and ReplayContext/entity tracks are retained
and validated, but do not alter 2D paths, route scores or interpolation results.
The original analytical view does not render dynamic entities or additional
sensor/camera fields. Stage 5.5B adds a separate simulator-interface mock that uses
the same replay service/controller, now with extended optional sensors in ReplayState.

Session result also offers Open 3D Replay, independently of View trajectory. It
now opens the interactive local mock for a 1.1 session with replay environment
and at least two drone samples. Source files/executables are not required after
import. The external handoff implementation is retained for future production wiring.
Missing preview context is shown in-app and leaves local replay usable.
No absolute source path is serialized into the session. No 3D renderer, video,
Unreal library, physics or AI resimulation is added to the assistant.

Future production integration in DroneDiploma must implement the documented CLI, neutral
environment/visual lookup, recorded drone/entity transforms and discrete states,
FPV with body-relative tilt and horizontal FOV, third-person camera, minimap,
event log and synchronized timeline. Conceptual position interpolation remains
linear and angle interpolation follows the same shortest normalized difference;
normal gameplay AI/physics must not drive recorded replay actors.
[REPLAY_INTEGRATION.md](REPLAY_INTEGRATION.md) specifies responsibilities, partial
coverage/default questions and source-unit mapping. These simulator features are
not implemented or claimed to exist in the current simulator. Stage 5.5B implements
only the interface mock; see MOCK_SIMULATOR_REPLAY.md for its actual-data workflow,
entity/minimap rules, event presentation and live verification. The main trajectory
state is independent of the mock's playback controller.

The new replay_obstacle_course_v1_1.json and replay_target_mission_v1_1.json fixtures
each add synthetic context, extended telemetry and one moving track to the existing
21-sample demonstrations. Existing 1.0 fixture files remain unchanged. Stage 5.5A
verification: 320/320 tests, retaining all 245 Stage 5 cases; build has 0 warnings
and errors. Existing obstacle/target scores remain 82.67/90.00.

## Verification and limitations

Stage 5 builds both targets with 0 warnings/errors; all 245 tests pass, including
all 203 previous tests unchanged. New checks cover aspect/padding/Y inversion,
extreme ranges, exact/clamped/interpolated states, shortest angles, null metrics,
sample steps, elapsed-time playback, event windows, metric identity, scene reset,
and visual bounds. DispatcherTimer tests do not use fragile wall-clock sleeps.

The real WPF application was operated through its standard picker and desktop
accessibility controls; rendered screens were visually inspected. Verified:
obstacle import/result/View trajectory, path/route/zones/marker, Play/Pause,
seek, steps (0.5 -> 1.0 -> 0.5 s), Restart, all three speeds, end stop, events,
metrics, and normal/minimum-size resizing. TargetMission replay worked without
a route. Replacing an obstacle replay at 6.25 s with TargetMission reset time to
zero and removed the old route geometry.

A separate temporary 6000-sample synthetic session was imported/rendered/replayed
in the UI; the marker advanced and the process remained responsive. The service
also has a 6000-sample test. This is a practical check, not a hardware-independent
FPS guarantee. That temporary file is outside the repository, not a Stage 8 fixture.

Rendering approximates buffers near bends and varying radii. There is no video,
3D drone/terrain, zoom/pan editor, infinite red polygon, or saved replay state.
Missing telemetry coverage is not extrapolated. Current playback position stays
in memory; imported files remain unchanged. Stage 6 now stores sessions, telemetry,
versioned profiles and result/route snapshots in SQLite. History reconstructs this
same view without rescoring or the original file. The 1.1 mock also works from saved
data after a complete restart. See PERSISTENCE_HISTORY.md for verification and
schema. Stage 7 editing remains unimplemented.
