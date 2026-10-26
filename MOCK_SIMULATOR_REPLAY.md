# Simulator replay mock — Stage 5.5B

## Laboratory positioning

The prototype demonstrates the integration scenario with an external simulator
through a replay-capable session contract. For laboratory demonstration, the
simulator-side replay interface is represented by an interactive mock that uses
the actual imported telemetry, events and replay metadata.

Implemented: an interactive WPF Simulator Replay Prototype window inside
FPVTrainingAssistant. Planned: real DroneDiploma FPV/third-person 3D replay.
DroneDiploma is not modified or launched, and no Unreal libraries/assets are
included. Keeping the laboratory demonstration local avoids requiring a separate
simulator installation or changes to its gameplay. This window is explicitly
labeled Mock integration preview and Technical viewport prototype.

## Opening and architecture

1. Run `dotnet run --project FPVTrainingAssistant --framework net10.0-windows`.
2. Import replay_obstacle_course_v1_1.json or replay_target_mission_v1_1.json
   from the copied TestSessions directory.
3. On Session result select Open 3D Replay. View trajectory remains the separate
   Stage 5 analytical screen.

App.OnStartup explicitly injects MockSimulatorReplayLauncher through the existing
ISimulatorReplayLauncher. MainViewModel also defaults to a mock without a window
host for safe headless use; the WPF application supplies ReplayMockWindowService.
No executable path or environment variable is needed. The mock has no process
starter and never instantiates the external launcher. It receives ReplayLaunchRequest
with the actual current TrainingSession, matching ExerciseProfile, analysis and
optional runtime source path, rather than rereading or inventing a session.

ReplayCapability separates CanPreviewReplay from CanLaunchExternalSimulator
(the latter aliases the retained CanLaunchSimulatorReplay property). Preview
requires exactly contract 1.1, a neutral replay EnvironmentId and at least two
telemetry samples. Entity tracks, a physical source file and simulator executable
are not required for an already loaded preview. External-file/executable checks
remain independent future handoff requirements; their Limitations never block
mock mode. PreviewLimitations supplies the relevant in-app explanation.

No loaded result disables the action. Contract 1.0 remains valid for analysis
and 2D replay, but preview reports: "3D replay preview requires Contract 1.1 replay
context. Analysis and 2D trajectory remain available." A 1.1 session without a
valid replay environment or with insufficient telemetry cannot open a preview.
Malformed input is still rejected by existing import validation first.

ReplayMockWindowService holds at most one preview window. Reopening the same
imported session activates the existing window without resetting its camera,
time or panels. Opening a different imported session closes the previous window
and initializes a fresh one. Closing a preview while playing stops/detaches its
DispatcherTimer, clock and ViewModel subscription; the main window remains open.
Reopening after close starts paused at the first sample, FPV, 1x, both panels visible.
The mock has an independent playback controller and does not reset the main
trajectory's time or alter its session. The preview is owned by the main window,
so application shutdown also closes it.

## Real data and mocked visualization

| Element | Source / behavior |
| --- | --- |
| Session, exercise, source simulator | Actual TrainingSession values. |
| Environment/scenario | Actual ReplayContext; absent optional scenario displays N/A. |
| Sample/event/entity counts | Actual imported collections. |
| Clock, transforms, flight/sensor values | TelemetryReplayService at the selected recorded timestamp. |
| Dynamic entities | ReplayEntityTrack transforms and recorded states. |
| Event log | Actual SessionEvent data, including readable Value and metadata. |
| Environment viewport, reticle, drone shape, technical grid | WPF interface illustration; no map, real camera image, perspective world or 3D rendering. |

No random telemetry, artificial events, score calculations, AI or physics run
in the preview. Optional missing sensor values display N/A. Neither imported
collections nor source file bytes are changed by replay.

## Replay engine and telemetry

SimulatorReplayMockViewModel composes a separate TrajectoryViewModel instance,
reusing its TelemetryReplayService, playback commands, stepping, elapsed-time
advancement and timeline semantics. It does not implement a second drone replay
engine. The window is only a DispatcherTimer/Stopwatch adapter targeting about
30 Hz. Tests advance elapsed time directly, without sleeping or opening windows.

Bounds are the first and last telemetry timestamps, even when these differ from
zero and full session duration. Seek clamps to these bounds, pauses playback and
immediately updates telemetry, entities, minimap and event progress. Steps choose
actual previous/next sample timestamps and pause. Restart returns to the first
timestamp paused. Play stops at the final sample, without auto-loop. Speeds are
0.5x, 1x and 2x. Camera/panel/speed changes preserve time and playing state.

The existing position/attitude and percentage interpolation semantics remain
unchanged. ReplayState now additionally exposes the optional 1.1 telemetry.
Vertical speed, horizontal FOV, voltages, current, consumed capacity and RSSI
interpolate linearly only when both endpoints supply a value. Camera tilt uses
shortest-path angle interpolation. Exact samples retain their own available
values. Failsafe, armed and flight-mode values hold the earlier sample between
timestamps and switch at the exact next timestamp. No missing endpoint is
invented. Shared ReplayInterpolation implements the same normalized-angle
convention for drone and entity replay, including the negative direction for a
180-degree tie.

## FPV and Third Person

FPV shows a thin reticle and technical HUD: mode/armed, battery/pack volts,
control quality/RSSI, speed/altitude, position, orientation, body-relative camera
tilt and horizontal FOV. The scrollable Recorded telemetry panel also exposes
vertical speed, cell voltage, current, consumed mAh, failsafe and video quality.
These are actual replay values; the reticle has no target-detection logic.

Third Person replaces the reticle/HUD composition with a stylized top-view drone
and grid. The centered marker represents a follow view and rotates using actual
yaw; its actual X/Y/Z and pitch/roll/yaw remain visible. Environment/scenario
labels remain available above the viewport. No fake world geometry is generated.
Switching views does not restart or pause playback.

## Minimap and dynamic entities

The compact minimap shows the full recorded X/Y flight path, current drone
position/heading, the recommended route when the supplied profile has one, and
generic labeled markers for active replay entities. TargetMission works without
an obstacle-course route. It reuses TrajectoryProjectionService, preserves aspect
ratio and inverts Y for the screen. Bounds include the full recorded entity tracks
so marker movement does not cause map rescaling; resize rebuilds cached static
path drawing. This is an overview, without Stage 5 zone/metric cards.

Hide Minimap collapses its docked panel, giving the viewport the freed width.
Showing it restores the same replay time and state. Entity information lists the
actual track IDs and informational states; total track count appears in the status.

ReplayEntityService snapshots each validated ordered track and uses binary search.
Before its first sample the entity has no available transform and no marker.
Between samples X/Y/Z interpolate linearly and pitch/roll/yaw follow the shortest
normalized angular difference. At or after the final sample the final transform
and state are held. A one-sample track follows the same before/after rule.
IsActive is a discrete earlier-sample value between timestamps; explicitly false
suppresses the marker, while null means no explicit deactivation. Exact timestamps
use that sample's own state. Unknown State strings are shown without inventing
gameplay meaning, so a label such as destroyed does not itself deactivate an
entity unless IsActive is false. Seeking backwards restores earlier recorded states.

## Event log

The collapsible event panel contains all actual events in stable chronological
order. Each row shows elapsed time, event type, entity and readable Value/metadata.
Events at or before the replay time are marked Occurred; later ones are dimmed
and marked Upcoming. The latest occurred row is highlighted and marked Latest.
For simultaneous events the last row in stable imported order is selected. Seeking
backwards reverses these presentation flags without modifying recorded data.
Hide Event Log collapses the entire panel, freeing viewport height. Scrolling the
log and toggling visibility do not affect playback.

## Verification

`dotnet build FPVTrainingAssistant.sln` and `dotnet test FPVTrainingAssistant.sln`
pass with 0 build warnings/errors and 364/364 tests: all 320 previous cases plus
44 new cases. New coverage includes both fixtures, metadata, recorded bounds,
play/pause/restart/steps/seek/speeds/end behavior, camera/panel state preservation,
extended sensors, entity coverage/activity/interpolation, projected markers,
event chronology/progress/metadata, unavailable contexts and launcher boundaries.
Existing source scores remain obstacle 82.67 and target 90.00. No tests start a
real simulator or use pixel assertions.

The real WPF application was operated using its file picker, accessibility and
standard keyboard controls. The rendered obstacle preview was visually inspected.

| Demonstration | Observed result |
| --- | --- |
| replay_obstacle_course_v1_1.json | Correct session/environment/scenario, 21 samples, 4 events and 1 moving obstacle. FPV telemetry/reticle and Third Person switch work; camera changes preserved 3.25 s. |
| Controls | Play/Pause advanced actual time; seek worked; forward/back stepped 0 -> 0.5 -> 0 s. The selector accepted 0.5x/1x/2x. |
| Panels and lifetime | Minimap path/route/drone/entity displayed. Hide/show minimap and event log preserved time. Latest collision highlighted at 6.25 s. Reopening activated one existing window and retained 6.25 s; closing while playing returned safely to the main app. |
| replay_target_mission_v1_1.json | Correct target scenario, 21 samples, 3 target events and target-01. At 8 s TargetDamageZone was Latest with damageZone: central. No route requirement blocked the preview. |
| valid_obstacle_course.json (1.0) | Analysis score 82.67; preview gave the explicit Contract 1.1 context limitation. The local trajectory and playback controls remained available. |

No DroneDiploma process was started during development or verification.

## Future replacement and limitations

Keep the current mock binding for laboratory use. A future production build can
inject the preserved SimulatorReplayLauncher through ISimulatorReplayLauncher,
using CanLaunchExternalSimulator and the runtime source path instead of preview
requirements. Its existing file/executable checks and ArgumentList protocol remain:

```text
DroneDiploma.exe -ReplayMode -ReplayFile="C:\sessions\session replay.json"
```

The external implementation uses FPVTRAINING_SIMULATOR_EXE; mock mode ignores it.
The separate simulator still needs CLI/file validation, neutral environment and
visual lookup, recorded transforms instead of gameplay AI/physics, real FPV with
camera tilt/FOV, third-person rendering, minimap, event log and synchronized
timeline. None of those capabilities is claimed for the current DroneDiploma build.
See REPLAY_INTEGRATION.md for the retained future protocol and conceptual mapping.

The mock has no static environment rendering, video, real camera projection,
collision simulation, AI or profile editor. It demonstrates integration workflow
using recorded evidence. Its selected replay position/window state stays in memory.

Stage 6 adds SQLite history for the actual session/profile/analysis and replay data.
Opening a saved 1.1 session through History reconstructs the existing preview without
its original JSON path, including optional telemetry, events, context and entity
tracks. Both 2D trajectory and this mock were verified after a complete application
shutdown/restart, with 395/395 tests passing. Source-file reconstruction for future
external handoff and Stage 7 editing remain unimplemented. See PERSISTENCE_HISTORY.md.
