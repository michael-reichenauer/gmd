using System.Net;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text.Json;
using gmd.Common;

namespace gmd.Installation;

interface IUpdater
{
    Task CheckUpdateAvailableAsync();
    Task<R<Version>> UpdateAsync();
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

    private readonly IStates states;
    private readonly ICmd cmd;
    readonly Version buildVersion;

    internal Updater(IStates states, ICmd cmd)
    {
        this.states = states;
        this.cmd = cmd;
        buildVersion = Build.Version();
    }

    internal Updater()
        : this(new States(), new Cmd()) { Log.Info("internal !!!!!!!!!!!!"); }

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
        if (releases.AllowPreview)
        {
            Log.Info($"Running: {buildVersion}, PreRelase: {releases.PreRelease.Version} (Stable: {releases.StableRelease.Version})");
        }
        else
        {
            Log.Info($"Running: {buildVersion}, Stable: {releases.StableRelease.Version} (Preview: {releases.PreRelease.Version})");
        }
    }

    public async Task<R<Version>> UpdateAsync()
    {
        if (IsDotNet()) return buildVersion;

        if (!Try(out var isAvailable, out var e, await IsUpdateAvailableAsync()))
        {
            Log.Warn($"Failed to check remote version, {e}");
            return e;
        }

        if (!isAvailable)
        {
            Log.Info("Already at latest release");
            return buildVersion;
        }

        if (!Try(out var newPath, out e, await DownloadBinaryAsync()))
        {
            Log.Warn($"Failed to download new version, {e}");
            return e;
        }

        if (!Try(out e, Install(newPath)))
        {
            Log.Warn($"Failed to install new version, {e}");
            return e;
        }

        var release = SelectRelease();
        return new Version(release.Version);
    }


    private R Install(string newPath)
    {
        if (IsDotNet()) return R.Ok;

        try
        {
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

    async Task<R<bool>> IsUpdateAvailableAsync()
    {
        if (!Try(out var release, out var e, await GetRemoteInfoAsync()))
        {
            Log.Info($"Failed to get remote info, {e}");
            return R.Error($"Failed to get remote info, {e}");
        }

        if (release.Version == "")
        {
            Log.Info("No remote release available");
            return false;
        }

        if (!release.Assets.Any())
        {
            Log.Warn($"No remote binaries for {release.Version}");
            return false;
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
            return false;
        }
        Log.Info($"Update available, local {buildVersion} < {release.Version} remote (preview={release.IsPreview})");
        states.Set(s => s.Releases.IsUpdateAvailable = true);

        return true;
    }


    async Task<R<string>> DownloadBinaryAsync()
    {
        try
        {
            using (HttpClient httpClient = GetHttpClient())
            {
                string downloadUrl = SelectBinaryPath();
                if (downloadUrl == "")
                {
                    return R.Error("No binay available");
                }

                var targetPath = GetTempPath();

                Log.Info($"Downloading from {downloadUrl} ...");

                byte[] remoteFileData = await httpClient.GetByteArrayAsync(downloadUrl);

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
                    if (!Try(out var e, Files.Delete(path))) Log.Info($"Failed to deleter {e}");
                }
            }
        }
        catch (Exception e)
        {
            Log.Info($"Failed to clean {e.Message}");
        }
    }


    string SelectBinaryPath()
    {
        string name = "";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            name = "gmd_mac";
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
            return "";
        }

        var release = SelectRelease();
        var binaryPath = release.Assets.FirstOrDefault(a => a.Name == name)?.Url ?? "";
        Log.Info($"{release.Version} Preview: {release.IsPreview}, {binaryPath}");

        return binaryPath;
    }


    Release SelectRelease()
    {
        var releases = states.Get().Releases;

        if (releases.AllowPreview && releases.PreRelease.Assets.Any() &&
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
        var stable = gitReleases?.FirstOrDefault(rr => !rr.prerelease);
        var preview = gitReleases?.FirstOrDefault(rr => rr.prerelease);

        Releases releases = new Releases()
        {
            Etag = eTag,
            AllowPreview = states.Get().Releases.AllowPreview,
            StableRelease = ToRelease(stable),
            PreRelease = ToRelease(preview)
        };

        // Cache the latest version info
        states.Set(s => s.Releases = releases);
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