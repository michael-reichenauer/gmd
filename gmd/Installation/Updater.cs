using System.Net;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text.Json;
using gmd.Common;
using gmd.Cui.Common;

namespace gmd.Installation;

interface IUpdater
{
    Task CheckUpdateAvailableAsync();
    Task<R<Version>> UpdateAsync();
    Task<R<(bool, Version)>> IsUpdateAvailableAsync();
    Task CheckUpdatesRegularly();
}

public class GitRelease
{
    public string tag_name { get; set; } = "";
    public bool draft { get; set; } = true;
    public bool prerelease { get; set; } = true;
    public string published_at { get; set; } = "";
    public string body { get; set; } = "";
    public GitAsset[] assets { get; set; } = new GitAsset[0];
}

public class GitAsset
{
    public string name { get; set; } = "";
    public int download_count { get; set; } = 0;
    public string browser_download_url { get; set; } = "";
}


class Updater : IUpdater
{
    static readonly TimeSpan checkUpdateInterval = TimeSpan.FromHours(1);
    const string releasesUri = "https://api.github.com/repos/michael-reichenauer/gmd/releases";
    const string binNameWindows = "gmd_windows";
    const string binNameLinux = "gmd_linux";
    const string binNameMac = "gmd_mac";
    const string tmpSuffix = ".tmp";
    const string UserAgent = "gmd";
    const string tmpRandomSuffix = "RTXZERT";
    readonly Version MinVersion = new Version("0.0.0.0");

    readonly IState states;
    private readonly IConfig configs;
    readonly ICmd cmd;
    readonly Version buildVersion;

    // Data for download binary tasks to avoud multiple paralell tasks
    static string requestingUri = "";
    static Task<byte[]>? getBytesTask = null;

    internal Updater(IState states, IConfig configs, ICmd cmd)
    {
        this.states = states;
        this.configs = configs;
        this.cmd = cmd;
        buildVersion = Build.Version();
    }


    public async Task CheckUpdateAvailableAsync()
    {
        if (IsDotNet()) return;

        CleanTempFiles();
        if (!Try(out var isAvailable, out var e, await IsUpdateAvailableAsync()))
        {
            Log.Warn($"Failed to check remote version, {e}");
            return;
        }

        var releases = states.Get().Releases;
        var allowPreview = configs.Get().AllowPreview;

        Log.Info($"Running: {buildVersion}, Remote; Stable: {releases.StableRelease.Version}, Preview: {releases.PreRelease.Version}, allow preview: {allowPreview})");
    }


    public async Task<R<Version>> UpdateAsync()
    {
        if (IsDotNet()) return buildVersion;

        if (!Try(out var isAvailable, out var e, await IsUpdateAvailableAsync()))
        {
            Log.Warn($"Failed to check remote version, {e}");
            return e;
        }

        if (!isAvailable.Item1)
        {
            Log.Info("Already at latest release");
            return buildVersion;
        }

        if (!Try(out var downloadedPath, out e, await DownloadBinaryAsync()))
        {
            Log.Warn($"Failed to download new version, {e}");
            return e;
        }

        if (!Try(out e, Install(downloadedPath)))
        {
            Log.Warn($"Failed to install new version, {e}");
            return e;
        }

        var release = SelectRelease();
        return new Version(release.Version);
    }


    public async Task CheckUpdatesRegularly()
    {
        if (Build.IsDevInstance())
        {
            Log.Info("Dev instance, no update check");
            return;
        }

        while (true)
        {
            await CheckUpdateAvailableAsync();

            if (configs.Get().AutoUpdate)
            {
                if (Try(out var isAvailable, out var e, await IsUpdateAvailableAsync()) && isAvailable.Item1)
                {
                    if (Try(out var update, out e, await UpdateAsync()))
                    {
                        UI.Post(() =>
                        {
                            UI.InfoMessage("Restart for New Version ",
                                $"Gmd has been updated to: {update}\n" +
                                "and the new version will run at next starts.\n\n" +
                                "Restart gmd if you want to run the updated version now.");
                        });
                    }
                }
            }

            await Task.Delay(checkUpdateInterval);
        }
    }


    public async Task<R<(bool, Version)>> IsUpdateAvailableAsync()
    {
        if (!configs.Get().CheckUpdates)
        {
            Log.Info("Check for updates is disabled");
            return (false, buildVersion);
        }

        if (!Try(out var release, out var e, await GetRemoteInfoAsync()))
        {
            Log.Info($"Failed to get remote info, {e}");
            return R.Error($"Failed to get remote info, {e}");
        }

        if (release.Version == "")
        {
            Log.Info("No remote release available");
            return (false, buildVersion);
        }

        if (!release.Assets.Any())
        {
            Log.Warn($"No remote binaries for {release.Version}");
            return (false, buildVersion);
        }
        states.Set(s =>
        {
            s.Releases.LatestVersion = release.Version;
            s.Releases.IsPreview = release.IsPreview;
        });

        if (!IsLeftNewer(release.Version, buildVersion.ToString()))
        {
            Log.Debug("No new remote release available");
            states.Set(s => s.Releases.IsUpdateAvailable = false);
            return (false, buildVersion);
        }
        Log.Info($"Update available, local {buildVersion} < {release.Version} remote (preview={release.IsPreview})");
        states.Set(s => s.Releases.IsUpdateAvailable = true);

        return (true, new Version(release.Version));
    }


    R Install(string downloadedPath)
    {
        if (IsDotNet()) return R.Ok;

        try
        {
            // Move downloaded file next to this process file (replace must be on same volume)
            var newPath = GetTempPath();
            File.Move(downloadedPath, newPath);
            Log.Info($"Install {newPath} ...");
            if (!Try(out var e, MakeBinaryExecutable(newPath))) return e;

            var thisPath = Environment.ProcessPath ?? "gmd";
            var newThisPath = GetTempPath();

            File.Move(thisPath, newThisPath);
            Log.Info($"Moved {thisPath} to {newThisPath}");
            File.Move(newPath, thisPath);
            Log.Info($"Installed {newPath}");
            return R.Ok;
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            Log.Exception(e, "Failed install new file");
            return R.Error("Failed to install new file", e);
        }
    }


    async Task<R<string>> DownloadBinaryAsync()
    {
        try
        {
            using (HttpClient httpClient = GetHttpClient())
            {
                (string downloadUrl, string version) = SelectBinaryPath();
                if (downloadUrl == "")
                {
                    return R.Error("No binary available");
                }

                var targetPath = GetDownloadFilePath(version);
                if (Files.Exists(targetPath))
                {
                    Log.Info($"Already downloaded {targetPath}");
                    return targetPath;
                }

                byte[] remoteFileData = await GetByteArrayAsync(httpClient, downloadUrl);

                File.WriteAllBytes(targetPath, remoteFileData);

                Log.Info($"Downloaded to    {targetPath}");
                return targetPath;
            }
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            Log.Exception(e, "Failed to download latest binary");
            return R.Error("Failed to download latest binary", e);
        }
    }

    Task<byte[]> GetByteArrayAsync(HttpClient httpClient, string requestUri)
    {
        if (requestingUri == requestUri && getBytesTask != null)
        {   // A request for this uri has already been started, lets reuse task
            Log.Info($"Download already started for {requestUri}");
            return getBytesTask;
        }

        // Start download task and remember task in case multiple requests for same request
        Log.Info($"Downloading from {requestUri} ...");
        requestingUri = requestUri;
        getBytesTask = httpClient.GetByteArrayAsync(requestUri);
        return getBytesTask;
    }


    string GetDownloadFilePath(string version)
    {
        var name = $"gmd.{version}.{tmpRandomSuffix}";
        var tmpFolderPath = Path.GetTempPath();
        return Path.Combine(tmpFolderPath, name);
    }


    string GetTempPath()
    {
        var thisPath = Environment.ProcessPath ?? "gmd";

        return $"{thisPath}.tmp_{RandomString(5)}";
    }

    void CleanTempFiles()
    {
        if (IsDotNet()) return;
        try
        {
            var thisPath = Environment.ProcessPath ?? "gmd";
            var tmpPathPrefix = thisPath + ".tmp";

            foreach (var path in Directory.GetFiles(Path.GetDirectoryName(thisPath) ?? ""))
            {
                if (path.StartsWith(tmpPathPrefix))
                {
                    Log.Info($"Deleting {path}");
                    if (!Try(out var e, Files.Delete(path))) Log.Info($"Failed to delete {e}");
                }
            }
        }
        catch (Exception e)
        {
            Log.Info($"Failed to clean {e.Message}");
        }
    }


    (string, string) SelectBinaryPath()
    {
        string name = "";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            name = "gmd_osx";
        }
        else
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            name = "gmd_linux";
        }
        else
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            name = "gmd_windows";
        }
        else
        {
            return ("", "");
        }

        var release = SelectRelease();
        var binaryPath = release.Assets.FirstOrDefault(a => a.Name == name)?.Url ?? "";
        Log.Debug($"{release.Version} Preview: {release.IsPreview}, {binaryPath}");

        return (binaryPath, release.Version);
    }


    Release SelectRelease()
    {
        var releases = states.Get().Releases;
        var allowPreview = configs.Get().AllowPreview;

        if (allowPreview && releases.PreRelease.Assets.Any() &&
            IsLeftNewer(releases.PreRelease.Version, releases.StableRelease.Version))
        {   // user allow preview versions, and the preview version is newer
            return releases.PreRelease;
        }
        return releases.StableRelease;
    }

    async Task<R<Release>> GetRemoteInfoAsync()
    {
        try
        {
            using (HttpClient httpClient = GetHttpClient())
            {
                // Try get cached information about latest remote version
                string eTag = GetCachedLatestVersionInfoEtag();

                if (eTag != "")
                {
                    // There is cached information, lets use the ETag when checking to follow
                    // GitHub Rate Limiting method.
                    httpClient.DefaultRequestHeaders.IfNoneMatch.Clear();
                    httpClient.DefaultRequestHeaders.IfNoneMatch.Add(new EntityTagHeaderValue(eTag));
                }

                HttpResponseMessage response = await httpClient.GetAsync(releasesUri);

                if (response.StatusCode == HttpStatusCode.NotModified || response.Content == null)
                {
                    Log.Debug("Remote latest version info same as cached info");
                    return SelectRelease();
                }

                string latestInfoText = await response.Content.ReadAsStringAsync();
                Log.Debug("New version info");

                if (response.Headers.ETag != null)
                {
                    eTag = response.Headers.ETag.Tag;
                    CacheLatestVersionInfo(eTag, latestInfoText);
                }

                return SelectRelease();
            }
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            Log.Exception(e, "Failed to download latest setup");
            return R.Error("Failed to download latest setup", e);
        }
    }

    string GetCachedLatestVersionInfoEtag() => states.Get().Releases.Etag;

    void CacheLatestVersionInfo(string eTag, string latestInfoText)
    {
        if (eTag == "") return;

        var gitReleases = JsonSerializer.Deserialize<GitRelease[]>(latestInfoText);
        var releases = gitReleases?.OrderByDescending(rr => TagToVersion(rr.tag_name))?.ToList() ?? new List<GitRelease>();
        var stable = releases.FirstOrDefault(rr => !rr.prerelease);
        var preview = releases.FirstOrDefault(rr => rr.prerelease);

        Releases latestReleases = new Releases()
        {
            Etag = eTag,
            StableRelease = ToRelease(stable),
            PreRelease = ToRelease(preview)
        };

        // Cache the latest version info
        states.Set(s => s.Releases = latestReleases);
    }

    Version TagToVersion(string tag)
    {
        if (Version.TryParse(tag?.TrimPrefix("v") ?? "", out var v))
        {
            return v;
        }
        return MinVersion;
    }

    Release ToRelease(GitRelease? gr)
    {
        if (gr == null)
        {
            return new Release();
        }

        return new Release()
        {
            Version = gr.tag_name.TrimPrefix("v"),
            IsPreview = gr.prerelease,
            Assets = ToAssets(gr.assets)
        };
    }

    Asset[] ToAssets(GitAsset[] gas) =>
        gas.Select(ga => new Asset() { Name = ga.name, Url = ga.browser_download_url }).ToArray();

    private static HttpClient GetHttpClient()
    {
        HttpClient httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("user-agent", UserAgent);
        return httpClient;
    }

    bool IsLeftNewer(string v1Text, string v2Text)
    {
        if (!Version.TryParse(v1Text, out var v1))
        {
            return false;
        }
        if (!Version.TryParse(v2Text, out var v2))
        {
            return true;
        }
        return v1 > v2;
    }

    private Random random = new Random();

    private string RandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    R MakeBinaryExecutable(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Not needed on windows
            return R.Ok;
        }

        return cmd.Run("chmod", $"+x {path}", "");
    }

    private bool IsDotNet()
    {
        var thisPath = Environment.ProcessPath ?? "gmd";
        return Path.GetFileNameWithoutExtension(thisPath) == "dotnet";
    }
}