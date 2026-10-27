# Settings and exercise profile configuration — Stage 7

## Scope and entry points

Settings opens independently of the current session after SQLite initialization.
The screen uses the existing dark WPF shell and has a version selector, general
profile fields, Evaluation criteria and Reference route panels, and Application /
Replay preferences. No extra package, graphical map editor, score editor or
automatic historical reanalysis is introduced. DroneDiploma remains unchanged.

App constructs ExerciseProfileRepository and ApplicationSettingsService, injects
the repository into ExerciseProfileProvider, and supplies both to SettingsViewModel.
SQL lives in Data; SettingsViewModel owns commands and status; ProfileEditorViewModel,
CriterionEditorViewModel and ReferencePointEditor own detached presentation fields.
SettingsView code-behind only initializes the view. Existing result and history
screens remain shared rather than gaining separate configuration editors.

## Working copies and immutable versions

The selector lists stored names, logical IDs, exercise types, versions and active
states. ProfileId is read-only. Name and supported exercise type can be changed in
the working copy. Domain strings remain open, while this UI offers ObstacleCourse
and TargetMission, the exercise types the analyzer currently supports.

Editing changes only the working copy. Save as New Version validates it, checks
the latest stored version observed by the editor and writes version MAX(version)+1.
Profile, criteria and route points are inserted in one transaction. Existing rows
are never overwritten. A concurrent save that advances the version returns a clear
Conflict message and retains the draft; Refresh profiles updates the version list
without discarding the draft, allowing a reviewed retry to create the next version.
Insert failure rolls back both child records and any requested activation change.

Reset Changes restores the selected stored version without writing to SQLite.
Selecting another version while dirty keeps the current editor and shows explicit
Discard and switch / Keep editing actions. Navigation away retains the in-memory
working copy; returning or refreshing does not silently replace dirty fields.
Unsaved profile changes are not retained across application shutdown.

The optional Make the new version active after saving checkbox defaults off.
Activate selected version selects an already stored configuration. Each activation
deactivates all other versions of that logical ProfileId inside the same transaction,
without affecting a different profile family. Activation is unavailable while the
selected draft has unsaved edits, avoiding confusion with unsaved configuration.

IsActive is operational selection metadata and may change on stored versions.
Names, exercise type, criteria, weights, parameters and reference points of an
existing version remain immutable. Historical session/result rows are never updated
by activation or version creation. SessionRepository's snapshot comparison excludes
only IsActive so a harmless activation change cannot block an otherwise identical
session save; configuration mismatches for the same version still fail.

## Exact profile resolution and history

When storage is available, ExerciseProfileProvider.ResolveAsync loads the exact
ProfileId + ProfileVersion and checks ExerciseType. It does not choose the newest
or active version in place of an explicit session version. A file requesting v1
continues to use v1 after v2 becomes active; a file requesting v2 uses the persisted
v2; a missing v3 remains unavailable. The active state tells instructors which
version should normally be used for future compatible data, but does not rewrite
the versioned JSON contract. Built-in v1 seed profiles are retained and initialization
does not overwrite them. If storage is unavailable the existing built-in v1
fallback remains available for non-persistent analysis, without inventing v2 data.

SessionAnalyzer.Analyze retains its original active-profile guard. The import
workflow now uses AnalyzeRequestedVersion, which permits an explicitly matched
inactive version while retaining identity/type/configuration validation. Both entry
points share exactly the same calculation body. No criterion formulas, default
penalties, route calculations or recommendation rules changed.

History reads its saved SessionAnalysisResult and exact versioned configuration.
It never invokes either analysis entry point to reopen old results. A selected
version becoming inactive does not change its stored score, measured values,
explanations, mistakes, recommendations or route metrics. Result already shows
the profile identity/version read-only. Reanalysis remains a future explicit feature.

## Criteria, weights and typed parameters

Each criterion shows name, description, immutable RuleType, Enabled, stored Weight
and normalized contribution. RuleType is not an arbitrary text field. Weights need
not sum to 1 or 100 and are never normalized back into storage: 40/30/20/10 and
4/3/2/1 produce the same 40%/30%/20%/10% display. Scaling by the largest enabled
weight avoids overflow in the presentation, matching the existing analyzer convention.

The editor presents named numeric fields rather than parameter JSON:

| Rule | Fields |
| --- | --- |
| CheckpointAccuracy | Enabled and weight. |
| CollisionAvoidance | Penalty per collision -> PenaltyPerCollision. |
| RouteAdherence | Yellow-zone contribution -> YellowZoneFactor. |
| CompletionTime | Target time / maximum time -> TargetTimeSec / MaximumTimeSec. |
| TargetIdentification, TargetHit, MissionSuccess | Enabled and weight. |
| TargetDamage | Critical, engine, weapon, track, hull and default damage scores -> the six DamageScore_* parameters. |

Blank parameter fields remove that explicit override and show the documented
engine fallback as a hint. Completion times are required instead; blank values
cannot form a valid enabled CompletionTime criterion. Existing unknown parameter keys are preserved. Numeric
fields use English decimal-point syntax; raw invalid input stays visible and is
rejected on save rather than silently retaining an older value. Current rules use
named parameters, not the generic Threshold. An existing non-null threshold is
retained/editable and labeled as unused by the current rule; no new threshold-based
scoring is claimed.

ExerciseProfileConfigurationValidator delegates criterion semantics to the existing
AnalysisProfileValidator. Enabled weights must be finite and positive, disabled
weights nonnegative, at least one criterion enabled, and rule/exercise compatibility
must hold. Collision penalty is nonnegative, YellowZoneFactor is 0..1, completion
target > 0 and maximum > target, and enabled damage-category values are 0..100.
Disabled rule-specific ranges follow the existing analyzer policy; enabling such a
criterion validates its parameters before saving. Clear issue text appears in-app.
Repository methods also validate, so bypassing the ViewModel cannot store an invalid
supported configuration through the settings API.

## Reference route table

The table edits PointType, X/Y/Z and Green/Yellow radii. Sequence is read-only.
Add point creates an explicit editable default row; Remove point deletes only the
working row. Move Up / Move Down and additions/removals rebuild SequenceNo as
0..N-1. An unsaved table change cannot alter older versions or recorded trajectories.

ReferencePoint's existing constructor enforces GreenRadiusM >= 0 and
YellowRadiusM >= GreenRadiusM. Coordinates must be finite; point types must be
nonblank and sequences unique. The shared RouteAnalyzer geometry validation rejects
an unusable enabled RouteAdherence route. Two or more supplied points must also
form a usable polyline for the analyzer's optional route metrics, even without a
route scoring criterion. A target profile may omit route points entirely.
There is no dragging, GIS, terrain editing or 3D route editor.

## Application and replay preferences

ApplicationSettings is a strongly typed immutable snapshot:

- DefaultReplaySpeed: 0.5x, 1x or 2x; fresh default 1x.
- ShowMinimapByDefault: fresh default true.
- ShowEventLogByDefault: fresh default true.

Save application settings is explicit and transactional. ApplicationSettingsService
validates the speed and retains a valid Current snapshot; failures return friendly
messages. Unsaved preference fields survive a Settings refresh. Successful saves
update SQLite and the in-process snapshot; recreation/restart reloads stored values.

MainViewModel applies the speed when constructing a new trajectory. The mock window
adapter supplies a settings snapshot to each newly created mock ViewModel, including
panel visibility. Saving defaults does not bind to or mutate existing replay
controllers, pause them, reset their time or change an already open window's panels.
Activating an existing mock window preserves its state. Closing/reopening it creates
a new controller with current defaults. Normal Stage 5 navigation-away pause behavior
is unchanged.

## Database schema 1 -> 2

Stage 6 used schema_version=1. Stage 7 accepts existing v1 or current v2 and creates
app_settings(key TEXT PRIMARY KEY, value TEXT NOT NULL), adding three default keys
only when absent. It updates app_metadata.schema_version to "2" in the same schema
transaction. No existing database or session table is dropped/recreated; no user
must delete assistant.db. The database now has sixteen application tables.

Repeated initialization preserves preferences and all profile versions/activation
flags. Existing operators, sessions, telemetry, results, replay data and history
remain intact. Other unsupported schema versions are refused without resetting
the database. The existing short-lived connections, parameterized SQL, foreign
keys, WAL and worker-thread SQLite operations remain in use. No new NuGet package
or migration framework is added.

## Verification

`dotnet build FPVTrainingAssistant.sln`: 0 warnings/errors.
`dotnet test FPVTrainingAssistant.sln`: 436/436 tests, retaining all 395 previous
cases and adding 41 settings cases. The previous fresh-schema assertion now expects
2; an explicit v1 migration case verifies older data instead of removing coverage.

New checks cover immutable v1 criteria/routes/results, new/active versions,
concurrent conflict and transaction rollback, exact v1/v2 resolution and unchanged
baseline scores, typed parameters/weights, route editing and validation, unsaved
switch/reset/refresh, preferences/service recreation and new-versus-running replay
behavior. Temporary databases are isolated from the actual application database.

The real WPF application and existing Stage 6 database were also checked:

| Scenario | Observed result |
| --- | --- |
| Migration | Existing schema 1 with six historical sessions upgraded to schema 2; all six retained their IDs, scores, telemetry, events, result records and replay data. Foreign-key check stayed clear. |
| Obstacle editor | Loaded v1, set collision penalty 40, checkpoint weight 50 and first green radius 2; saved v2 and activated it. After restart v2 stayed active with those values; v1 retained weight 40, default collision penalty 20 and radius 1.5. |
| Target editor | Changed Other category score to 60 and created target v2; target v1 remained available and active. |
| Unsaved workflow | Attempted a profile switch with a temporary unsaved name, saw the discard/keep prompt, kept editing and Reset Changes restored the stored name without a DB change. |
| Replay defaults/restart | Saved 2x with minimap/event-log defaults off, closed the process and reopened it. Historical trajectory opened at 2x; a new historical mock opened at 2x with Show Minimap / Show Event Log actions, confirming both panels initially hidden. |
| Historical result | The existing target v1 reopened as a saved snapshot at 90.00; trajectory and mock stayed available. A v1 obstacle import still resolved v1 and displayed 82.67 after obstacle v2 activation. |
| Explicit v2 import | A temporary synthetic session requesting obstacle v2 evaluated to 77.58 under the edited configuration and saved against v2. No permanent Stage 8 fixture was added. |

Read-only inspection after the additional v2 import found schema 2, seven valid
session/profile/operator joins, 147 telemetry rows, 26 events, 28 criterion results,
six replay contexts/tracks, 36 entity samples and zero foreign-key violations.
Both old profile configurations and all six original session scores were retained.
There are two versions per seeded profile family and one active version per family.
Temporary picker/inspection/capture files and runtime SQLite data remain outside Git.

## Remaining work

Stages 7–9 are complete: final fixtures and laboratory coverage are in
DEMO_SCENARIOS.md, and final usability/resource evidence is in QUALITY_REVIEW.md.
Stage 9 adds friendly criterion/damage labels, required completion-time hints,
concise validation, two-decimal percentage contributions and shared dark controls;
internal parameter keys, stored weights and scoring semantics remain unchanged.
Profile-family creation, adding/removing criterion rules,
graphical route editing, automatic reanalysis, manual score editing, history deletion,
external simulator replay, cloud and authentication are not implemented. This editor
configures the current profiles/rules and creates their new immutable versions.
