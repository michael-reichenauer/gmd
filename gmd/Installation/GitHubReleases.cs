using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using gmd.Common;

namespace gmd.Installation;

interface IGitHubReleases
{
    // Reads the latest stable release and the latest preview from GitHub into config.Releases,
    // unless GitHub says they are unchanged since they were last read
    Task<Result> FetchAsync();
}

// A release as GitHub's API gives it, the fields gmd reads
public class GitRelease
{
    public string tag_name { get; set; } = "";
    public bool draft { get; set; } = true;
    public bool prerelease { get; set; } = true;
    public string published_at { get; set; } = "";
    public string body { get; set; } = "";
    public GitAsset[] assets { get; set; } = [];
}

public class GitAsset
{
    public string name { get; set; } = "";
    public int download_count { get; set; } = 0;
    public string browser_download_url { get; set; } = "";
}

class GitHubReleases : IGitHubReleases
{
    const string releasesUri = "https://api.github.com/repos/michael-reichenauer/gmd/releases";
    static readonly Version MinVersion = new("0.0.0.0");

    readonly Config config;
    readonly IHttp http;

    internal GitHubReleases(Config config, IHttp http)
    {
        this.config = config;
        this.http = http;
    }

    public async Task<Result> FetchAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, releasesUri);

            // Try get cached information about latest remote version
            string eTag = config.Releases.Etag;
            if (eTag != "")
            {
                // There is cached information, lets use the ETag when checking to follow
                // GitHub Rate Limiting method.
                request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(eTag));
            }

            using var response = await http.Client.SendAsync(request);

            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                Log.Debug("Remote latest version info same as cached info");
                return Result.Ok;
            }

            string latestInfoText = await response.Content.ReadAsStringAsync();
            Log.Debug("New version info");

            if (response.Headers.ETag != null)
            {
                eTag = response.Headers.ETag.Tag;
                CacheLatestVersionInfo(eTag, latestInfoText);
            }

            return Result.Ok;
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            Log.Exception(e, "Failed to download latest setup");
            return new Error("Failed to download latest setup", e);
        }
    }

    void CacheLatestVersionInfo(string eTag, string latestInfoText)
    {
        if (eTag == "")
            return;

        // Cache the latest version info
        var latestReleases = Parse(latestInfoText, eTag);
        config.Set(s => s.Releases = latestReleases);
    }

    // The latest stable release and the latest preview of the list GitHub gives, latest by version,
    // since the list is in the order the releases were made
    internal static Releases Parse(string json, string eTag)
    {
        var gitReleases = JsonSerializer.Deserialize<GitRelease[]>(json);
        var releases = gitReleases?.OrderByDescending(rr => TagToVersion(rr.tag_name))?.ToList() ?? [];
        var stable = releases.FirstOrDefault(rr => !rr.prerelease);
        var preview = releases.FirstOrDefault(rr => rr.prerelease);

        return new Releases()
        {
            Etag = eTag,
            StableRelease = ToRelease(stable),
            PreRelease = ToRelease(preview),
        };
    }

    static Version TagToVersion(string tag)
    {
        if (Version.TryParse(tag?.TrimPrefix("v") ?? "", out var v))
        {
            return v;
        }
        return MinVersion;
    }

    static Release ToRelease(GitRelease? gr)
    {
        if (gr == null)
        {
            return new Release();
        }

        return new Release()
        {
            Version = gr.tag_name.TrimPrefix("v"),
            IsPreview = gr.prerelease,
            Assets = ToAssets(gr.assets),
        };
    }

    static Asset[] ToAssets(GitAsset[] gas) =>
        gas.Select(ga => new Asset() { Name = ga.name, Url = ga.browser_download_url }).ToArray();
}
