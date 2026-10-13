using System.Net;
using gmd.Common;
using gmd.Installation;
using gmdTest.Fixtures;

namespace gmdTest.Installation;

// Reading the releases from GitHub's API: the latest stable release and the latest preview, and
// the ETag that lets GitHub answer "unchanged" rather than send the list again
[TestClass]
public class GitHubReleasesTest
{
    // As GitHub lists them, in the order they were made, which is not the order of their versions:
    // the release from main is made after the preview from dev that was merged into it, and an old
    // preview with a tag that is no version sorts last
    const string ReleasesJson = """
        [
          {
            "tag_name": "v1.6.1444.518",
            "prerelease": false,
            "draft": false,
            "assets": [
              { "name": "gmd_linux_x64", "browser_download_url": "https://github.com/x/v1.6.1444.518/gmd_linux_x64" },
              { "name": "SHA256SUMS", "browser_download_url": "https://github.com/x/v1.6.1444.518/SHA256SUMS" }
            ]
          },
          {
            "tag_name": "v1.6.1444.870",
            "prerelease": true,
            "assets": [ { "name": "gmd_linux_x64", "browser_download_url": "https://github.com/x/v1.6.1444.870/gmd_linux_x64" } ]
          },
          { "tag_name": "preview-x", "prerelease": true, "assets": [] },
          { "tag_name": "v1.6.1444.781", "prerelease": true, "assets": [] },
          { "tag_name": "v1.5.1444.482", "prerelease": false, "assets": [] }
        ]
        """;

    [TestMethod]
    public void TestTheLatestStableReleaseAndTheLatestPreview()
    {
        var releases = GitHubReleases.Parse(ReleasesJson, "\"abc\"");

        Assert.AreEqual("\"abc\"", releases.Etag);
        Assert.AreEqual("1.6.1444.518", releases.StableRelease.Version);
        Assert.IsFalse(releases.StableRelease.IsPreview);
        Assert.AreEqual("1.6.1444.870", releases.PreRelease.Version);
        Assert.IsTrue(releases.PreRelease.IsPreview);
        Assert.AreEqual(
            "gmd_linux_x64 https://github.com/x/v1.6.1444.518/gmd_linux_x64, SHA256SUMS https://github.com/x/v1.6.1444.518/SHA256SUMS",
            string.Join(", ", releases.StableRelease.Assets.Select(a => $"{a.Name} {a.Url}"))
        );
    }

    [TestMethod]
    public void TestNoReleasesAtAll()
    {
        var releases = GitHubReleases.Parse("[]", "\"abc\"");

        Assert.AreEqual("", releases.StableRelease.Version);
        Assert.AreEqual("", releases.PreRelease.Version);
    }

    [TestMethod]
    public async Task TestTheReleasesReadAreKeptWithTheirETag()
    {
        var config = new FakeConfigService().Config;
        var http = new FakeHttp(_ => FakeHttp.Text(ReleasesJson, eTag: "\"abc\""));

        AssertOk(await new GitHubReleases(config, http).FetchAsync());

        Assert.AreEqual("", http.Calls[0].IfNoneMatch);
        Assert.AreEqual("\"abc\"", config.Releases.Etag);
        Assert.AreEqual("1.6.1444.518", config.Releases.StableRelease.Version);
        Assert.AreEqual("1.6.1444.870", config.Releases.PreRelease.Version);
    }

    // Asked with the ETag of the releases kept, GitHub answers 304 when they are unchanged, which
    // keeps the check within GitHub's rate limit, and the releases kept are the ones to use
    [TestMethod]
    public async Task TestUnchangedReleasesAreNotReadAgain()
    {
        var config = new FakeConfigService(c => c.Releases = GitHubReleases.Parse(ReleasesJson, "\"abc\"")).Config;
        var http = new FakeHttp(_ => FakeHttp.Status(HttpStatusCode.NotModified));

        AssertOk(await new GitHubReleases(config, http).FetchAsync());

        Assert.AreEqual("\"abc\"", http.Calls[0].IfNoneMatch);
        Assert.AreEqual("\"abc\"", config.Releases.Etag);
        Assert.AreEqual("1.6.1444.518", config.Releases.StableRelease.Version);
    }

    // They were dropped, and the ones read before taken for the latest, with no error
    [TestMethod]
    public async Task TestReleasesWithNoETagAreKeptToo()
    {
        var config = new FakeConfigService(c => c.Releases = GitHubReleases.Parse("[]", "\"old\"")).Config;
        var http = new FakeHttp(_ => FakeHttp.Text(ReleasesJson));

        AssertOk(await new GitHubReleases(config, http).FetchAsync());

        Assert.AreEqual("", config.Releases.Etag);
        Assert.AreEqual("1.6.1444.518", config.Releases.StableRelease.Version);
    }

    // GitHub's rate limit is 60 requests an hour for one address, which a team behind one address can
    // reach, and the body is then no release list
    [TestMethod]
    public async Task TestAnErrorStatusIsAnError()
    {
        var config = new FakeConfigService(c => c.Releases = GitHubReleases.Parse(ReleasesJson, "\"abc\"")).Config;
        var http = new FakeHttp(_ =>
            FakeHttp.Status(HttpStatusCode.Forbidden, """{"message": "API rate limit exceeded"}""")
        );

        var e = AssertError(await new GitHubReleases(config, http).FetchAsync());

        StringAssert.Contains(e.AllMessages(), "403");
        Assert.AreEqual("1.6.1444.518", config.Releases.StableRelease.Version);
    }

    [TestMethod]
    public async Task TestNoConnectionIsAnError()
    {
        var config = new FakeConfigService().Config;
        var http = new FakeHttp(_ => throw new HttpRequestException("No such host is known"));

        var e = AssertError(await new GitHubReleases(config, http).FetchAsync());

        StringAssert.Contains(e.AllMessages(), "No such host is known");
    }
}
