using System.Text.Json;
using FPVTrainingAssistant.Data;
using FPVTrainingAssistant.Models;
using FPVTrainingAssistant.Services;
using FPVTrainingAssistant.ViewModels;

namespace FPVTrainingAssistant.Tests;

public sealed class ApplicationSettingsTests
{
    [Fact]
    public async Task VersionOneMigrationPreservesAllSavedDataAndSeedsReplayPreferences()
    {
        using var db=new PersistenceTestDatabase();await db.Initialize();
        var snapshot=PersistenceTestDatabase.Snapshot();await db.Sessions.SaveAsync(snapshot);
        await db.History.UpdateOperatorAsync(snapshot.Session.OperatorId,"Migration pilot","Retained notes");
        var before=JsonSerializer.Serialize(await db.Sessions.LoadAsync(snapshot.Session.SessionId));
        db.Execute("DROP TABLE app_settings; UPDATE app_metadata SET value='1' WHERE key='schema_version';");
        Assert.Equal("1",db.Scalar("SELECT value FROM app_metadata WHERE key='schema_version'"));
        await db.Initialize();
        Assert.Equal("2",db.Scalar("SELECT value FROM app_metadata WHERE key='schema_version'"));
        Assert.Equal(before,JsonSerializer.Serialize(await db.Sessions.LoadAsync(snapshot.Session.SessionId)));
        Assert.Equal("Migration pilot",Assert.Single(await db.History.ListOperatorsAsync()).Profile.DisplayName);
        Assert.Equal(3L,db.Scalar("SELECT COUNT(*) FROM app_settings"));
        var service=new ApplicationSettingsService(db.Connections);Assert.True((await service.LoadAsync()).IsSuccess);
        Assert.Equal(new ApplicationSettings(),service.Current);
        await service.SaveAsync(new(2,false,false));await db.Initialize();
        var recreated=new ApplicationSettingsService(db.Connections);await recreated.LoadAsync();
        Assert.Equal(new ApplicationSettings(2,false,false),recreated.Current);
        Assert.Equal(0L,db.Scalar("SELECT COUNT(*) FROM pragma_foreign_key_check"));
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task PreferencesRoundTripThroughARecreatedService(double speed)
    {
        using var db=new PersistenceTestDatabase();await db.Initialize();
        var service=new ApplicationSettingsService(db.Connections);
        Assert.True((await service.SaveAsync(new(speed,false,false))).IsSuccess);
        var reopened=new ApplicationSettingsService(new DatabaseConnectionFactory(db));await reopened.LoadAsync();
        Assert.Equal(new ApplicationSettings(speed,false,false),reopened.Current);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public async Task InvalidReplaySpeedDoesNotReplaceStoredDefaults(double speed)
    {
        using var db=new PersistenceTestDatabase();await db.Initialize();
        var service=new ApplicationSettingsService(db.Connections);await service.LoadAsync();
        Assert.False((await service.SaveAsync(new(speed,false,false))).IsSuccess);
        await service.LoadAsync();Assert.Equal(new ApplicationSettings(),service.Current);
    }

    [Fact]
    public async Task NewMockUsesDefaultsWhileRunningMockKeepsItsExistingState()
    {
        using var db=new PersistenceTestDatabase();await db.Initialize();
        var service=new ApplicationSettingsService(db.Connections);await service.LoadAsync();
        var snapshot=PersistenceTestDatabase.Snapshot();
        using var current=new SimulatorReplayMockViewModel(snapshot.Session,snapshot.Profile,snapshot.Analysis,service.Current);
        current.PlayPauseCommand.Execute(null);current.AdvanceBy(1);
        await service.SaveAsync(new(2,false,false));
        Assert.Equal(1,current.PlaybackSpeed);Assert.True(current.IsPlaying);Assert.Equal(1,current.CurrentTimeSec);
        Assert.True(current.IsMinimapVisible);Assert.True(current.IsEventLogVisible);
        using var next=new SimulatorReplayMockViewModel(snapshot.Session,snapshot.Profile,snapshot.Analysis,service.Current);
        Assert.Equal(2,next.PlaybackSpeed);Assert.False(next.IsMinimapVisible);Assert.False(next.IsEventLogVisible);
        Assert.False(next.IsPlaying);
    }
}
