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
    readonly Version buildVersion;

    bool isUpdateCheckerRunning = false;

    internal Updater(Config config, IGitHubReleases gitHubReleases, IBinaryInstaller installer)
    {
        this.config = config;
        this.gitHubReleases = gitHubReleases;
        this.installer = installer;
        buildVersion = Build.Version();
    }

    public async Task CheckUpdateAvailableAsync()
    {
        if (IsDotNet())
            return;

        installer.CleanUp(ThisPath);
        if (!config.CheckUpdates)
        {
            Log.Info("Check for updates is disabled");
            return;
        }
        if (await IsUpdateAvailableAsync() is Error e)
        {
            Log.Warn($"Failed to check remote version, {e}");
            return;
        }

        var releases = config.Releases;
        var allowPreview = config.AllowPreview;

        Log.Info(
            $"Running: {buildVersion}, Remote; Stable: {releases.StableRelease.Version}, Preview: {releases.PreRelease.Version}, allow preview: {allowPreview})"
        );
    }

    public async Task<Result<Version>> UpdateAsync()
    {
        if (IsDotNet())
            return buildVersion;

        var availableResult = await IsUpdateAvailableAsync();
        if (availableResult is not UpdateAvailability available)
        {
            Log.Warn($"Failed to check remote version, {availableResult.Error}");
            return availableResult.Error;
        }

        if (!available.IsAvailable)
        {
            Log.Info("Already at latest release");
            return buildVersion;
        }

        var release = SelectRelease();
        var binary = ReleaseChoice.Binary(release, BinaryNames());
        if (binary == null)
            return new Error("No binary available");

        if (await installer.InstallAsync(binary.Url, release.Version, ThisPath) is Error installError)
            return installError;

        return new Version(SelectRelease().Version);
    }

    public async Task StartCheckUpdatesRegularly()
    {
        if (Build.IsDevInstance())
        {
            Log.Info("Dev instance, no update check");
            return;
        }

        if (isUpdateCheckerRunning)
            return;
        isUpdateCheckerRunning = true;

        while (true)
        {
            await CheckUpdateAvailableAsync();

            if (config.CheckUpdates && config.AutoUpdate)
            {
                if (await IsUpdateAvailableAsync() is UpdateAvailability { IsAvailable: true })
                {
                    if (await UpdateAsync() is Version updatedVersion)
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
            }

            await Task.Delay(checkUpdateInterval);
        }
    }

    // Checks even when CheckUpdates is off, which turns off only the checks gmd makes by itself: an
    // update the user asks for, by --update or in the update dialog, is looked for
    public async Task<Result<UpdateAvailability>> IsUpdateAvailableAsync()
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
            return new UpdateAvailability(false, buildVersion);
        }

        if (!release.Assets.Any())
        {
            Log.Warn($"No remote binaries for {release.Version}");
            return new UpdateAvailability(false, buildVersion);
        }
        config.Set(s =>
        {
            s.Releases.LatestVersion = release.Version;
            s.Releases.IsPreview = release.IsPreview;
        });

        if (!config.Releases.IsUpdateAvailable())
        {
            Log.Debug("No new remote release available");
            return new UpdateAvailability(false, buildVersion);
        }
        Log.Info($"Update available, local {buildVersion} < {release.Version} remote (preview={release.IsPreview})");

        return new UpdateAvailability(true, new Version(release.Version));
    }

    Release SelectRelease() => ReleaseChoice.Select(config.Releases, config.AllowPreview);

    static IReadOnlyList<string> BinaryNames()
    {
        var os =
            Build.IsWindows ? OSPlatform.Windows
            : Build.IsMacOS ? OSPlatform.OSX
            : Build.IsLinux ? OSPlatform.Linux
            : OSPlatform.Create("other");
        return ReleaseChoice.BinaryNames(os, RuntimeInformation.OSArchitecture);
    }

    static string ThisPath => Environment.ProcessPath ?? "gmd";

    private static bool IsDotNet()
    {
        var thisPath = Environment.ProcessPath ?? "gmd";
        return Path.GetFileNameWithoutExtension(thisPath) == "dotnet";
    }
}
