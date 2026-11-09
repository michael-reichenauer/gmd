using gmd.Cui.Common;
using gmd.Cui.Graphs;
using gmd.Cui.WebLog;
using gmd.Git;
using gmdTest.Fixtures;

namespace gmdTest.Cui.WebLog;

[TestClass]
public class WebLogPageTest
{
    static async Task<string> PageOf(RepoBuilder builder, IReadOnlyList<PullRequest>? prs = null, int maxRows = 100)
    {
        var repo = await builder.ViewRepoAsync(gmd.Server.ShowBranches.AllActive);
        var colors = new BranchColorService(new FakeRepoConfig());
        var graph = new GraphCreater(colors).Create(repo);
        return WebLogPage.Html(
            repo,
            graph,
            new GraphWriter(),
            b => colors.GetColor(repo, b),
            WebLinks.From("git@github.com:owner/repo.git"),
            prs ?? [],
            "repo Branch Graph",
            new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc),
            maxRows
        );
    }

    static RepoBuilder FeatureRepo() =>
        new RepoBuilder()
            .Commit("f2", "More feature work", "f1")
            .Commit("c3", "Merge pull request #7 from owner/feature", "c2", "f1")
            .Commit("f1", "Feature <work>", "c1")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c3", isCurrent: true)
            .BranchWithRemote("feature", "f2");

    [TestMethod]
    public async Task LinksCommitsBranchesAndPullRequests()
    {
        var prs = new List<PullRequest>
        {
            new(8, "The feature", "feature", "https://github.com/owner/repo/pull/8", false),
        };
        var html = await PageOf(FeatureRepo(), prs);

        StringAssert.Contains(
            html,
            $"href=\"https://github.com/owner/repo/commit/{RepoBuilder.Sha("c2")}\">Second</a>"
        );
        StringAssert.Contains(html, "href=\"https://github.com/owner/repo/pull/7\">Merge pull request #7");
        StringAssert.Contains(html, "href=\"https://github.com/owner/repo/pull/8\" title=\"The feature\">#8</a>");
        StringAssert.Contains(html, "href=\"https://github.com/owner/repo/tree/main\">main</a>");
        // The subject is text, never markup
        StringAssert.Contains(html, "Feature &lt;work&gt;");
    }

    [TestMethod]
    public async Task ListsBranchesLatestFirst()
    {
        var html = await PageOf(FeatureRepo());

        var branches = html[html.IndexOf("<section class=\"branches\">")..html.IndexOf("<section class=\"log\">")];
        var feature = branches.IndexOf(">feature</a>");
        var main = branches.IndexOf(">main</a>");
        Assert.IsTrue(feature >= 0 && main > feature, branches);
    }

    [TestMethod]
    public async Task DrawsARowPerCommitUpToTheMax()
    {
        var html = await PageOf(FeatureRepo(), maxRows: 2);

        Assert.AreEqual(2, html.Split("<div class=\"r\"").Length - 1);
        StringAssert.Contains(html, "… and 3 older commits");
    }

    [TestMethod]
    public async Task LeavesOutPullRequestsFromForks()
    {
        var prs = new List<PullRequest>
        {
            new(9, "A fork's main", "main", "https://github.com/owner/repo/pull/9", false, IsCrossRepository: true),
        };
        var html = await PageOf(FeatureRepo(), prs);

        Assert.IsFalse(html.Contains("/pull/9"), html);
    }

    [TestMethod]
    public async Task LinksABranchOlderThanTheCommitsDrawnToItsPage()
    {
        var html = await PageOf(FeatureRepo(), maxRows: 1);

        StringAssert.Contains(
            html,
            "href=\"https://github.com/owner/repo/tree/main\"><span class=\"dot\"></span>main</a>"
        );
        StringAssert.Contains(html, "href=\"#f2000000\"><span class=\"dot\"></span>feature</a>");
    }

    [TestMethod]
    public async Task TitlesThePageAndShowsEverySubjectAlike()
    {
        var html = await PageOf(FeatureRepo());

        StringAssert.Contains(html, "<title>repo Branch Graph</title>");
        StringAssert.Contains(html, "<h1><a href=\"https://github.com/owner/repo\">repo Branch Graph</a></h1>");
        StringAssert.Contains(html, "<a class=\"subj\" href=\"https://github.com/owner/repo/pull/7\">");
        Assert.IsFalse(html.Contains("prefers-color-scheme"), "The page is dark whatever the device's mode");
    }

    [TestMethod]
    public async Task LinksATagToItsRelease()
    {
        var html = await PageOf(FeatureRepo().Tag("v1.2", "c3"));

        StringAssert.Contains(
            html,
            "<a class=\"tag\" href=\"https://github.com/owner/repo/releases/tag/v1.2\">v1.2</a>"
        );
    }

    [TestMethod]
    public async Task PutsTheSubjectBesideTheCommitsDot()
    {
        var html = await PageOf(FeatureRepo());

        // The two lines of text are moved down by half a line from the middle of the row
        StringAssert.Contains(html, "line-height:18px;position:relative;top:9px}");
    }
}
