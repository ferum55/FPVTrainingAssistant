using System.Text.Json;
using System.Text.Json.Serialization;

namespace FPVTrainingAssistant.Services;

public static class SessionJson
{
    // Retain the baseline producer version; acceptance uses IsSupportedVersion.
    public const string SupportedContractVersion = "1.0";
    public const string ReplayContractVersion = "1.1";

    public static bool IsSupportedVersion(string version) =>
        version is SupportedContractVersion or ReplayContractVersion;

    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Disallow,
            AllowTrailingCommas = false,
            NumberHandling = JsonNumberHandling.Strict,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip
        };
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
