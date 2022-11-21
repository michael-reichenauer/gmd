using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using gmd.Common;

namespace gmd.Installation;

interface IUpdater
{
    Task<bool> IsUpdateAvailableAsync();
}

public class GitRelease
{
    public string Tag_name { get; set; } = "";
    public bool Draft { get; set; } = true;
    public bool Prerelease { get; set; } = true;
    public string Published_at { get; set; } = "";
    public string Body { get; set; } = "";
    public GitAsset[] Assets { get; set; } = new GitAsset[0];
}

public class GitAsset
{
    public string Name { get; set; } = "";
    public int Download_count { get; set; } = 0;
    public string Browser_download_url { get; set; } = "";
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

    private readonly IState state;

    readonly Version buildVersion;

    internal Updater(IState state)
    {
        this.state = state;
        buildVersion = Util.GetBuildVersion();
    }


    public async Task<bool> IsUpdateAvailableAsync()
    {
        if (!Try(out var release, out var e, await GetRemoteInfoAsync()))
        {
            Log.Info($"Failed to get remote info, {e}");
            return false;
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

        if (!IsLeftNewer(release.Version, buildVersion.ToString()))
        {
            return false;
        }
        Log.Info($"Update available, local {buildVersion}<{release.Version} remote (preview={release.IsPreview})");

        return true;
    }

    Release SelectRelease()
    {
        var releases = state.Get().Releases;

        if (releases.AllowPreview && releases.PreRelease.Assets.Any() &&
            IsLeftNewer(releases.StableRelease.Version, releases.PreRelease.Version))
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
                    Log.Info("Remote latest version info same as cached info");
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

    string GetCachedLatestVersionInfoEtag() => state.Get().Releases.Etag;

    void CacheLatestVersionInfo(string eTag, string latestInfoText)
    {
        if (eTag == "") return;

        var gitReleases = JsonSerializer.Deserialize<GitRelease[]>(latestInfoText);
        var stable = gitReleases?.FirstOrDefault(rr => !rr.Prerelease);
        var preview = gitReleases?.FirstOrDefault(rr => rr.Prerelease);

        Releases releases = new Releases()
        {
            Etag = eTag,
            AllowPreview = state.Get().Releases.AllowPreview,
            StableRelease = ToRelease(stable),
            PreRelease = ToRelease(preview)
        };

        // Cache the latest version info
        state.Set(s => s.Releases = releases);
    }

    Release ToRelease(GitRelease? gr)
    {
        if (gr == null)
        {
            return new Release();
        }

        return new Release()
        {
            Version = gr.Tag_name.TrimPrefix("v"),
            IsPreview = gr.Prerelease,
            Assets = ToAssets(gr.Assets)
        };
    }

    Asset[] ToAssets(GitAsset[] gas) =>
        gas.Select(ga => new Asset() { Name = ga.Name, Url = ga.Browser_download_url }).ToArray();

    private static HttpClient GetHttpClient()
    {
        HttpClient httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("user-agent", UserAgent);
        return httpClient;
    }

    bool IsLeftNewer(string v1Text, string v2Text)
    {
        Version v1 = Version.Parse(v1Text);
        Version v2 = Version.Parse(v2Text);
        return v1 > v2;
    }
}