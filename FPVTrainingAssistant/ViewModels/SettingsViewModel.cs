using System.ComponentModel;
using FPVTrainingAssistant.Data;
using FPVTrainingAssistant.Models;
using FPVTrainingAssistant.Services;

namespace FPVTrainingAssistant.ViewModels;

public sealed record ProfileVersionItem(ExerciseProfile Profile)
{
    public string Label=>$"{Profile.Name} · {Profile.ProfileId} · {Profile.ExerciseType} · v{Profile.Version} · {(Profile.IsActive ? "Active" : "Inactive")}";
}

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly IExerciseProfileRepository _profiles;
    private readonly IApplicationSettingsService _preferences;
    private IReadOnlyList<ProfileVersionItem> _items=[];
    private ProfileVersionItem? _selected,_pending;
    private ProfileEditorViewModel? _draft;
    private IReadOnlyList<string> _issues=[];
    private string _message="";
    private bool _busy,_activateAfterSave,_appDirty,_minimap=true,_events=true;
    private double _speed=1;

    public SettingsViewModel(IExerciseProfileRepository profiles,IApplicationSettingsService preferences)
    {
        _profiles=profiles;_preferences=preferences;
        RefreshCommand=new(RefreshAsync,()=>!IsBusy);
        SaveVersionCommand=new(SaveVersionAsync,()=>!IsBusy && Draft?.HasUnsavedChanges==true && !HasPendingSelection);
        ActivateCommand=new(ActivateAsync,()=>!IsBusy && SelectedProfile is not null && Draft?.HasUnsavedChanges!=true && !HasPendingSelection);
        ResetCommand=new(()=> {if(SelectedProfile is not null) LoadDraft(SelectedProfile);ClearPending();Message="Working copy reset.";},()=>!IsBusy && Draft is not null);
        DiscardAndSwitchCommand=new(()=> {if(_pending is { } next) LoadDraft(next);ClearPending();},()=>!IsBusy && HasPendingSelection);
        KeepEditingCommand=new(()=>{ClearPending();OnPropertyChanged(nameof(SelectedProfile));Message="Your working copy has been kept.";});
        SaveApplicationCommand=new(SaveApplicationAsync,()=>!IsBusy && HasUnsavedApplicationSettings);
    }
    public IReadOnlyList<ProfileVersionItem> Profiles { get=>_items;private set=>SetProperty(ref _items,value); }
    public ProfileVersionItem? SelectedProfile
    {
        get=>_selected;
        set
        {
            if(value is null || ReferenceEquals(value,_selected)) return;
            if(Draft?.HasUnsavedChanges==true)
            { _pending=value;OnPropertyChanged(nameof(HasPendingSelection));OnPropertyChanged(nameof(SelectedProfile));Message="Unsaved changes: discard and switch, or keep editing.";CommandsChanged();return; }
            LoadDraft(value);Message="";
        }
    }
    public ProfileEditorViewModel? Draft { get=>_draft;private set {SetProperty(ref _draft,value);OnPropertyChanged(nameof(HasProfile));CommandsChanged();} }
    public bool HasProfile=>Draft is not null;
    public bool HasPendingSelection=>_pending is not null;
    public IReadOnlyList<string> ValidationIssues { get=>_issues;private set {SetProperty(ref _issues,value.Select(FriendlyIssue).ToArray());OnPropertyChanged(nameof(HasValidationIssues));} }
    public bool HasValidationIssues=>ValidationIssues.Count>0;
    public string Message { get=>_message;private set {SetProperty(ref _message,value);OnPropertyChanged(nameof(HasMessage));} }
    public bool HasMessage=>!string.IsNullOrEmpty(Message);
    public bool IsBusy {get=>_busy;private set {if(SetProperty(ref _busy,value)) {OnPropertyChanged(nameof(CanEdit));CommandsChanged();}}}
    public bool CanEdit=>!IsBusy;
    public bool ActivateAfterSave {get=>_activateAfterSave;set=>SetProperty(ref _activateAfterSave,value);}
    public IReadOnlyList<double> ReplaySpeeds {get;}=[0.5,1,2];
    public double DefaultReplaySpeed {get=>_speed;set {if(SetProperty(ref _speed,value)) AppEdited();}}
    public bool ShowMinimapByDefault {get=>_minimap;set {if(SetProperty(ref _minimap,value)) AppEdited();}}
    public bool ShowEventLogByDefault {get=>_events;set {if(SetProperty(ref _events,value)) AppEdited();}}
    public bool HasUnsavedApplicationSettings {get=>_appDirty;private set {SetProperty(ref _appDirty,value);SaveApplicationCommand.RaiseCanExecuteChanged();}}
    public AsyncRelayCommand RefreshCommand {get;}
    public AsyncRelayCommand SaveVersionCommand {get;}
    public AsyncRelayCommand ActivateCommand {get;}
    public AsyncRelayCommand SaveApplicationCommand {get;}
    public RelayCommand ResetCommand {get;}
    public RelayCommand DiscardAndSwitchCommand {get;}
    public RelayCommand KeepEditingCommand {get;}

    public async Task RefreshAsync()
    {
        IsBusy=true;
        try
        {
            await ReloadProfilesAsync();
            if(!HasUnsavedApplicationSettings)
            {
                var result=await _preferences.LoadAsync();
                if(!result.IsSuccess) Message=result.Message;
                _speed=_preferences.Current.DefaultReplaySpeed;_minimap=_preferences.Current.ShowMinimapByDefault;_events=_preferences.Current.ShowEventLogByDefault;
                OnPropertyChanged(nameof(DefaultReplaySpeed));OnPropertyChanged(nameof(ShowMinimapByDefault));OnPropertyChanged(nameof(ShowEventLogByDefault));
            }
        }
        catch {Message="Settings could not be loaded. Your working copy has been kept.";}
        finally {IsBusy=false;}
    }
    private async Task ReloadProfilesAsync(ExerciseProfile? choose=null)
    {
        Profiles=(await _profiles.GetProfilesAsync()).Select(p=>new ProfileVersionItem(p)).ToArray();
        var selected=Profiles.FirstOrDefault(p=>p.Profile.ProfileId==(choose?.ProfileId ?? _selected?.Profile.ProfileId) && p.Profile.Version==(choose?.Version ?? _selected?.Profile.Version)) ?? Profiles.FirstOrDefault();
        if(Draft?.HasUnsavedChanges==true && choose is null) {SetProperty(ref _selected,selected,nameof(SelectedProfile));return;}
        if(selected is not null) LoadDraft(selected);
    }
    private void LoadDraft(ProfileVersionItem item)
    {
        if(Draft is not null) {Draft.PropertyChanged-=DraftChanged;Draft.Dispose();}
        SetProperty(ref _selected,item,nameof(SelectedProfile));
        Draft=new ProfileEditorViewModel(item.Profile);Draft.PropertyChanged+=DraftChanged;ValidationIssues=[];
    }
    private void DraftChanged(object? sender,PropertyChangedEventArgs e)=>CommandsChanged();
    private void ClearPending() {_pending=null;OnPropertyChanged(nameof(HasPendingSelection));CommandsChanged();}
    private void AppEdited()=>HasUnsavedApplicationSettings=true;
    private static string FriendlyIssue(string issue) => issue switch
    {
        "The profile has no enabled criteria or positive total weight." or "The profile has no enabled criteria." => "At least one criterion must be enabled with a positive weight.",
        "CompletionTime requires TargetTimeSec > 0 and MaximumTimeSec > TargetTimeSec." => "Target time must be positive. Maximum time must be greater than target time.",
        "Every enabled criterion must have a positive weight." => "Each enabled criterion needs a weight greater than zero.",
        "PenaltyPerCollision must be nonnegative." => "Penalty per collision cannot be negative.",
        "YellowZoneFactor must be between 0 and 1." => "Yellow-zone contribution must be between 0 and 1.",
        "Configured DamageScore values must be between 0 and 100." => "Damage scores must be between 0 and 100.",
        "The reference route has no non-zero X/Y segment." => "The route needs at least two different horizontal positions.",
        _ => issue
    };
    private void CommandsChanged()
    {RefreshCommand?.RaiseCanExecuteChanged();SaveVersionCommand?.RaiseCanExecuteChanged();ActivateCommand?.RaiseCanExecuteChanged();ResetCommand?.RaiseCanExecuteChanged();DiscardAndSwitchCommand?.RaiseCanExecuteChanged();SaveApplicationCommand?.RaiseCanExecuteChanged();}
    private async Task SaveVersionAsync()
    {
        if(Draft is null) return;
        var profile=Draft.Build(out var issues);ValidationIssues=issues;
        if(profile is null) {Message="Profile settings are invalid. Review the details below.";return;}
        IsBusy=true;
        try
        {
            var latest=Profiles.Where(p=>p.Profile.ProfileId==profile.ProfileId).Max(p=>p.Profile.Version);
            var result=await _profiles.CreateNewVersionAsync(profile,latest,ActivateAfterSave);
            Message=result.Message;ValidationIssues=result.Issues ?? [];
            if(result.State==ProfileSaveState.Saved) {ClearPending();await ReloadProfilesAsync(result.Profile);}
        }
        catch {Message="Profile settings could not be saved. Your working copy has been kept.";}
        finally {IsBusy=false;}
    }
    private async Task ActivateAsync()
    {
        if(SelectedProfile is null) return;
        IsBusy=true;
        try
        {
            var profile=SelectedProfile.Profile;
            var result=await _profiles.SetActiveVersionAsync(profile.ProfileId,profile.Version);
            Message=result.Message;ValidationIssues=result.Issues ?? [];
            if(result.State==ProfileSaveState.Saved) await ReloadProfilesAsync(profile);
        }
        catch {Message="Active profile could not be updated.";}
        finally {IsBusy=false;}
    }
    private async Task SaveApplicationAsync()
    {
        IsBusy=true;
        try
        {
            var result=await _preferences.SaveAsync(new(DefaultReplaySpeed,ShowMinimapByDefault,ShowEventLogByDefault));
            Message=result.Message;if(result.IsSuccess) HasUnsavedApplicationSettings=false;
        }
        catch {Message="Application settings could not be saved.";}
        finally {IsBusy=false;}
    }
}
