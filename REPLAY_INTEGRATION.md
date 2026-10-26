# Replay integration — Stage 5.5A

## Architecture and delivery boundary

One neutral JSON file supplies two consumers:

```text
External simulator or prepared fixture -> session/replay JSON
    +-> FPVTrainingAssistant: validation, scores, evidence, recommendations, 2D replay
    +-> future DroneDiploma consumer: environment + recorded transforms -> 3D replay
```

Stage 5.5A delivers contract 1.1, validation, capability checks, assistant-side
process launch and the Open 3D Replay action. The assistant remains independent
of Unreal Engine. It imports no Unreal code/assets and renders no 3D environment.
No changes to DroneDiploma, IPC, network, SQLite or Settings UI are included.
The current DroneDiploma build is **not claimed to export this JSON or implement
the replay launch protocol**. Simulator-side work is Stage 5.5B below.

## Supported contracts and recorded data

[SESSION_CONTRACT.md](SESSION_CONTRACT.md) specifies both versions in detail.
Version 1.0 remains valid for analysis and local 2D replay. Version 1.1 extends
it with optional telemetry and nullable replay context; declaring 1.1 does not
require every optional sensor or even replay context. Both are accepted explicitly;
other versions, including 2.0, are rejected. Missing context limits 3D capability
without invalidating an otherwise valid analytical session.

New nullable telemetry fields are:

| Field | Semantics |
| --- | --- |
| VerticalSpeedMps | m/s, positive up and negative down. |
| CameraTiltDeg | Body-relative camera pitch offset, positive nose-up, degrees normalized to [-180, 180) on assistant import. |
| CameraFovDeg | Horizontal FOV in degrees, 0 < FOV < 180. |
| BatteryVoltageV | Full pack volts, nonnegative. |
| CellVoltageV | Representative/per-cell volts, nonnegative. |
| CurrentA | Signed amperes, including negative values. |
| ConsumedMah | Consumed milliamp-hours, nonnegative. |
| ControlRssiDbm | Control radio RSSI in dBm. |
| Failsafe | Optional control-link failsafe boolean. |
| FlightMode | Open neutral string; unknown future modes accepted. |
| Armed | Optional armed boolean. |

Every supplied numeric value must be finite. Omitted/null means unavailable,
never a fabricated zero. Existing required telemetry and optional percentage
validation are unchanged. Additional telemetry, replay context and entity tracks
do not enter scoring or recommendation formulas. The current 2D replay continues
to use its original interpolated state; it does not visualize dynamic entities
or expose the new sensors as additional UI metrics.

## Neutral environment and dynamic entities

ReplayContext contains required nonblank EnvironmentId, optional ScenarioId and
a required nonnull Entities array. Static scenes may have Entities = []. Names
such as synthetic-training-ground-01 and obstacle-course-basic are logical IDs.
EnvironmentId, ScenarioId and ArchetypeId exclude path separators, colons and
quotes. Unreal `/Game/Maps/...` and Blueprint object paths are never contract IDs.
The future simulator owns the lookup from these names to its local maps/visuals.
No complete static world or third-party assets are embedded in the file.

Each ReplayEntityTrack has a unique nonblank EntityId, nonblank open EntityType,
optional neutral ArchetypeId, nonempty Samples and optional string metadata.
Examples of types are vehicle, infantry, target, objective and movingObstacle;
the assistant has no closed enum or gameplay interpretation. Unknown archetypes
are also valid logical names. Relevant events can share the track's EntityId.

ReplayEntitySample records TimeSec, X/Y/Z and PitchDeg/RollDeg/YawDeg, plus optional
IsActive, State and string metadata. Times are finite, nonnegative, strictly
increasing within a track and within session duration plus 0.000001 seconds.
All transforms are finite. Samples use the drone's frame, units and elapsed clock.
Unknown State strings remain valid; IsActive may describe presence, removal or
inactivity. Different tracks can have different sampling rates and partial coverage.

Rerunning AI is insufficient: a vehicle observed at position A at original time
30 might move elsewhere if gameplay runs again. The future replay must disable
normal gameplay AI and physics that would move replay actors, applying recorded
drone/entity transforms and discrete states instead. This is deterministic relative
to the recorded evidence, not a reconstruction of unrecorded simulation state.
Video is unnecessary because transforms reconstruct viewpoints in the simulator's
existing environment; no video capture, encoding or playback is introduced here.

## Capability and source-file lifetime

ReplayCapabilityService evaluates a validated session plus local runtime paths:

| Result | Rule |
| --- | --- |
| CanReplayDrone | At least two telemetry samples. |
| HasEnvironment | Replay context with nonblank neutral EnvironmentId. |
| HasDynamicEntityTracks | At least one track satisfying track/sample validation. |
| CanLaunchSimulatorReplay | Drone data and environment, existing source file, and configured existing executable. Dynamic tracks are optional. |
| Limitations | Read-only reasons for missing drone data, environment, file or configuration. |

Successful import stores the normalized absolute source file path on
SessionImportResult.SourceFilePath and MainViewModel.SourceFilePath. It is never
embedded in TrainingSession JSON. New imports reset this path, capability and
launch status; failed imports expose no launchable result. A cancelled picker
preserves the current review. Availability is checked again when the action runs,
so a removed file or executable is handled without losing analysis/2D playback.

The launcher passes the original file and never rewrites it. Assistant angle
normalization/event sorting occur only in memory. A future simulator consumer
must independently parse, validate and normalize the physical input file, rather
than assume it contains the assistant's normalized in-memory representation.
File existence is local availability, not proof that a particular simulator build
understands an environment, archetype or protocol. Source files must remain
accessible; no private copy, hash snapshot or persistence is added in this stage.

## Launch protocol and local configuration

Set FPVTRAINING_SIMULATOR_EXE to the full executable path before starting the
assistant. For example, in PowerShell for the current terminal and its children:

```powershell
$env:FPVTRAINING_SIMULATOR_EXE = 'C:\Simulators\DroneDiploma\DroneDiploma.exe'
dotnet run --project FPVTrainingAssistant --framework net10.0-windows
```

Use the executable path as the value, without embedded quote characters or command
arguments. Restart an already running assistant to inherit changed environment
variables. This repository does not set or persist the variable. Stage 7 may
eventually add a Settings UI.

The protocol is:

```text
DroneDiploma.exe -ReplayMode -ReplayFile="C:\sessions\session replay.json"
```

SimulatorReplayLauncher reads configuration on each call, checks executable and
source file existence, resolves absolute paths and builds ProcessStartInfo with
UseShellExecute = false. ArgumentList has exactly two entries: `-ReplayMode` and
`-ReplayFile=<absolute path>`. The second entry is one argument even with spaces;
literal quote characters are not manually inserted into it. WorkingDirectory is
the executable directory. No shell, elevation, admin verb, IPC or network is used.
IReplayProcessStarter isolates actual Process.Start from tests. Exceptions or a
failed process start return a friendly result. Starting a process reports handoff,
not confirmation that the simulator successfully loaded a replay.

## Instructor-facing action

Session result contains separate View trajectory and Open 3D Replay buttons.
The first opens the existing local 2D screen. The second is unavailable before a
result is loaded and during import. Once a result exists it can be clicked to
explain limitations, even if launch capability is false. In-app status shows:

- Missing environment: "This session does not contain the environment information required for 3D replay."
- Missing configuration: "Simulator executable is not configured."
- Missing/deleted source: "The source replay file is no longer available."
- Removed configured executable: "The configured simulator executable is no longer available."

When several prerequisites are missing, the first limitation is shown (drone data,
environment, source, then executable configuration/availability). The full list
remains on ReplayCapability. A launch attempt preserves the current result; local
playback is paused before starting an external simulator. Errors remain nonfatal.

## Conceptual DroneDiploma exporter mapping (future)

This table is an integration design, **not an existing export implementation**.
Source units, signs and coordinate frames must be verified in the simulator.

| Conceptual source | Neutral contract |
| --- | --- |
| FlightTime | TimeSec, seconds elapsed from this session start. |
| GetActorLocation.X / 100 | X meters. |
| GetActorLocation.Y / 100 | Y meters. |
| GetActorLocation.Z / 100 | Z meters. |
| Speed | SpeedMps after conversion from actual source units to m/s. |
| VerticalSpeed | VerticalSpeedMps after unit/sign conversion, positive up. |
| Barometric/relative altitude | AltitudeM in meters with declared local datum. |
| Pitch, Roll, Yaw | PitchDeg, RollDeg, YawDeg after axis/sign conversion. |
| Camera tilt, camera FOV | Body-relative CameraTiltDeg and horizontal CameraFovDeg. |
| Pack volts, cell volts, current, consumed capacity | BatteryVoltageV, CellVoltageV, CurrentA, ConsumedMah. |
| Derived battery percentage when available | BatteryPercent, 0..100. |
| Control LQ, RSSI, failsafe | ControlLinkQualityPercent, ControlRssiDbm, Failsafe. |
| Video quality | VideoLinkQualityPercent. |
| Flight mode, armed state | FlightMode, Armed. |

The location rows express centimeter-to-meter scaling in an aligned local frame;
they do not waive axis/origin conversion. Contract X/Y are horizontal, Z is up,
pitch is positive nose-up, roll positive rightward, yaw zero is +X and positive
yaw turns toward +Y. Exporters must align all transforms, static environment
registration and reference-route coordinates. Source speed units are not assumed
to already be m/s. Simulator rotations must not be blindly copied with an
unverified handedness or sign. Convert source vertical FOV if necessary to the
contract's horizontal FOV. Missing values stay absent/null.

## Exact remaining Stage 5.5B responsibilities in DroneDiploma

Future work belongs in the separate simulator repository and is not performed here:

1. Parse `-ReplayMode` and the single `-ReplayFile=<absolute path>` argument;
   load and independently validate supported 1.0/1.1 input, reporting unsupported
   versions, missing replay environment, unknown environment mappings or bad data.
2. Implement neutral EnvironmentId/ScenarioId lookup and environment registration;
   map optional ArchetypeId and EntityType to local replay representations, with
   a clear fallback/error policy for unknown logical names. Do not require engine
   asset paths in session JSON.
3. Create a replay drone and replay entity actors, disable gameplay AI/physics
   as transform drivers, and apply recorded transforms/IsActive/State. Use linear
   position interpolation and the same shortest-path normalized angle convention
   as the assistant, including negative direction for an exact 180-degree tie.
   Treat discrete states as recorded state changes, not interpolated numbers.
   Define visible coverage/default behavior for absent camera data, single-sample
   tracks and partial entity tracks without fabricating AI motion.
4. Implement FPV camera using recorded drone attitude plus body-local camera tilt
   and horizontal FOV when available, and an external third-person follow camera.
5. Implement a show/hide minimap for drone, route where available and relevant
   event/entity markers, plus a show/hide chronological event log with stable
   ordering of simultaneous events.
6. Implement synchronized play/pause/seek and 0.5x/1x/2x timeline for drone, dynamic
   entities, camera, events and minimap; test seeks, state changes and end bounds.
7. Test the command protocol with both synthetic 1.1 fixtures, compare transforms
   and clock behavior with the assistant, and verify graceful failures and normal
   simulator mode isolation. Add a source exporter only as separately authorized
   integration work after verifying actual simulator data/units.

Route geometry currently resides in the assistant's matching in-memory exercise
profile, not in the session JSON. A future simulator minimap can show a route only
when its own compatible profile/scenario catalog supplies that geometry. This
stage does not add a route snapshot or invent environment/archetype assets.

## Synthetic examples, verification and current limits

The two new files are replay_obstacle_course_v1_1.json and
replay_target_mission_v1_1.json in FPVTrainingAssistant/TestSessions. Each has 21
drone samples over 0..10 seconds, the original exercise evidence and profile IDs,
all new telemetry fields, an explicit synthetic source and one six-sample moving
entity. The target track uses target-01, matching its events. The environment and
visual IDs are synthetic; they need future simulator mappings. Existing 1.0 files
are retained. Scores remain obstacle 82.67 and target 90.00, simulator score 750.

Verification: `dotnet build FPVTrainingAssistant.sln` has 0 warnings/errors;
`dotnet test FPVTrainingAssistant.sln` passes 320/320 tests (245 existing, 75 new).
Tests cover versions, strict extension types/ranges/finiteness, track identity,
time/transform validation, score/evidence invariance, capabilities, source lifetime,
safe arguments with spaces, unchanged source bytes and recoverable UI commands.
Process starts use test doubles; no actual DroneDiploma launch occurs in tests.

The real Windows WPF application was also checked through its standard file
picker and accessibility controls, without pixel assertions. The 1.1 obstacle
fixture showed 82.67 and an enabled Open 3D Replay action; with configuration
absent it showed the exact missing-configuration message in-app. The unchanged
1.0 target fixture showed 90.00 and the exact missing-environment message.
View trajectory still opened the local screen with Play, Restart and sample-step
controls. No external simulator was launched during this check.

The assistant cannot prove that a configured simulator understands the protocol.
Actual FPV/third-person 3D replay, entity rendering, minimap and simulator event
log/timeline remain Stage 5.5B. The assistant retains only the current session in
memory; Stage 6 persistence/history and Stage 7 editing remain future work.
