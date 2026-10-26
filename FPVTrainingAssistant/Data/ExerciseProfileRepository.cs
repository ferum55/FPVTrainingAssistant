using FPVTrainingAssistant.Models;
using Microsoft.Data.Sqlite;
using static FPVTrainingAssistant.Data.SqliteData;

namespace FPVTrainingAssistant.Data;

public sealed class ExerciseProfileRepository(DatabaseConnectionFactory connections)
{
    public Task<ExerciseProfile?> LoadAsync(string id, int version) => Task.Run(() =>
    {
        using var c = connections.Open();
        return Load(c, null, id, version);
    });

    internal static ExerciseProfile? Load(SqliteConnection c, SqliteTransaction? t, string id, int version)
    {
        var profiles = Query(c,t,"SELECT * FROM exercise_profiles WHERE profile_id=$p0 AND version=$p1", r => new ExerciseProfile
        {
            ProfileId = Text(r,"profile_id")!, Version = r.GetInt32(r.GetOrdinal("version")), Name = Text(r,"name")!,
            ExerciseType = Text(r,"exercise_type")!, IsActive = Bool(r,"is_active")!.Value
        }, id,version);
        var profile = profiles.SingleOrDefault();
        if (profile is null) return null;
        profile.ReferencePoints.AddRange(Query(c,t,"SELECT * FROM reference_points WHERE profile_id=$p0 AND profile_version=$p1 ORDER BY sequence_no", r =>
            new ReferencePoint(Number(r,"green_radius_m")!.Value,Number(r,"yellow_radius_m")!.Value)
            {
                SequenceNo = r.GetInt32(r.GetOrdinal("sequence_no")), X = Number(r,"x")!.Value, Y = Number(r,"y")!.Value,
                Z = Number(r,"z")!.Value, PointType = Text(r,"point_type")!
            },id,version));
        profile.Criteria.AddRange(Query(c,t,"SELECT * FROM evaluation_criteria WHERE profile_id=$p0 AND profile_version=$p1 ORDER BY ordinal", r => new EvaluationCriterion
        {
            CriterionId = Text(r,"criterion_id")!, Name = Text(r,"name")!, Description = Text(r,"description")!,
            Weight = Number(r,"weight")!.Value, RuleType = Text(r,"rule_type")!, IsEnabled = Bool(r,"is_enabled")!.Value,
            Threshold = Number(r,"threshold"), Parameters = ReadJson<Dictionary<string,double>>(Text(r,"parameters_json"))
        },id,version));
        return profile;
    }

    internal static void Insert(SqliteConnection c, SqliteTransaction t, ExerciseProfile p)
    {
        Execute(c,t,"INSERT INTO exercise_profiles(profile_id,version,name,exercise_type,is_active) VALUES($p0,$p1,$p2,$p3,$p4)",p.ProfileId,p.Version,p.Name,p.ExerciseType,p.IsActive);
        foreach (var point in p.ReferencePoints)
            Execute(c,t,"INSERT INTO reference_points(profile_id,profile_version,sequence_no,x,y,z,point_type,green_radius_m,yellow_radius_m) VALUES($p0,$p1,$p2,$p3,$p4,$p5,$p6,$p7,$p8)",
                p.ProfileId,p.Version,point.SequenceNo,point.X,point.Y,point.Z,point.PointType,point.GreenRadiusM,point.YellowRadiusM);
        for (var i = 0; i < p.Criteria.Count; i++)
        {
            var criterion = p.Criteria[i];
            Execute(c,t,"INSERT INTO evaluation_criteria(profile_id,profile_version,criterion_id,ordinal,name,description,weight,rule_type,is_enabled,threshold,parameters_json) VALUES($p0,$p1,$p2,$p3,$p4,$p5,$p6,$p7,$p8,$p9,$p10)",
                p.ProfileId,p.Version,criterion.CriterionId,i,criterion.Name,criterion.Description,criterion.Weight,criterion.RuleType,criterion.IsEnabled,criterion.Threshold,Json(criterion.Parameters));
        }
    }

    internal static bool SameSnapshot(ExerciseProfile a, ExerciseProfile b) => Canonical(a) == Canonical(b);
    private static string? Canonical(ExerciseProfile p) => Json(new
    {
        p.ProfileId, p.Version, p.Name, p.ExerciseType, p.IsActive,
        Points = p.ReferencePoints.OrderBy(x => x.SequenceNo),
        Criteria = p.Criteria.Select(x => new { x.CriterionId,x.Name,x.Description,x.Weight,x.RuleType,x.IsEnabled,x.Threshold,
            Parameters = x.Parameters?.OrderBy(k => k.Key, StringComparer.Ordinal).ToArray() })
    });
}
