using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using FPVTrainingAssistant.Models;
using FPVTrainingAssistant.Services;

namespace FPVTrainingAssistant.ViewModels;

public sealed class ProfileEditorViewModel : ViewModelBase, IDisposable
{
    private string _name,_type;
    private bool _dirty;
    private ReferencePointEditor? _selectedPoint;
    private readonly HashSet<ReferencePointEditor> _attached=[];
    public ProfileEditorViewModel(ExerciseProfile source)
    {
        Source=source;_name=source.Name;_type=source.ExerciseType;
        Criteria=source.Criteria.Select(c=>new CriterionEditorViewModel(c)).ToArray();
        Points=new(source.ReferencePoints.OrderBy(p=>p.SequenceNo).Select(p=>new ReferencePointEditor(p)));
        foreach(var c in Criteria)
        {
            c.PropertyChanged+=Edited;
            foreach(var p in c.Parameters) p.PropertyChanged+=Edited;
        }
        foreach(var p in Points) Attach(p);
        Points.CollectionChanged+=PointsChanged;
        AddPointCommand=new(()=>
        {
            var point=new ReferencePointEditor(new ReferencePoint(1.5,3) { SequenceNo=Points.Count,X=0,Y=0,Z=0,PointType="Route" });
            Points.Add(point);SelectedPoint=point;
        });
        RemovePointCommand=new(()=>{ if(SelectedPoint is { } point) { Points.Remove(point);SelectedPoint=Points.LastOrDefault(); } },()=>SelectedPoint is not null);
        MoveUpCommand=new(()=>Move(-1),()=>SelectedPoint is not null && Points.IndexOf(SelectedPoint)>0);
        MoveDownCommand=new(()=>Move(1),()=>SelectedPoint is not null && Points.IndexOf(SelectedPoint)<Points.Count-1);
        RecalculateContributions();
    }
    public ExerciseProfile Source { get; }
    public string ProfileId=>Source.ProfileId;
    public string VersionText=>"Editing working copy of version "+Source.Version;
    public string Name { get=>_name;set { if(SetProperty(ref _name,value)) Dirty(); } }
    public string ExerciseType { get=>_type;set { if(SetProperty(ref _type,value)) Dirty(); } }
    public IReadOnlyList<string> ExerciseTypes { get; }=[Models.ExerciseTypes.ObstacleCourse,Models.ExerciseTypes.TargetMission];
    public IReadOnlyList<CriterionEditorViewModel> Criteria { get; }
    public ObservableCollection<ReferencePointEditor> Points { get; }
    public bool HasUnsavedChanges { get=>_dirty;private set { if(SetProperty(ref _dirty,value)) OnPropertyChanged(nameof(ChangeStatus)); } }
    public string ChangeStatus=>HasUnsavedChanges ? "Unsaved profile changes" : "Stored version is unchanged";
    public ReferencePointEditor? SelectedPoint { get=>_selectedPoint;set { if(SetProperty(ref _selectedPoint,value)) CommandsChanged(); } }
    public RelayCommand AddPointCommand { get; }
    public RelayCommand RemovePointCommand { get; }
    public RelayCommand MoveUpCommand { get; }
    public RelayCommand MoveDownCommand { get; }

    public ExerciseProfile? Build(out IReadOnlyList<string> validationIssues)
    {
        var issues=new List<string>();
        var criteria=Criteria.Select(c=>c.Build(issues)!).ToList();
        var points=Points.Select(p=>p.Build(issues)).Where(p=>p is not null).Cast<ReferencePoint>().ToList();
        var profile=new ExerciseProfile { ProfileId=ProfileId,Version=Source.Version,Name=Name,ExerciseType=ExerciseType,IsActive=Source.IsActive,Criteria=criteria,ReferencePoints=points };
        if(issues.Count==0) issues.AddRange(ExerciseProfileConfigurationValidator.Validate(profile));
        validationIssues=issues.Distinct().ToArray();
        return issues.Count==0 ? profile : null;
    }

    private void Move(int direction)
    { if(SelectedPoint is { } p) { var i=Points.IndexOf(p);Points.Move(i,i+direction);CommandsChanged(); } }
    private void Attach(ReferencePointEditor p) { if(_attached.Add(p)) p.PropertyChanged+=Edited; }
    private void PointsChanged(object? sender,NotifyCollectionChangedEventArgs e)
    {
        foreach(var old in _attached.Where(p=>!Points.Contains(p)).ToArray()) {old.PropertyChanged-=Edited;_attached.Remove(old);}
        foreach(var point in Points) Attach(point);
        for(var i=0;i<Points.Count;i++) Points[i].SequenceNo=i;
        Dirty();CommandsChanged();
    }
    private void Edited(object? sender,PropertyChangedEventArgs e)
    {
        if(e.PropertyName==nameof(CriterionEditorViewModel.Contribution)) return;
        Dirty();RecalculateContributions();
    }
    private void Dirty()=>HasUnsavedChanges=true;
    private void CommandsChanged() { RemovePointCommand.RaiseCanExecuteChanged();MoveUpCommand.RaiseCanExecuteChanged();MoveDownCommand.RaiseCanExecuteChanged(); }
    private void RecalculateContributions()
    {
        var weights=Criteria.Select(c=>double.TryParse(c.WeightText,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var w) && double.IsFinite(w) && w>0 && c.IsEnabled ? w : 0).ToArray();
        var max=weights.DefaultIfEmpty().Max();var sum=max>0 ? weights.Sum(w=>w/max) : 0;
        for(var i=0;i<Criteria.Count;i++) Criteria[i].Contribution=sum>0 ? (weights[i]/max/sum*100).ToString("F1",System.Globalization.CultureInfo.InvariantCulture)+"%" : "—";
    }
    public void Dispose()
    {
        Points.CollectionChanged-=PointsChanged;
        foreach(var p in _attached) p.PropertyChanged-=Edited;
        foreach(var c in Criteria) {c.PropertyChanged-=Edited;foreach(var p in c.Parameters) p.PropertyChanged-=Edited;}
    }
}
