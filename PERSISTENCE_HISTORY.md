# Persistence, operators, history and progress — Stage 6

## Provider and location

The sole added direct NuGet dependency is
[Microsoft.Data.Sqlite 10.0.14](https://www.nuget.org/packages/Microsoft.Data.Sqlite/10.0.14).
It supplies SQLite through parameterized ADO.NET SQL; no Entity Framework, Dapper,
remote server or cloud is used. Its native SQLite dependencies are transitive.

ApplicationDataPathProvider resolves the per-user path using
Environment.SpecialFolder.LocalApplicationData:

```text
%LOCALAPPDATA%\FPVTrainingAssistant\assistant.db
```

The initializer creates the directory automatically. Runtime data is outside the
repository; .gitignore additionally excludes *.db, *.db-wal, *.db-shm and
*.db-journal. The application does not hard-code a developer path. Tests inject
isolated temporary directories and never open the real application database.
Normal development verification leaves its explicitly synthetic demo history in
the per-user database so it can be reopened in the application.

DatabaseConnectionFactory opens short-lived connections, enables foreign keys on
each connection and disables connection pooling so closed operations release their
handles. Transactions use one connection. Initialization enables WAL after schema
creation/seeding commits; SQLite manages its checkpoint and companion files.
Do not remove a live database's WAL/SHM files. No long-lived global connection exists.

## Initialization and schema version

App shows its main window, initializes storage off the UI thread, then enables
History. Initialization creates version-1 tables and seeds the exact built-in
synthetic-obstacle-course/v1 and synthetic-target-mission/v1 profiles if absent.
Reference points, ordered criteria and parameters are included. An existing exact
profile/version is never overwritten during seeding.

app_metadata contains schema_version = "1". Repeated initialization is idempotent.
A populated database without version metadata or with another schema version is
refused clearly, without replacing its contents. There is no migration framework,
automatic reset or destructive recovery. Future upgrades must define migrations
explicitly before accepting another version.

Initialization failure leaves import, validation, analysis and local replay usable.
History stays disabled and a visible in-app message explains that sessions cannot
be saved. Technical initialization/save details use Trace; normal UI messages do
not expose raw SQL exceptions or stack traces.

## Relational schema

There are 15 application tables (plus SQLite's internal sequence table):

| Table | Identity and purpose |
| --- | --- |
| app_metadata | key; schema version. |
| operators | operator_id; optional display_name/notes, created/updated UTC timestamps. |
| exercise_profiles | (profile_id, version); immutable historical profile identity, name/type/active flag. |
| reference_points | (profile_id, profile_version, sequence_no); ordered route positions/types/radii. |
| evaluation_criteria | (profile_id, profile_version, criterion_id); ordered names/descriptions/weights/rules/enabled/threshold/parameters_json. |
| training_sessions | session_id; identities, contract, UTC times, source/outcome, independent scores, analysis status/summary, aggregate metrics and creation time. |
| telemetry_samples | (session_id, time_sec); all original and nullable 1.1 telemetry columns. |
| session_events | (session_id, event_id); stable ordinal, time/type/entity/value/metadata_json. |
| evaluation_results | result_id; session, versioned criterion FK, ordinal, measured value, score, explanation. |
| recommendations | recommendation_id; session, ordinal, category/text/priority/evidence. |
| detected_mistakes | local integer id; session, ordinal, code/category/description/time/entity/evidence; repeated codes allowed. |
| route_analysis | session_id; green/yellow/red percentages, average/maximum deviation, evaluated time; optional. |
| replay_contexts | session_id; environment/scenario, optional for analytical sessions. |
| replay_entity_tracks | (session_id, entity_id); ordinal, open type/archetype and metadata_json. |
| replay_entity_samples | (session_id, entity_id, time_sec); position/angles/nullable active/state/metadata_json. |

Foreign keys connect operators, versioned profiles, sessions and their children.
Evaluation results additionally reference a criterion in the same profile version
as their session. Session-owned child relationships cascade on explicit future
deletion; no deletion API/UI is added. An index supports operator/time history.

An ordinal preserves criteria/results/recommendations/mistakes/track order.
Events load by time then original ordinal so simultaneous events remain stable.
Telemetry/entity samples load chronologically with strict timestamp uniqueness.
All source/domain IDs remain strings; database-local mistake IDs do not change
the public DetectedMistake model.

Extensible metadata and criterion parameters use System.Text.Json with the existing
SessionJson options, preserving case-sensitive dictionary keys and null versus
empty dictionaries. Aggregate ExerciseMetrics is stored as metrics_json to preserve
its measured-count snapshot, including damage-zone labels, without recalculating
it or adding tables for every aggregate count. This is not a JSON replacement for
the relational session, telemetry, event or result tables.

Absolute timestamps are stored as round-trip UTC ISO 8601; history displays local
date/time. Nullable booleans preserve NULL, 0 and 1, including success, failsafe,
armed and entity isActive. Supplied optional 1.1 telemetry, unknown events/types/
archetypes/states and metadata all round-trip.

## Score ownership and historical snapshots

training_sessions.simulator_score retains the source-owned value on its own scale.
assistant_final_score stores SessionAnalysisResult.FinalScore on 0..100.
imported_assistant_score separately preserves the original nullable
TrainingSession.AssistantFinalScore supplied by the importer; these values must
not be silently conflated. Loaded TrainingSession reproduces that original value,
while the result screen uses the saved SessionAnalysisResult.FinalScore.

StoredSession groups TrainingSession, its exact ExerciseProfile and the complete
SessionAnalysisResult. SessionRepository reconstructs these directly from storage.
Opening history does not invoke SessionAnalyzer, criterion scoring, route analysis,
mistake detection or recommendation generation. Saved measured values, explanations,
recommendations, mistakes, route metrics, summary and aggregate metrics are the
authoritative snapshot. Replay reconstructs transforms, not scores.

Profile edits in a future Stage 7 must create a new version. Saving against an
existing same version with a different configuration fails clearly rather than
changing history. A genuinely new profile version can be saved as its own snapshot;
Stage 6 does not offer profile/version editing controls.

## Automatic save, duplicates and atomicity

The normal workflow is Import -> Validate -> Resolve profile -> Analyze -> Save ->
Display result. Validation/analysis behavior is unchanged. SessionRepository saves
the operator if absent, required profile snapshot, session and every child record
inside one transaction. All SQL values are parameters. Telemetry uses one prepared
insert statement per save. Any failure rolls the transaction back, including newly
created operators/profiles. No partial session is left behind.

SessionId is globally unique. An existing ID returns Duplicate without an exception,
overwrite or telemetry merge. Concurrent same-ID saves serialize on the write
transaction; one saves and the other reports Duplicate. Operator metadata is not
overwritten by importing another session with the same OperatorId.

SessionSaveResult exposes Saved, Duplicate or Failed plus a user-facing message:

- Saved: "Session saved to history."
- Duplicate: "This session already exists in history and was not saved again."
- Failure: "The session was analyzed but could not be saved to history."

Duplicate/save failure leaves the newly analyzed result and replay available in
memory. There is no manual Save button for ordinary imports. Retry behavior is to
import again; already saved IDs are never silently replaced.

## Data access and responsiveness

DatabaseInitializer owns initialization, ExerciseProfileRepository owns versioned
profile mapping, SessionRepository owns atomic save/full reconstruction, and
HistoryRepository owns operator listings/metadata and session summaries. SQL stays
out of ViewModels. SqliteData is a small internal command/parameter/reader helper,
not a generic ORM or migration system.

Microsoft.Data.Sqlite's async methods execute synchronously because SQLite has no
asynchronous I/O; see [Microsoft's async limitations](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/async).
Therefore each repository operation runs its complete synchronous connection/
transaction workload on a worker using Task.Run, rather than blocking WPF or
wrapping individual genuinely async calls. Loading/busy states prevent conflicting
commands. History session requests use a generation counter so a slower previous
operator/filter response cannot replace a newer selection.

## History, operators and progress

History is available immediately after storage initialization, without a current
import. The operator selector uses actual identities, optional names, session
counts, last local date and average stored score. OperatorId is read-only; the
Operator details expander edits only optional DisplayName and Notes. Save operator
details updates those fields and updated_at_utc, then refreshes the selected label.
It does not create authentication accounts or require personal information.

Selecting an operator loads only that operator's sessions, newest first with a
deterministic SessionId tie-break. All exercises/ObstacleCourse/TargetMission
filters update the table, summary and chart. Rows show local date/time, exercise,
session ID, duration, independent scores and outcome. Open Result activates the
existing result screen with a subtle Historical session label. Trajectory uses
stored telemetry; Contract 1.1 mock replay uses stored context/entity tracks.

For the selected operator and exercise filter, the summary shows session count,
average, best and latest assistant scores. The native WPF progress chart uses
chronological session order on X (date labels at the ends) and a fixed 0..100 Y
range, with points and connecting line. It handles zero, one and multiple points.
These are descriptive statistics, not new evaluation criteria. A single point is
centered; empty history has a clear message instead of an empty table/chart.

History refreshes whenever it is opened and after metadata updates. New imports
are visible without restarting. Read/update failures show concise recoverable
messages. Settings stays disabled; no score editing or session deletion is added.

## Source path and replay

The original JSON absolute source path is never stored in the database. Opening
history explicitly sets MainViewModel.SourceFilePath to null and populates the
same active session/profile/analysis state used by an import. The existing mock
preview needs only loaded 1.1 replay data, so both replay views work without the
original file or simulator installation.

The preserved future external SimulatorReplayLauncher needs a physical replay file.
A database-loaded session may eventually need explicit export/reconstruction of a
contract file before external handoff. That export is not implemented in Stage 6.
DroneDiploma remains unchanged and unlaunched.

## Verification and demonstration

Build: 0 warnings/errors. Tests: 395/395, retaining all 364 earlier cases and adding
31 persistence/workflow cases. Every database test creates/removes an isolated
temporary database. Coverage includes schema/version/FKs/idempotent seeding,
both contract versions/full snapshots, all extended telemetry/replay data,
null booleans/dictionaries/open strings, independent scores, duplicate/concurrent
saves, controlled late-trigger rollback, profile conflict/new version, operator
metadata, history/filter/statistics/progress, automatic save/fallback and reopening
with an analyzer that throws if called.

Three new synthetic fixtures history_progress_01/02/03.json use one operator at
different UTC dates. Their events legitimately produce 56.00, 82.67 and 100.00;
AssistantFinalScore remains null on import. Existing obstacle/replay baselines
stay 82.67 and target baselines stay 90.00. No scoring formula or existing fixture
was changed.

The actual Windows WPF application was checked with its picker/accessibility/
keyboard controls; History and the native chart were visually inspected:

| Scenario | Observed result |
| --- | --- |
| Fresh storage | No database existed; startup created it, enabled History and showed No training history is available yet. |
| Contract 1.0 | Imported/analyzed/saved, listed in History, reopened as Historical session with 82.67, and opened persisted 2D trajectory. |
| Contract 1.1 and complete restart | Saved replay obstacle, edited synthetic operator name/notes, closed the process completely, restarted, reopened History and its saved result. Name/notes, score, environment/scenario, 21 samples, 4 events and moving entity survived. Both trajectory and mock opened from the loaded snapshot with no source path state. |
| Duplicate | Reimporting the replay obstacle showed the exact nonfatal duplicate warning and did not add rows. |
| Multiple sessions | Three progress fixtures plus baselines/target produced six saved sessions. Obstacle filter showed five, average 80.80, best 100.00 and latest 82.67; chart showed chronological values including 56.0 -> 82.7 -> 100.0. |
| Database inspection | A local read-only .NET helper confirmed schema 1, foreign_keys=1, zero foreign_key_check violations, six valid operator/profile joins, 126 telemetry rows, 22 events, 24 criterion results, five replay contexts/tracks and 30 entity samples. |

No external SQLite browser was needed; no pixel tests were added. The generated
database and temporary inspection/capture helpers are outside Git.

## Remaining scope

Stage 6 is complete after build, tests and full restart verification. Stage 7
profile/settings editing, explicit reanalysis, migrations, database backup UI,
history deletion/export and source-file reconstruction remain unimplemented.
Normal local SQLite files are not encrypted by this prototype; Windows user-folder
permissions provide the existing local boundary. No cloud or authentication is added.
