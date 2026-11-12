using gmd.Cui.Common;
using gmd.Cui.Graphs;
using gmd.Cui.WebLog;
using gmd.Git;
using gmdTest.Fixtures;

namespace gmdTest.Cui.WebLog;

[TestClass]
public class WebLogPageTest
{
    static async Task<string> PageOf(
        RepoBuilder builder,
        IReadOnlyList<PullRequest>? prs = null,
        int maxRows = 100,
        string? remote = "git@github.com:owner/repo.git"
    )
    {
        var repo = await builder.ViewRepoAsync(gmd.Server.ShowBranches.AllActive);
        var colors = new BranchColorService(new FakeRepoConfig());
        var graph = new GraphCreater(colors).Create(repo);
        return WebLogPage.Html(
            repo,
            graph,
            new GraphWriter(),
            b => colors.GetColor(repo, b),
            remote != null ? WebLinks.From(remote) : null,
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

    // A branch with its tip below the last commit drawn has no line in the graph, so it is not
    // listed either
    [TestMethod]
    public async Task ListsOnlyTheBranchesOfTheCommitsDrawn()
    {
        var html = await PageOf(FeatureRepo(), maxRows: 1);

        var branches = html[html.IndexOf("<section class=\"branches\">")..html.IndexOf("<section class=\"log\">")];
        StringAssert.Contains(branches, "href=\"#f2000000\"><span class=\"dot\"></span>feature</a>");
        Assert.IsFalse(branches.Contains(">main</a>"), branches);
        Assert.AreEqual(1, branches.Split("<li>").Length - 1, branches);

        // With both tips drawn, both are listed, each going to its tip
        var all = await PageOf(FeatureRepo(), maxRows: 2);
        StringAssert.Contains(all, "href=\"#c3000000\"><span class=\"dot\"></span>main</a>");
        StringAssert.Contains(all, "href=\"#f2000000\"><span class=\"dot\"></span>feature</a>");
    }

    // The commits not pushed are the local branch's, drawn above the tip origin has, so the branch
    // is listed while any of them is shown, and goes to the latest
    [TestMethod]
    public async Task ListsABranchWithOnlyCommitsNotPushedDrawn()
    {
        var html = await PageOf(
            new RepoBuilder()
                .Commit("c2", "Not pushed", "c1")
                .Commit("c1", "Initial")
                .BranchWithRemote("main", "c2", isCurrent: true, remoteTipCommit: "c1", ahead: 1),
            maxRows: 1
        );

        Assert.AreEqual(1, html.Split("<div class=\"r\"").Length - 1);
        StringAssert.Contains(html, "href=\"#c2000000\"><span class=\"dot\"></span>main</a>");
    }

    [TestMethod]
    public async Task TitlesThePageAndShowsEverySubjectAlike()
    {
        var html = await PageOf(FeatureRepo());

        StringAssert.Contains(html, "<title>repo Branch Graph</title>");
        StringAssert.Contains(
            html,
            "<h1><a target=\"gmd-repo\" href=\"https://github.com/owner/repo\">repo Branch Graph</a></h1><nav"
        );
        StringAssert.Contains(
            html,
            "<a target=\"gmd-pr\" class=\"subj\" href=\"https://github.com/owner/repo/pull/7\">"
        );
        Assert.IsFalse(html.Contains("prefers-color-scheme"), "The page is dark whatever the device's mode");
    }

    [TestMethod]
    public async Task HasShortcutsToTheRepositoryItsPullRequestsItsActionsAndItsFilesInVsCode()
    {
        var html = await PageOf(FeatureRepo());

        StringAssert.Contains(
            html,
            "<nav class=\"site\"><a target=\"gmd-repo\" href=\"https://github.com/owner/repo\">GitHub</a>"
                + "<a target=\"gmd-pr\" href=\"https://github.com/owner/repo/pulls\">Pull requests</a>"
                + "<a target=\"gmd-runs\" href=\"https://github.com/owner/repo/actions\">Actions</a>"
                + "<a target=\"gmd-editor\" href=\"https://vscode.dev/github/owner/repo\">VS Code</a></nav>"
        );
    }

    [TestMethod]
    public async Task NamesTheShortcutsAsTheHostDoesAndLeavesOutWhatItHasNot()
    {
        var gitLab = await PageOf(FeatureRepo(), remote: "git@gitlab.com:group/repo.git");
        StringAssert.Contains(
            gitLab,
            "<nav class=\"site\"><a target=\"gmd-repo\" href=\"https://gitlab.com/group/repo\">GitLab</a>"
                + "<a target=\"gmd-pr\" href=\"https://gitlab.com/group/repo/-/merge_requests\">Merge requests</a>"
                + "<a target=\"gmd-runs\" href=\"https://gitlab.com/group/repo/-/pipelines\">Pipelines</a></nav>"
        );

        var unknown = await PageOf(FeatureRepo(), remote: "git@git.example.com:team/repo.git");
        StringAssert.Contains(
            unknown,
            "<nav class=\"site\"><a target=\"gmd-repo\" href=\"https://git.example.com/team/repo\">git.example.com</a></nav>"
        );

        var noRemote = await PageOf(FeatureRepo(), remote: null);
        Assert.IsFalse(noRemote.Contains("<nav"), "No shortcuts with no remote");
        StringAssert.Contains(noRemote, "<div class=\"top\"><h1>repo Branch Graph</h1></div>");
    }

    // A link to another site opens in the tab of its kind of page, which the next link of that kind
    // is shown in, so that the graph stays open and ten commits tapped are one tab, not ten. A link
    // to a row of the page itself stays in the page.
    [TestMethod]
    public async Task OpensALinkToAnotherSiteInTheTabOfItsKind()
    {
        var prs = new List<PullRequest>
        {
            new(8, "The feature", "feature", "https://github.com/owner/repo/pull/8", false),
        };
        var html = await PageOf(FeatureRepo().Tag("v1.2", "c3"), prs);

        var links = System.Text.RegularExpressions.Regex.Matches(html, "<a ([^>]*)>").Select(m => m.Groups[1].Value);
        var tabs = new Dictionary<string, HashSet<string>>();
        foreach (var link in links)
        {
            var href = System.Text.RegularExpressions.Regex.Match(link, "href=\"([^\"]*)\"").Groups[1].Value;
            var tab = System.Text.RegularExpressions.Regex.Match(link, "target=\"([^\"]*)\"").Groups[1].Value;
            if (href.StartsWith('#'))
            {
                Assert.AreEqual("", tab, link);
                continue;
            }
            Assert.IsFalse(link.Contains("noopener"), "A tab opened with noopener has no name to be found by");
            var page =
                href == "https://github.com/owner/repo" ? "/" : href.Replace("https://github.com/owner/repo", "");
            var kind = System.Text.RegularExpressions.Regex.Replace(page, "[0-9a-f]{40}|(?<=/pull/)[0-9]+", "*");
            tabs.TryAdd(tab, []);
            tabs[tab].Add(kind);
        }

        Assert.AreEqual(
            """
            gmd-commit: /commit/*
            gmd-editor: https://vscode.dev/github/owner/repo
            gmd-guide: https://github.com/michael-reichenauer/gmd/blob/dev/docs/BRANCH-GRAPH.md
            gmd-pr: /pull/* /pulls
            gmd-repo: / /tree/feature /tree/main
            gmd-runs: /actions
            gmd-tag: /releases/tag/v1.2
            """,
            string.Join('\n', tabs.OrderBy(t => t.Key).Select(t => $"{t.Key}: {string.Join(' ', t.Value.Order())}"))
        );
    }

    // The page keeps itself current by reading itself anew and showing the branches and the rows of
    // what it read, so its script and the page agree on where those are, and on what makes two
    // pages look alike, of which there is one each
    [TestMethod]
    public async Task HasWhatItsScriptUpdatesItBy()
    {
        var html = await PageOf(FeatureRepo());

        StringAssert.Contains(html, "<section class=\"branches\">");
        StringAssert.Contains(html, "<div class=\"rows\">");
        StringAssert.Contains(html, "<p class=\"meta\">Made by gmd, updated <time data-t=\"2026-11-07T12:00:00Z\">");
        foreach (var tag in new[] { "<title>", "<style>", "<script>" })
            Assert.AreEqual(1, html.Split(tag).Length - 1, tag);
        foreach (var selector in new[] { "\".branches\"", "\".rows\"", "\".meta time\"" })
            StringAssert.Contains(html, $"q(d,{selector})");
        StringAssert.Contains(html, "fetch(location.pathname,{cache:\"no-store\"})");
    }

    // A browser says that the page is shown again with one event or another, by whether its tab,
    // its window or the browser itself was left, so the page checks for a newer one on each of them
    [TestMethod]
    public async Task ChecksForANewerPageAsItIsShownAgain()
    {
        var html = await PageOf(FeatureRepo());

        StringAssert.Contains(html, "document.addEventListener(\"visibilitychange\",back)");
        StringAssert.Contains(html, "window.addEventListener(\"pageshow\",back)");
        StringAssert.Contains(html, "window.addEventListener(\"focus\",back)");
        StringAssert.Contains(html, "Date.now()-tried>=soon)check()");
    }

    [TestMethod]
    public async Task LinksATagToItsRelease()
    {
        var html = await PageOf(FeatureRepo().Tag("v1.2", "c3"));

        StringAssert.Contains(
            html,
            "<a target=\"gmd-tag\" class=\"tag\" href=\"https://github.com/owner/repo/releases/tag/v1.2\">v1.2</a>"
        );
    }

    [TestMethod]
    public async Task PutsTheSubjectBesideTheCommitsDot()
    {
        var html = await PageOf(FeatureRepo());

        // The two lines of text are moved down by half a line from the middle of the row
        StringAssert.Contains(html, "line-height:18px;position:relative;top:9px}");
    }

    [TestMethod]
    public void ShowsTheStartOfTheMessageBelowTheSubjectOnOneLine()
    {
        Assert.AreEqual("", WebLogPage.MessageExcerpt(CommitOf("Subject only")));
        Assert.AreEqual(
            "The page now links tags. A second paragraph",
            WebLogPage.MessageExcerpt(
                CommitOf(
                    "Subject\n\n- The page now links\n  tags.\n\nA second paragraph\n\n"
                        + "Co-Authored-By: Someone <s@example.com>\nClaude-Session: https://example.com/s"
                )
            )
        );

        var cut = WebLogPage.MessageExcerpt(CommitOf("Subject\n\n" + new string('a', 200)));
        Assert.AreEqual(new string('a', 150) + "…", cut);
    }

    static gmd.Server.Commit CommitOf(string message) =>
        new RepoBuilder()
            .Commit("c1", "x")
            .BranchWithRemote("main", "c1", isCurrent: true)
            .ViewRepoAsync()
            .Result.ViewCommits[0] with
        {
            Message = message,
        };

    [TestMethod]
    public async Task HidesTheLocalBranchesAtTheirRemoteBranchsCommit()
    {
        var repo = await FeatureRepo().ViewRepoAsync(gmd.Server.ShowBranches.AllActive);
        Assert.AreEqual(
            """
                ┣─┺  More feature work
            ┣─┺╮┃    Merge pull request #7 from owner/feature
            ┃  ├┺    Feature <work>
            ┣  │     Second
            ┗──╯     Initial
            """,
            GraphText.WithSubjects(repo)
        );

        // Neither local branch's tip beside its remote branch's, and a column less for each
        var hidden = WebLogExport.HideSyncedLocalBranches(repo);
        Assert.AreEqual(
            """
              ┣  More feature work
            ┣╮┃  Merge pull request #7 from owner/feature
            ┃├┺  Feature <work>
            ┣│   Second
            ┗╯   Initial
            """,
            GraphText.WithSubjects(hidden)
        );
        CollectionAssert.AreEqual(
            new[] { "origin/main", "origin/feature" },
            hidden.ViewBranches.Select(b => b.Name).ToList()
        );
    }

    [TestMethod]
    public async Task KeepsALocalBranchWithCommitsNotOnItsRemoteBranch()
    {
        var repo = await new RepoBuilder()
            .Commit("c2", "Not pushed", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true, remoteTipCommit: "c1", ahead: 1)
            .ViewRepoAsync(gmd.Server.ShowBranches.AllActive);

        Assert.AreSame(repo, WebLogExport.HideSyncedLocalBranches(repo));
    }
}
