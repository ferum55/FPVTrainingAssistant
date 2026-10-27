using System.Globalization;
using FPVTrainingAssistant.Models;

namespace FPVTrainingAssistant.ViewModels;

public sealed class CriterionParameterEditor(string key, string label, string value, string defaultValue) : ViewModelBase
{
    private string _value = value;
    public string Key { get; } = key;
    public string Label { get; } = label;
    public string DefaultHint { get; } = "Default when blank: " + defaultValue;
    public string ValueText { get => _value; set => SetProperty(ref _value,value); }
}

public sealed class CriterionEditorViewModel : ViewModelBase
{
    private bool _enabled;
    private string _weight, _threshold, _contribution = "";
    private readonly EvaluationCriterion _source;
    public CriterionEditorViewModel(EvaluationCriterion source)
    {
        _source=source;_enabled=source.IsEnabled;_weight=Number(source.Weight);_threshold=source.Threshold is { } v ? Number(v) : "";
        Parameters=Definitions(source.RuleType).Select(p => new CriterionParameterEditor(p.Key,p.Label,
            source.Parameters?.TryGetValue(p.Key,out var value)==true ? Number(value) : "",Number(p.Default))).ToArray();
    }
    public string CriterionId => _source.CriterionId;
    public string Name => _source.Name;
    public string Description => _source.Description;
    public string RuleType => _source.RuleType;
    public bool IsEnabled { get=>_enabled;set=>SetProperty(ref _enabled,value); }
    public string WeightText { get=>_weight;set=>SetProperty(ref _weight,value); }
    public string ThresholdText { get=>_threshold;set=>SetProperty(ref _threshold,value); }
    public string Contribution { get=>_contribution; internal set=>SetProperty(ref _contribution,value); }
    public IReadOnlyList<CriterionParameterEditor> Parameters { get; }
    public bool HasParameters => Parameters.Count>0;
    public bool HasThreshold => _source.Threshold is not null;

    internal EvaluationCriterion? Build(List<string> issues)
    {
        var weight=Parse(WeightText,Name+" weight",issues);
        double? threshold=string.IsNullOrWhiteSpace(ThresholdText) ? null : Parse(ThresholdText,Name+" threshold",issues);
        var values=_source.Parameters is null ? new Dictionary<string,double>() : new Dictionary<string,double>(_source.Parameters,StringComparer.Ordinal);
        foreach(var p in Parameters)
        {
            if(string.IsNullOrWhiteSpace(p.ValueText)) values.Remove(p.Key);
            else values[p.Key]=Parse(p.ValueText,Name+": "+p.Label,issues);
        }
        return new EvaluationCriterion { CriterionId=CriterionId,Name=Name,Description=Description,RuleType=RuleType,
            IsEnabled=IsEnabled,Weight=weight,Threshold=threshold,Parameters=values.Count==0 && _source.Parameters is null ? null : values };
    }

    internal static string Number(double value)=>value.ToString("G",CultureInfo.InvariantCulture);
    internal static double Parse(string text,string label,List<string> issues)
    {
        if(double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out var value) && double.IsFinite(value)) return value;
        issues.Add(label+" must be a finite number. Use a decimal point when needed.");return 0;
    }
    private static (string Key,string Label,double Default)[] Definitions(string rule)=>rule switch
    {
        RuleTypes.CollisionAvoidance=>[("PenaltyPerCollision","Penalty per collision",20)],
        RuleTypes.RouteAdherence=>[("YellowZoneFactor","Yellow-zone contribution (0–1)",0.5)],
        RuleTypes.CompletionTime=>[("TargetTimeSec","Target time (s)",0),("MaximumTimeSec","Maximum time (s)",0)],
        RuleTypes.TargetDamage=>[("DamageScore_Critical","Critical category score",100),("DamageScore_Engine","Engine category score",90),
            ("DamageScore_Weapon","Weapon category score",80),("DamageScore_Track","Track category score",70),("DamageScore_Hull","Hull category score",60),("DamageScore_Default","Other category score",50)],
        _=>[]
    };
}

public sealed class ReferencePointEditor : ViewModelBase
{
    private string _x,_y,_z,_green,_yellow,_type;
    private int _sequence;
    public ReferencePointEditor(ReferencePoint point)
    { _sequence=point.SequenceNo;_type=point.PointType;_x=CriterionEditorViewModel.Number(point.X);_y=CriterionEditorViewModel.Number(point.Y);_z=CriterionEditorViewModel.Number(point.Z);_green=CriterionEditorViewModel.Number(point.GreenRadiusM);_yellow=CriterionEditorViewModel.Number(point.YellowRadiusM); }
    public int SequenceNo { get=>_sequence;internal set=>SetProperty(ref _sequence,value); }
    public string PointType { get=>_type;set=>SetProperty(ref _type,value); }
    public string XText { get=>_x;set=>SetProperty(ref _x,value); }
    public string YText { get=>_y;set=>SetProperty(ref _y,value); }
    public string ZText { get=>_z;set=>SetProperty(ref _z,value); }
    public string GreenRadiusText { get=>_green;set=>SetProperty(ref _green,value); }
    public string YellowRadiusText { get=>_yellow;set=>SetProperty(ref _yellow,value); }
    internal ReferencePoint? Build(List<string> issues)
    {
        var label="Point "+SequenceNo;
        var x=CriterionEditorViewModel.Parse(XText,label+" X",issues);var y=CriterionEditorViewModel.Parse(YText,label+" Y",issues);var z=CriterionEditorViewModel.Parse(ZText,label+" Z",issues);
        var green=CriterionEditorViewModel.Parse(GreenRadiusText,label+" green radius",issues);var yellow=CriterionEditorViewModel.Parse(YellowRadiusText,label+" yellow radius",issues);
        try { return new(green,yellow) { SequenceNo=SequenceNo,X=x,Y=y,Z=z,PointType=PointType }; }
        catch(ArgumentOutOfRangeException e)
        {
            issues.Add(label+(e.ParamName=="greenRadiusM" ? ": green radius must be nonnegative." : ": yellow radius must be greater than or equal to green radius."));
            return null;
        }
    }
}
