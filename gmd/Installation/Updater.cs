using System.Runtime.InteropServices;
using gmd.Common;
using gmd.Cui.Common;

namespace gmd.Installation;

// Whether a newer release exists, and its version (the running one when there is none)
record UpdateAvailability(bool IsAvailable, Version Version);

interface IUpdater
{
    Task CheckUpdateAvailableAsync();
    Task<Result<Version>> UpdateAsync();
    Task<Result<UpdateAvailability>> IsUpdateAvailableAsync();
    Task StartCheckUpdatesRegularly();
}

// The gmd that is running, as far as updating it goes: its version, the path of its binary, which
// an update replaces, whether it is a dev instance, which is never offered an update, and the names
// of the release assets that run here. Handed to the Updater rather than read by it, so that the
// tests can be any gmd, the test process being a dotnet host.
record RunningGmd(Version Version, string? Path, bool IsDevInstance, IReadOnlyList<string> BinaryNames)
{
    public static RunningGmd Current { get; } =
        new(
            Build.Version(),
            Environment.ProcessPath,
            Build.IsDevInstance(),
            ReleaseChoice.BinaryNames(CurrentOs, RuntimeInformation.OSArchitecture)
        );

    // Run by 'dotnet gmd.dll', as the debugger does
    public bool IsDotNetHost => System.IO.Path.GetFileNameWithoutExtension(Path ?? "") == "dotnet";

    static OSPlatform CurrentOs =>
        Build.IsWindows ? OSPlatform.Windows
        : Build.IsMacOS ? OSPlatform.OSX
        : Build.IsLinux ? OSPlatform.Linux
        : OSPlatform.Create("other");
}

// Keeps gmd up to date with its releases on GitHub. Which release there is to update to is read
// from GitHub (GitHubReleases) and chosen (ReleaseChoice); updating replaces the binary of the
// running gmd (BinaryInstaller), which goes on running, and the new version runs once gmd is
// started again.
[SingleInstance]
class Updater : IUpdater
{
    static readonly TimeSpan checkUpdateInterval = TimeSpan.FromHours(1);

    readonly Config config;
    readonly IGitHubReleases gitHubReleases;
    readonly IBinaryInstaller installer;

    readonly object updateLock = new();
    Task<Result<Version>>? update; // The update in progress, which a second one waits for
    Version? installedVersion; // Installed by this gmd, and run once gmd is started again
    bool isUpdateCheckerRunning = false;

    internal Updater(Config config, IGitHubReleases gitHubReleases, IBinaryInstaller installer)
    {
        this.config = config;
        this.gitHubReleases = gitHubReleases;
        this.installer = installer;
    }

    public Task CheckUpdateAvailableAsync() => CheckUpdateAvailableAsync(RunningGmd.Current);

    public Task<Result<Version>> UpdateAsync() => UpdateAsync(RunningGmd.Current);

    public Task StartCheckUpdatesRegularly() => StartCheckUpdatesRegularly(RunningGmd.Current);

    public Task<Result<UpdateAvailability>> IsUpdateAvailableAsync() => IsUpdateAvailableAsync(RunningGmd.Current);

    internal async Task CheckUpdateAvailableAsync(RunningGmd self)
    {
        if (self.IsDotNetHost)
            return;

        // A dev instance never installs anything to clean up after
        if (!self.IsDevInstance && self.Path is string path)
            installer.CleanUp(path);
        if (!config.CheckUpdates)
        {
            Log.Info("Check for updates is disabled");
            return;
        }
        if (await IsUpdateAvailableAsync(self) is Error e)
        {
            Log.Warn($"Failed to check remote version, {e}");
            return;
        }

        var releases = config.Releases;
        var allowPreview = config.AllowPreview;

        Log.Info(
            $"Running: {self.Version}, Remote; Stable: {releases.StableRelease.Version}, Preview: {releases.PreRelease.Version}, allow preview: {allowPreview})"
        );
    }

    // One at a time: the update dialog and the regular check can both start one, and a second one
    // waits for the first rather than download and install the same binary again beside it
    internal Task<Result<Version>> UpdateAsync(RunningGmd self)
    {
        lock (updateLock)
        {
            if (update is not { IsCompleted: false })
                update = UpdateOnceAsync(self);
            return update;
        }
    }

    async Task<Result<Version>> UpdateOnceAsync(RunningGmd self)
    {
        // The binary of a dev instance is a build, e.g. gmd/bin/Debug, which is never replaced by a
        // release. It is never offered one either, but a guard there was all that stood between an
        // update and a build of gmd that a developer runs.
        if (self.IsDevInstance)
            return new Error("A dev instance of gmd is not updated");

        // Unknown is rare, but taken to be 'gmd', it was a file in the current folder, a repository
        if (self.Path is not string path)
            return new Error("The path of the gmd binary is not known, so it cannot be replaced");

        var availableResult = await IsUpdateAvailableAsync(self);
        if (availableResult is not UpdateAvailability available)
        {
            Log.Warn($"Failed to check remote version, {availableResult.Error}");
            return availableResult.Error;
        }

        if (!available.IsAvailable)
        {
            Log.Info("Already at latest release");
            return self.Version;
        }

        // The running gmd is still the old version, so the new one is still newer, but it is in place
        if (installedVersion != null && installedVersion >= available.Version)
        {
            Log.Info($"Already installed {installedVersion}, which runs once gmd is started again");
            return installedVersion;
        }

        var release = SelectRelease();
        var binary = ReleaseChoice.Binary(release, self.BinaryNames);
        if (binary == null || !Version.TryParse(release.Version, out var version))
        {
            Log.Warn($"Failed to download new version, no binary for this platform in {release.Version}");
            return new Error("No binary available");
        }

        var checksums = ReleaseChoice.Checksums(release);
        var releaseBinary = new ReleaseBinary(version, binary.Name, binary.Url, checksums?.Url ?? "");
        if (await installer.InstallAsync(releaseBinary, path) is Error installError)
            return installError;

        installedVersion = version;
        return version;
    }

    async Task StartCheckUpdatesRegularly(RunningGmd self)
    {
        if (self.IsDevInstance)
        {
            Log.Info("Dev instance, no update check");
            return;
        }

        if (isUpdateCheckerRunning)
            return;
        isUpdateCheckerRunning = true;

        while (true)
        {
            await CheckUpdateAvailableAsync(self);

            if (config.CheckUpdates && config.AutoUpdate && IsToInstall(self))
            {
                if (await UpdateAsync(self) is Version updatedVersion)
                {
                    UI.Post(() =>
                    {
                        UI.InfoMessage(
                            "Restart for New Version ",
                            $"Gmd has been updated to: {updatedVersion}\n"
                                + "and the new version will run at next starts.\n\n"
                                + "Restart gmd if you want to run the updated version now."
                        );
                    });
                }
            }

            await Task.Delay(checkUpdateInterval);
        }
    }

    // Checks even when CheckUpdates is off, which turns off only the checks gmd makes by itself: an
    // update the user asks for, by --update or in the update dialog, is looked for
    internal async Task<Result<UpdateAvailability>> IsUpdateAvailableAsync(RunningGmd self)
    {
        if (await gitHubReleases.FetchAsync() is Error fetchError)
        {
            Log.Info($"Failed to get remote info, {fetchError}");
            return new Error($"Failed to get remote info, {fetchError}");
        }

        var release = SelectRelease();
        if (release.Version == "")
        {
            Log.Info("No remote release available");
            return new UpdateAvailability(false, self.Version);
        }

        if (!release.Assets.Any())
        {
            Log.Warn($"No remote binaries for {release.Version}");
            return new UpdateAvailability(false, self.Version);
        }
        config.Set(s =>
        {
            s.Releases.LatestVersion = release.Version;
            s.Releases.IsPreview = release.IsPreview;
        });

        // As Releases.IsUpdateAvailable(), which the UI asks, but of the gmd handed in
        if (self.IsDevInstance || !Version.TryParse(release.Version, out var latest) || latest <= self.Version)
        {
            Log.Debug("No new remote release available");
            return new UpdateAvailability(false, self.Version);
        }
        Log.Info($"Update available, local {self.Version} < {release.Version} remote (preview={release.IsPreview})");

        return new UpdateAvailability(true, latest);
    }

    // Whether the release the last check found is newer than the running gmd, and than the version
    // it installed already, which it would otherwise install again at every check until restarted
    bool IsToInstall(RunningGmd self) =>
        Version.TryParse(config.Releases.LatestVersion, out var latest)
        && latest > self.Version
        && (installedVersion == null || latest > installedVersion);

    Release SelectRelease() => ReleaseChoice.Select(config.Releases, config.AllowPreview);
}
