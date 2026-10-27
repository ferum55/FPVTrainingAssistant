using System.Diagnostics;
using FPVTrainingAssistant.Models;
using FPVTrainingAssistant.Services;
using static FPVTrainingAssistant.Data.SqliteData;

namespace FPVTrainingAssistant.Data;

public sealed partial class ExerciseProfileRepository
{
    public Task<IReadOnlyList<ExerciseProfile>> GetProfilesAsync() => Task.Run<IReadOnlyList<ExerciseProfile>>(() =>
    {
        using var c = connections.Open();
        using var t = c.BeginTransaction(deferred:true);
        var keys = Query(c,t,"SELECT profile_id,version FROM exercise_profiles ORDER BY profile_id,version DESC",r => (Id:r.GetString(0),Version:r.GetInt32(1)));
        var profiles = keys.Select(k => Load(c,t,k.Id,k.Version)!).ToArray();
        t.Commit(); return profiles;
    });

    public Task<ExerciseProfile?> GetActiveVersionAsync(string id) => Task.Run(() =>
    {
        using var c = connections.Open();
        using var t = c.BeginTransaction(deferred:true);
        var versions = Query(c,t,"SELECT version FROM exercise_profiles WHERE profile_id=$p0 AND is_active=1 ORDER BY version DESC",r => r.GetInt32(0),id);
        var profile = versions.Count == 0 ? null : Load(c,t,id,versions[0]);
        t.Commit(); return profile;
    });

    public Task<ProfileSaveResult> CreateNewVersionAsync(ExerciseProfile workingCopy, int expectedLatestVersion, bool activate) => Task.Run(() =>
    {
        var issues = ExerciseProfileConfigurationValidator.Validate(workingCopy);
        if (issues.Count > 0) return new ProfileSaveResult(ProfileSaveState.Invalid,"Profile settings are invalid.",Issues:issues);
        try
        {
            using var c = connections.Open(); using var t = c.BeginTransaction();
            var latest = Query(c,t,"SELECT MAX(version) FROM exercise_profiles WHERE profile_id=$p0",r => r.IsDBNull(0) ? 0 : r.GetInt32(0),workingCopy.ProfileId).Single();
            if (latest != expectedLatestVersion || latest == 0)
                return new ProfileSaveResult(ProfileSaveState.Conflict,"Another profile version was saved. Refresh the profile list and review your working copy before saving again.");
            var next = new ExerciseProfile
            {
                ProfileId=workingCopy.ProfileId,Version=checked(latest+1),Name=workingCopy.Name,ExerciseType=workingCopy.ExerciseType,IsActive=activate,
                Criteria=workingCopy.Criteria.ToList(),ReferencePoints=workingCopy.ReferencePoints.ToList()
            };
            if (activate) Execute(c,t,"UPDATE exercise_profiles SET is_active=0 WHERE profile_id=$p0",next.ProfileId);
            Insert(c,t,next); t.Commit();
            return new ProfileSaveResult(ProfileSaveState.Saved,$"Profile version {next.Version} saved.",next);
        }
        catch (Exception e)
        {
            Trace.TraceError("Profile version save: {0}",e);
            return new ProfileSaveResult(ProfileSaveState.Failed,"Profile settings could not be saved.");
        }
    });

    public Task<ProfileSaveResult> SetActiveVersionAsync(string id, int version) => Task.Run(() =>
    {
        try
        {
            using var c = connections.Open(); using var t = c.BeginTransaction();
            var profile = Load(c,t,id,version);
            if (profile is null) return new ProfileSaveResult(ProfileSaveState.Failed,"The selected profile version is no longer available.");
            var issues = ExerciseProfileConfigurationValidator.Validate(profile);
            if (issues.Count > 0) return new ProfileSaveResult(ProfileSaveState.Invalid,"This profile configuration cannot be activated.",Issues:issues);
            Execute(c,t,"UPDATE exercise_profiles SET is_active=CASE WHEN version=$p0 THEN 1 ELSE 0 END WHERE profile_id=$p1",version,id);
            t.Commit();
            return new ProfileSaveResult(ProfileSaveState.Saved,"Active profile updated.");
        }
        catch (Exception e)
        {
            Trace.TraceError("Profile activation: {0}",e);
            return new ProfileSaveResult(ProfileSaveState.Failed,"Active profile could not be updated.");
        }
    });
}
