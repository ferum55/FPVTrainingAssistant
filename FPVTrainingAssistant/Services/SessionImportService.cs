using System.IO;
using System.Text.Json;
using FPVTrainingAssistant.Models;

namespace FPVTrainingAssistant.Services;

public sealed class SessionImportService : ISessionImportService
{
    private readonly SessionValidator _validator = new();

    public async Task<SessionImportResult> ImportAsync(string filePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return Failure("InvalidFilePath", "$", "Select a nonempty JSON file path.");
        }

        try
        {
            await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var document = await JsonDocument.ParseAsync(stream, new JsonDocumentOptions
            {
                AllowTrailingCommas = SessionJson.Options.AllowTrailingCommas,
                CommentHandling = SessionJson.Options.ReadCommentHandling,
                MaxDepth = SessionJson.Options.MaxDepth
            }, cancellationToken);

            var structure = SessionJsonStructureValidator.Validate(document.RootElement);
            if (!structure.IsValid) return new(null, structure);

            var session = document.RootElement.Deserialize<TrainingSession>(SessionJson.Options);
            var validation = _validator.Validate(session);
            if (!validation.IsValid || session is null) return new(null, validation);

            cancellationToken.ThrowIfCancellationRequested();
            NormalizeAngles(session.Telemetry);
            // LINQ ordering is stable: simultaneous events preserve their input order.
            var orderedEvents = session.Events.OrderBy(e => e.TimeSec).ToArray();
            session.Events.Clear();
            session.Events.AddRange(orderedEvents);
            return new(session, validation);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (JsonException exception)
        {
            return Failure("InvalidJson", exception.Path ?? "$", "Invalid JSON or incompatible field value: " + exception.Message);
        }
        catch (FileNotFoundException)
        {
            return Failure("FileNotFound", "$", "The session file does not exist.");
        }
        catch (DirectoryNotFoundException)
        {
            return Failure("FileNotFound", "$", "The session file directory does not exist.");
        }
        catch (ArgumentException exception)
        {
            // Includes invalid paths and defensive Stage 1 model guards.
            return Failure("InvalidInput", "$", exception.Message);
        }
        catch (Exception exception)
        {
            return new(null, new ValidationResult([]), "Unable to read or deserialize the session: " + exception.Message);
        }
    }

    private static SessionImportResult Failure(string code, string path, string message) =>
        new(null, new ValidationResult([new(code, path, message)]));

    private static void NormalizeAngles(List<TelemetrySample> telemetry)
    {
        for (var i = 0; i < telemetry.Count; i++)
        {
            var sample = telemetry[i];
            telemetry[i] = new TelemetrySample
            {
                TimeSec = sample.TimeSec,
                X = sample.X, Y = sample.Y, Z = sample.Z,
                SpeedMps = sample.SpeedMps, AltitudeM = sample.AltitudeM,
                PitchDeg = NormalizeAngle(sample.PitchDeg),
                RollDeg = NormalizeAngle(sample.RollDeg),
                YawDeg = NormalizeAngle(sample.YawDeg),
                BatteryPercent = sample.BatteryPercent,
                ControlLinkQualityPercent = sample.ControlLinkQualityPercent,
                VideoLinkQualityPercent = sample.VideoLinkQualityPercent
            };
        }
    }

    private static double NormalizeAngle(double angle)
    {
        var wrapped = angle % 360;
        if (wrapped < -180) return wrapped + 360;
        return wrapped >= 180 ? wrapped - 360 : wrapped;
    }
}
