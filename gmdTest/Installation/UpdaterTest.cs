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

    // Installed with Homebrew, which links /opt/homebrew/bin/gmd, on the PATH, to the binary
    static readonly RunningGmd Brewed = Running with { Path = "/opt/homebrew/Cellar/gmd/1.6.1444.518/bin/gmd" };

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

    // Offered, it failed once taken, as "No binary available"
    [TestMethod]
    public async Task TestAReleaseWithNoBinaryForThisPlatformIsNoUpdate()
    {
        var (updater, _, releases, _) = NewUpdater(stable: "1.6.1444.870");
        releases.Stable = ReleaseChoiceTest.NewRelease("1.6.1444.870", false, "gmd_windows", "SHA256SUMS");

        var available = AssertOk(await updater.IsUpdateAvailableAsync(Running));

        Assert.IsFalse(available.IsAvailable);
    }

    // Not the running version, which the update dialog took for the version it had updated to, and so
    // said gmd was updated and quit
    [TestMethod]
    public async Task TestAnUpdateWithNoNewerVersionIsAnError()
    {
        var (updater, _, _, installer) = NewUpdater(stable: "1.6.1444.518");

        var e = AssertError(await updater.UpdateAsync(Running));

        StringAssert.Contains(e.Message, "no newer version");
        Assert.AreEqual(0, installer.Calls.Count);
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
            "1.6.1444.870 gmd_linux_x64 https://example.com/v1.6.1444.870/gmd_linux_x64"
                + $" https://example.com/v1.6.1444.870/SHA256SUMS {BinaryPath}",
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

    // A dev instance is a build, e.g. gmd/bin/Debug, which is never replaced by a release
    [TestMethod]
    public async Task TestADevInstanceIsNeverUpdated()
    {
        var (updater, _, _, installer) = NewUpdater(stable: "1.6.1444.870");

        var e = AssertError(await updater.UpdateAsync(Running with { IsDevInstance = true }));

        StringAssert.Contains(e.Message, "dev instance");
        Assert.AreEqual(0, installer.Calls.Count);
    }

    // Taken to be 'gmd' when it was unknown, it was a file in the current folder, a repository
    [TestMethod]
    public async Task TestAGmdWithNoKnownPathIsNotUpdated()
    {
        var (updater, _, _, installer) = NewUpdater(stable: "1.6.1444.870");

        var e = AssertError(await updater.UpdateAsync(Running with { Path = null }));

        StringAssert.Contains(e.Message, "path of the gmd binary is not known");
        Assert.AreEqual(0, installer.Calls.Count);
    }

    // The update dialog and the regular check can both start an update, and the second waits for the
    // first rather than download and install the same binary beside it
    [TestMethod]
    public async Task TestTwoUpdatesAtOnceInstallOnce()
    {
        var (updater, _, _, installer) = NewUpdater(stable: "1.6.1444.870");
        var installing = new TaskCompletionSource<Result>();
        installer.InstallTask = () => installing.Task;

        var first = updater.UpdateAsync(Running);
        var second = updater.UpdateAsync(Running);
        installing.SetResult(Result.Ok);

        Assert.AreEqual(new Version("1.6.1444.870"), AssertOk(await first));
        Assert.AreEqual(new Version("1.6.1444.870"), AssertOk(await second));
        Assert.AreEqual(1, installer.Calls.Count);
    }

    // Cancel, from the box shown while updating, reaches the update in progress, whichever started it,
    // and the next update can be cancelled again, rather than be cancelled already
    [TestMethod]
    public async Task TestCancelCancelsTheUpdateInProgress()
    {
        var (updater, _, _, installer) = NewUpdater(stable: "1.6.1444.870");
        var installing = new TaskCompletionSource<Result>();
        installer.InstallTask = () => installing.Task;

        var update = updater.UpdateAsync(Running);
        updater.CancelUpdate();

        Assert.IsTrue(installer.Cancel.IsCancellationRequested);
        installing.SetResult(new UpdateCancelledError());
        Assert.IsInstanceOfType<UpdateCancelledError>(AssertError(await update));

        installer.InstallTask = () => Task.FromResult(Result.Ok);
        AssertOk(await updater.UpdateAsync(Running));
        Assert.IsFalse(installer.Cancel.IsCancellationRequested, "A new update is not cancelled already");
    }

    // A cancel with no update in progress cancels none to come
    [TestMethod]
    public async Task TestACancelWithNoUpdateInProgressCancelsNothing()
    {
        var (updater, _, _, installer) = NewUpdater(stable: "1.6.1444.870");

        updater.CancelUpdate();

        AssertOk(await updater.UpdateAsync(Running));
        Assert.IsFalse(installer.Cancel.IsCancellationRequested);
    }

    // The running gmd is still the old version after an update, so the release is still newer than
    // it, and the regular check of a gmd left open installed it again every hour
    [TestMethod]
    public async Task TestAnInstalledVersionIsNotInstalledAgain()
    {
        var (updater, _, _, installer) = NewUpdater(stable: "1.6.1444.870");

        AssertOk(await updater.UpdateAsync(Running));
        var again = AssertOk(await updater.UpdateAsync(Running));

        Assert.AreEqual(new Version("1.6.1444.870"), again);
        Assert.AreEqual(1, installer.Calls.Count);
    }

    [TestMethod]
    public async Task TestANewerReleaseAfterAnUpdateIsInstalled()
    {
        var (updater, _, releases, installer) = NewUpdater(stable: "1.6.1444.870");
        AssertOk(await updater.UpdateAsync(Running));

        releases.Stable = ReleaseChoiceTest.NewRelease("1.6.1445.10", false, "gmd_linux_x64");
        var newer = AssertOk(await updater.UpdateAsync(Running));

        Assert.AreEqual(new Version("1.6.1445.10"), newer);
        Assert.AreEqual(2, installer.Calls.Count);
    }

    [TestMethod]
    public async Task TestAFailedUpdateIsTriedAgain()
    {
        var (updater, _, _, installer) = NewUpdater(stable: "1.6.1444.870");
        installer.InstallTask = () => Task.FromResult<Result>(new Error("Timed out"));
        AssertError(await updater.UpdateAsync(Running));

        installer.InstallTask = () => Task.FromResult(Result.Ok);
        AssertOk(await updater.UpdateAsync(Running));

        Assert.AreEqual(2, installer.Calls.Count);
    }

    // Homebrew updates it, and gmd replacing its binary would leave Homebrew taking the old version for
    // the one installed
    [TestMethod]
    public async Task TestAGmdInstalledWithHomebrewIsNotUpdatedByGmd()
    {
        var (updater, _, _, installer) = NewUpdater(stable: "1.6.1444.870");

        var e = AssertError(await updater.UpdateAsync(Brewed));

        StringAssert.Contains(e.Message, "installed with Homebrew");
        StringAssert.Contains(e.Message, "'brew update && brew upgrade gmd'");
        Assert.AreEqual(0, installer.Calls.Count);
    }

    // The update is still offered, as the command that makes it, but not installed by the regular check
    [TestMethod]
    public async Task TestAGmdInstalledWithHomebrewIsOfferedAnUpdateButNotAutoUpdated()
    {
        var (updater, _, _, _) = NewUpdater(stable: "1.6.1444.870");

        Assert.IsTrue(AssertOk(await updater.IsUpdateAvailableAsync(Brewed)).IsAvailable);

        Assert.IsFalse(updater.IsToInstall(Brewed));
        Assert.IsTrue(updater.IsToInstall(Running));
    }

    // The tap has only the stable releases, so a preview would be offered and never come
    [TestMethod]
    public async Task TestAGmdInstalledWithHomebrewIsOfferedNoPreview()
    {
        var (updater, config, releases, _) = NewUpdater(stable: "1.6.1444.870");
        releases.Preview = ReleaseChoiceTest.NewRelease("1.6.1445.10", true, "gmd_linux_x64", "SHA256SUMS");
        config.Set(c => c.AllowPreview = true);

        Assert.AreEqual(new Version("1.6.1445.10"), AssertOk(await updater.IsUpdateAvailableAsync(Running)).Version);
        Assert.AreEqual(new Version("1.6.1444.870"), AssertOk(await updater.IsUpdateAvailableAsync(Brewed)).Version);
        Assert.IsFalse(config.Releases.IsPreview);
    }

    // Said before anything is downloaded, rather than as the 'access denied' of the download
    [TestMethod]
    public async Task TestAGmdInAFolderItCannotWriteToIsNotUpdated()
    {
        var (updater, _, _, installer) = NewUpdater(stable: "1.6.1444.870");
        installer.CanInstallResult = new Error("Access to the path is denied");

        var e = AssertError(await updater.UpdateAsync(Running));

        StringAssert.Contains(e.Message, "you cannot write to /home/anna/gmd");
        Assert.AreEqual(0, installer.Calls.Count);
    }

    // The binary, rather than the link on the PATH that started it: an update would replace the link
    // with a file, and Homebrew is told by where the binary is, in its Cellar
    [TestMethod]
    public void TestTheBinaryPathIsWhereALinkToItLeads()
    {
        if (OperatingSystem.IsWindows())
            return; // Making a link takes developer mode there
        var root = Directory.CreateTempSubdirectory("gmdTest-brew-").FullName;
        try
        {
            var keg = Directory.CreateDirectory(Path.Join(root, "Cellar", "gmd", "1.6.1444.518", "bin")).FullName;
            var binary = Path.Join(keg, "gmd");
            File.WriteAllText(binary, "gmd");
            var bin = Directory.CreateDirectory(Path.Join(root, "bin")).FullName;
            var link = Path.Join(bin, "gmd");
            File.CreateSymbolicLink(link, "../Cellar/gmd/1.6.1444.518/bin/gmd");

            Assert.AreEqual(binary, RunningGmd.BinaryPath(link));
            Assert.AreEqual(
                PackageManager.Homebrew,
                (Running with { Path = RunningGmd.BinaryPath(link) }).PackageManager
            );
            Assert.AreEqual(binary, RunningGmd.BinaryPath(binary));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void TestTheBinaryPathOfNoFileIsThePathGiven()
    {
        Assert.AreEqual("/no/such/folder/gmd", RunningGmd.BinaryPath("/no/such/folder/gmd"));
        Assert.IsNull(RunningGmd.BinaryPath(null));
    }

    static (Updater, Config, FakeGitHubReleases, FakeInstaller) NewUpdater(string stable)
    {
        var config = new FakeConfigService().Config;
        var releases = new FakeGitHubReleases(config)
        {
            Stable = ReleaseChoiceTest.NewRelease(stable, false, "gmd_linux_x64", "gmd_windows", "SHA256SUMS"),
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
        public Result CanInstallResult { get; set; } = Result.Ok;

        public Task<Result> InstallAsync(ReleaseBinary binary, string targetPath, CancellationToken cancel = default)
        {
            Calls.Add($"{binary.Version} {binary.Name} {binary.Url} {binary.ChecksumsUrl} {targetPath}");
            Cancel = cancel;
            return InstallTask();
        }

        // What the install was given to cancel it
        public CancellationToken Cancel { get; private set; }

        public Result CanInstall(string targetPath) => CanInstallResult;

        public void CleanUp(string targetPath) { }
    }
}
