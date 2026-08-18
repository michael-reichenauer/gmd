using gmd.Server;
using gmdTest.Fixtures;

// cSpell:ignore zzzznothing

namespace gmdTest.Server.Private;

// Filtering the commits, i.e. what the filter dialog shows while the user types. A filter is
// split on spaces into terms that must all match, and a term matches if it is found in any of the
// commit's id, subject, branch name, author, date or tags. Two filters are special: '$' is the
// commits whose branch the user set by hand, and '*' is the ambiguous branch tips.
//
// Kept apart from ViewRepoCreaterTest, which is about the branches the user chose to show.
[TestClass]
public class ViewRepoCreaterFilterTest
{
    [TestMethod]
    public async Task TestATermMatchesTheSubject()
    {
        var repo = await Fixture().FilteredViewRepoAsync("Feature");

        CollectionAssert.AreEqual(new[] { "Feature line" }, Subjects(repo));
    }

    [TestMethod]
    public async Task TestTheMatchIsCaseInsensitive()
    {
        CollectionAssert.AreEqual(new[] { "Feature line" }, Subjects(await Fixture().FilteredViewRepoAsync("feature")));
        CollectionAssert.AreEqual(new[] { "Feature line" }, Subjects(await Fixture().FilteredViewRepoAsync("FEATURE")));
    }

    [TestMethod]
    public async Task TestATermMatchesTheAuthor()
    {
        var repo = await Fixture().FilteredViewRepoAsync("Test Author");

        Assert.AreEqual(5, repo.ViewCommits.Count, "Every commit in the fixture has the same author");
    }

    [TestMethod]
    public async Task TestATermMatchesTheShortCommitId()
    {
        var repo = await Fixture().FilteredViewRepoAsync(RepoBuilder.Sid("d1"));

        CollectionAssert.AreEqual(new[] { "Feature line" }, Subjects(repo));
    }

    // A term is looked for in every field, so a branch name also matches the merge commit that
    // happens to name that branch in its subject
    [TestMethod]
    public async Task TestATermMatchesTheBranchName()
    {
        var repo = await Fixture().FilteredViewRepoAsync("dev");

        CollectionAssert.AreEqual(new[] { "Merge branch 'dev' into main", "Feature line" }, Subjects(repo));
    }

    [TestMethod]
    public async Task TestATermMatchesATag()
    {
        var repo = await Fixture().FilteredViewRepoAsync("v1.0");

        CollectionAssert.AreEqual(new[] { "Second line" }, Subjects(repo));
    }

    // The date is matched as the ISO date the log column shows, not as the raw time
    [TestMethod]
    public async Task TestATermMatchesTheDate()
    {
        var repo = await Fixture().FilteredViewRepoAsync("2024-10-15");

        Assert.AreEqual(5, repo.ViewCommits.Count, "Every commit in the fixture is on that day");
    }

    // Spaces separate terms and every one of them has to match, though not in the same field:
    // 'Feature' is in the subject and 'dev' is the branch name of the one commit that matches
    [TestMethod]
    public async Task TestEveryTermMustMatch()
    {
        CollectionAssert.AreEqual(
            new[] { "Feature line" },
            Subjects(await Fixture().FilteredViewRepoAsync("Feature dev"))
        );
        CollectionAssert.AreEqual(
            new[] { NoMatchesRow },
            Subjects(await Fixture().FilteredViewRepoAsync("Feature main")),
            "No commit is both a feature and on main"
        );
    }

    // Quoting is how a phrase containing a space is searched for as a whole, rather than as two
    // terms that may match anywhere
    [TestMethod]
    public async Task TestAQuotedPhraseMustMatchAsAWhole()
    {
        CollectionAssert.AreEqual(
            new[] { "ending the line", "Fix line ending" },
            Subjects(await Phrases().FilteredViewRepoAsync("line ending")),
            "Unquoted, the two words may match anywhere in the commit"
        );
        CollectionAssert.AreEqual(
            new[] { "Fix line ending" },
            Subjects(await Phrases().FilteredViewRepoAsync("\"line ending\"")),
            "Quoted, only the phrase itself matches"
        );
    }

    // The uncommitted changes are a commit like any other by the time the filter runs, so they can
    // be searched for by name
    [TestMethod]
    public async Task TestTheUncommittedCommitCanBeFiltered()
    {
        var repo = await Fixture().WithStatus(modified: 1).FilteredViewRepoAsync("uncommitted");

        CollectionAssert.AreEqual(new[] { "1 uncommitted changes" }, Subjects(repo));
        Assert.AreEqual(Repo.UncommittedId, repo.ViewCommits.Single().Id);
    }

    // The dialog passes 5000, so a filter matching a large repo does not build a view of all of it
    [TestMethod]
    public async Task TestMaxCountCapsTheResult()
    {
        var all = await Fixture().FilteredViewRepoAsync("line");
        var capped = await Fixture().FilteredViewRepoAsync("line", maxCount: 2);

        Assert.AreEqual(4, all.ViewCommits.Count);
        CollectionAssert.AreEqual(new[] { "Feature line", "Third line" }, Subjects(capped));
    }

    // The branches shown are those of the matched commits, brought in with their ancestors, so the
    // graph the results are drawn on still connects up
    [TestMethod]
    public async Task TestTheBranchesOfTheMatchedCommitsAreShown()
    {
        var repo = await Fixture().FilteredViewRepoAsync("Feature");

        CollectionAssert.AreEqual(
            new[] { "origin/main", "main", "dev" },
            repo.ViewBranches.Select(b => b.Name).ToArray(),
            "'dev' is the branch of the match, 'main' and 'origin/main' are its ancestors"
        );
    }

    // The repo carries the filter it was made with, which is how the view knows it is showing
    // results rather than the log
    [TestMethod]
    public async Task TestTheFilterIsCarriedOnTheRepo()
    {
        Assert.AreEqual("Feature", (await Fixture().FilteredViewRepoAsync("Feature")).Filter);
        Assert.AreEqual("", (await Fixture().ViewRepoAsync()).Filter);
    }

    // '$' is the commits whose branch could not be inferred and the user said which it was, i.e.
    // the way to find the choices made in this repo
    [TestMethod]
    public async Task TestDollarShowsTheCommitsWhoseBranchTheUserSet()
    {
        var repo = await new RepoBuilder()
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true)
            .UserSetBranch("c1", "main")
            .FilteredViewRepoAsync("$");

        CollectionAssert.AreEqual(new[] { "Initial" }, Subjects(repo));
    }

    // '*' is the other half of the same job: the commits gmd could not decide a branch for and is
    // asking about
    [TestMethod]
    public async Task TestStarShowsTheAmbiguousBranchTips()
    {
        var repo = await Ambiguous().FilteredViewRepoAsync("*");

        CollectionAssert.AreEqual(new[] { "Shared" }, Subjects(repo));
    }

    // Nothing matched, so the view is the one placeholder row saying so. It is on the virtual
    // branch '<none>', which is how the dialog knows it is not a commit: FilterDlg.cs:91 refuses
    // to select it and FilterDlg.cs:204 leaves it out of the commit and branch counts.
    //
    // Regression test. EmptyFilteredRepo built this repo all along, but ViewRepoCreater.cs:73
    // called it purely for its return value and discarded that value, so what reached the dialog
    // was simply an empty view and the row the dialog is written for never arrived.
    [TestMethod]
    public async Task TestNoMatchesGivesTheNoMatchesRow()
    {
        var repo = await Fixture().FilteredViewRepoAsync("zzzznothing");

        CollectionAssert.AreEqual(new[] { NoMatchesRow }, Subjects(repo));
        Assert.AreEqual("<none>", repo.ViewCommits.Single().BranchName);
        CollectionAssert.AreEqual(new[] { "<none>" }, repo.ViewBranches.Select(b => b.Name).ToArray());
        Assert.AreEqual("zzzznothing", repo.Filter);

        // EmptyFilteredRepo hands ToViewRepo a repo of its own holding just these two, rather than
        // the real one, which is what keeps this lookup from throwing — FilterDlg.cs:197 does it
        // for every row it draws
        Assert.IsTrue(repo.BranchByName.ContainsKey("<none>"));
    }

    // The dialog counts only real commits and real branches, so the placeholder row reads as the
    // '0 commits, 0 branches' it is meant to
    [TestMethod]
    public async Task TestTheNoMatchesRowCountsAsNeitherACommitNorABranch()
    {
        var repo = await Fixture().FilteredViewRepoAsync("zzzznothing");

        Assert.AreEqual(0, repo.ViewCommits.Count(c => c.BranchName != "<none>"));
        Assert.AreEqual(0, repo.ViewCommits.Select(c => c.BranchPrimaryName).Count(b => b != "<none>"));
    }

    // The one row the view is when nothing matched, on the virtual '<none>' branch
    const string NoMatchesRow = "<... No commits matching filter ...>";

    static string[] Subjects(Repo repo) => repo.ViewCommits.Select(c => c.Subject).ToArray();

    // Two branches, a tag and a merge, i.e. enough for a filter to pick things out of. Every
    // subject ends in 'line' so that one term can match several commits.
    static RepoBuilder Fixture() =>
        new RepoBuilder()
            .Commit("c4", "Merge branch 'dev' into main", "c3", "d1")
            .Commit("d1", "Feature line", "c2")
            .Commit("c3", "Third line", "c2")
            .Commit("c2", "Second line", "c1")
            .Commit("c1", "Initial line")
            .BranchWithRemote("main", "c4", isCurrent: true)
            .LocalBranch("dev", "d1")
            .Tag("v1.0", "c2");

    // A phrase whose two words also appear apart, so that quoting changes the answer
    static RepoBuilder Phrases() =>
        new RepoBuilder()
            .Commit("c3", "ending the line", "c2")
            .Commit("c2", "Fix line ending", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c3", isCurrent: true);

    // A commit shared by two branches, which is what makes it ambiguous
    static RepoBuilder Ambiguous() =>
        new RepoBuilder()
            .Commit("b2", "Two", "a1")
            .Commit("b1", "One", "a1")
            .Commit("a1", "Shared")
            .LocalBranch("one", "b1", isCurrent: true)
            .LocalBranch("two", "b2");
}
