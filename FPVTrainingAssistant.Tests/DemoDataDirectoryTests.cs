using FPVTrainingAssistant.Data;

namespace FPVTrainingAssistant.Tests;

public sealed class DemoDataDirectoryTests
{
    [Fact]
    public async Task ExplicitDirectoryCreatesAnIsolatedFreshDatabaseAndReopensItWithoutResetting()
    {
        using var temporary = new PersistenceTestDatabase();
        var directory = Path.GetDirectoryName(temporary.DatabasePath)!;
        var paths = ApplicationDataPathProvider.FromArguments(["--data-directory", directory]);
        Assert.Equal(temporary.DatabasePath, paths.DatabasePath);
        Assert.NotEqual(ApplicationDataPathProvider.FromArguments([]).DatabasePath, paths.DatabasePath);
        Assert.True((await new DatabaseInitializer(new(paths)).InitializeAsync()).IsSuccess);
        Assert.Empty(await new HistoryRepository(new(paths)).ListOperatorsAsync());
        temporary.Execute("UPDATE app_settings SET value='2' WHERE key='default_replay_speed'");
        var restarted = ApplicationDataPathProvider.FromArguments(["--data-directory", directory]);
        Assert.True((await new DatabaseInitializer(new(restarted)).InitializeAsync()).IsSuccess);
        Assert.Equal("2", temporary.Scalar("SELECT value FROM app_settings WHERE key='default_replay_speed'"));
    }

    [Theory]
    [InlineData("--data-directory")]
    [InlineData("--unknown", "ignored")]
    [InlineData("--data-directory", "relative-demo-folder")]
    public void InvalidLaunchArgumentsNeverSilentlySelectTheNormalDatabase(params string[] arguments) =>
        Assert.Throws<ArgumentException>(() => ApplicationDataPathProvider.FromArguments(arguments));
}
