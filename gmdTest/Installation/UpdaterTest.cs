using gmd.Common;
using gmd.Installation;
using gmdTest.Fixtures;

namespace gmdTest.Installation;

// When gmd is offered an update and what it installs, for a gmd handed in, since the test process
// is a dotnet host, which is never updated. GitHub and the install are faked: what they do is
// tested in GitHubReleasesTest and BinaryInstallerTest.
[TestClass]
public class UpdaterTest
{
    const string BinaryPath = "/home/anna/gmd/gmd";
    static readonly RunningGmd Running = new(new Version("1.6.1444.518"), BinaryPath, false, ["gmd_linux_x64"]);

    [TestMethod]
    public async Task TestANewerReleaseIsAnUpdate()
    {
        var (updater, config, _, _) = NewUpdater(stable: "1.6.1444.870");

        var available = AssertOk(await updater.IsUpdateAvailableAsync(Running));

        Assert.IsTrue(available.IsAvailable);
        Assert.AreEqual(new Version("1.6.1444.870"), available.Version);
        Assert.AreEqual("1.6.1444.870", config.Releases.LatestVersion);
    }

    [TestMethod]
    public async Task TestTheRunningReleaseIsNoUpdate()
    {
        var (updater, _, _, _) = NewUpdater(stable: "1.6.1444.518");

        var available = AssertOk(await updater.IsUpdateAvailableAsync(Running));

        Assert.IsFalse(available.IsAvailable);
        Assert.AreEqual(Running.Version, available.Version);
    }

    [TestMethod]
    public async Task TestADevInstanceIsOfferedNoUpdate()
    {
        var (updater, _, _, _) = NewUpdater(stable: "1.6.1444.870");

        var available = AssertOk(await updater.IsUpdateAvailableAsync(Running with { IsDevInstance = true }));

        Assert.IsFalse(available.IsAvailable);
    }

    [TestMethod]
    public async Task TestNoConnectionToGitHubIsAnError()
    {
        var (updater, _, releases, _) = NewUpdater(stable: "1.6.1444.870");
        releases.Failure = new Error("No such host is known");

        var e = AssertError(await updater.IsUpdateAvailableAsync(Running));

        StringAssert.Contains(e.AllMessages(), "No such host is known");
    }

    [TestMethod]
    public async Task TestAnUpdateInstallsTheBinaryOfThisPlatform()
    {
        var (updater, _, _, installer) = NewUpdater(stable: "1.6.1444.870");

        var version = AssertOk(await updater.UpdateAsync(Running));

        Assert.AreEqual(new Version("1.6.1444.870"), version);
        Assert.AreEqual(
            $"https://example.com/v1.6.1444.870/gmd_linux_x64 1.6.1444.870 {BinaryPath}",
            string.Join("\n", installer.Calls)
        );
    }

    [TestMethod]
    public async Task TestAFailedInstallIsAnError()
    {
        var (updater, _, _, installer) = NewUpdater(stable: "1.6.1444.870");
        installer.InstallTask = () => Task.FromResult<Result>(new Error("Disk full"));

        var e = AssertError(await updater.UpdateAsync(Running));

        StringAssert.Contains(e.AllMessages(), "Disk full");
    }

    static (Updater, Config, FakeGitHubReleases, FakeInstaller) NewUpdater(string stable)
    {
        var config = new FakeConfigService().Config;
        var releases = new FakeGitHubReleases(config)
        {
            Stable = ReleaseChoiceTest.NewRelease(stable, false, "gmd_linux_x64", "gmd_windows"),
        };
        var installer = new FakeInstaller();
        return (new Updater(config, releases, installer), config, releases, installer);
    }

    // GitHub, as the releases it has
    class FakeGitHubReleases : IGitHubReleases
    {
        readonly Config config;

        public FakeGitHubReleases(Config config) => this.config = config;

        public Release Stable { get; set; } = new();
        public Release Preview { get; set; } = new();
        public Error? Failure { get; set; }

        public Task<Result> FetchAsync()
        {
            if (Failure != null)
                return Task.FromResult<Result>(Failure);
            config.Set(c =>
            {
                c.Releases.StableRelease = Stable;
                c.Releases.PreRelease = Preview;
            });
            return Task.FromResult(Result.Ok);
        }
    }

    // The install, as the calls made to it and what it answers
    class FakeInstaller : IBinaryInstaller
    {
        public List<string> Calls { get; } = [];
        public Func<Task<Result>> InstallTask { get; set; } = () => Task.FromResult(Result.Ok);

        public Task<Result> InstallAsync(string url, string version, string targetPath)
        {
            Calls.Add($"{url} {version} {targetPath}");
            return InstallTask();
        }

        public void CleanUp(string targetPath) { }
    }
}
