using gmd.Cui.Common;
using gmd.Cui.RepoView;
using gmd.Server;
using gmdTest.Fixtures;

namespace gmdTest.Cui.RepoView;

// What the log copies: the commit on the cursor's row, or the commits selected, oldest first. A
// selection is a range of rows, but the rows in between belong to whichever branches the graph
// happens to draw there, so what is copied is only the commits of the branch the selection started
// on, which are the ones the log highlights.
//
// The rows of ThreeBranches, all shown: c5, f1, c4, d1, c3, c2, c1.
[TestClass]
public class CommitCopyTest
{
    static readonly Selection NoSelection = new(0, 0, 0, 0, 0);

    [TestMethod]
    public async Task TestWithNothingSelectedTheRowsCommitIsCopied()
    {
        var repo = await ThreeBranches().ViewRepoAsync(ShowBranches.AllActive);

        Assert.AreEqual("c3", Names(CommitCopy.Commits(repo, NoSelection, 4)));
    }

    [TestMethod]
    public async Task TestTheSelectedCommitsAreCopiedOldestFirst()
    {
        var repo = await ThreeBranches().ViewRepoAsync(ShowBranches.AllActive);

        var commits = CommitCopy.Commits(repo, Rows(4, 6), 6);

        Assert.AreEqual("c1 c2 c3", Names(commits));
        Assert.AreEqual(
            $"""
            {Short("c1")} Initial
            {Short("c2")} Second
            {Short("c3")} Third
            """,
            CommitCopy.Text(CopyKind.IdsAndSubjects, commits)
        );
    }

    // Rows 0 to 3 are two main commits with a feat and a dev commit drawn in between them, and
    // those two are skipped rather than copied as if they were main commits
    [TestMethod]
    public async Task TestTheCommitsOfOtherBranchesInBetweenAreSkipped()
    {
        var repo = await ThreeBranches().ViewRepoAsync(ShowBranches.AllActive);

        Assert.AreEqual("c4 c5", Names(CommitCopy.Commits(repo, Rows(0, 3), 3)));
    }

    // The branch is the one of the row the selection started on, as the log highlights it, not the
    // one of the top row: selected upwards from c4 over f1, the copy is c4, not the feat commit
    [TestMethod]
    public async Task TestTheSelectionIsOfTheBranchItStartedOn()
    {
        var repo = await ThreeBranches().ViewRepoAsync(ShowBranches.AllActive);

        Assert.AreEqual("c4", Names(CommitCopy.Commits(repo, Rows(1, 2, initial: 2), 1)));
        Assert.AreEqual("f1", Names(CommitCopy.Commits(repo, Rows(1, 2, initial: 1), 2)));
    }

    [TestMethod]
    public async Task TestTheUncommittedChangesAreNoCommitToCopy()
    {
        var repo = await ThreeBranches().WithStatus(modified: 1).ViewRepoAsync(ShowBranches.AllActive);
        Assert.IsTrue(repo.ViewCommits[0].IsUncommitted);

        Assert.AreEqual("", Names(CommitCopy.Commits(repo, NoSelection, 0)));
        Assert.AreEqual(
            "The uncommitted changes are no commit yet: move to one first",
            CommitCopy.WhyNot(repo, NoSelection, 0)
        );

        // Selected with the commits below it, it is left out, and the commits are copied
        Assert.AreEqual("c4 c5", Names(CommitCopy.Commits(repo, Rows(0, 3, initial: 0), 3)));
        Assert.AreEqual("", CommitCopy.WhyNot(repo, Rows(0, 3, initial: 0), 3));
    }

    // Ids on one line, as a git command takes them, the subjects and the messages one to a line,
    // the messages with a blank line between them
    [TestMethod]
    public async Task TestTheTextsOfSeveralCommits()
    {
        var repo = await ThreeBranches().ViewRepoAsync(ShowBranches.AllActive);
        var commits = CommitCopy.Commits(repo, Rows(4, 5), 5);

        Assert.AreEqual($"{RepoBuilder.Sha("c2")} {RepoBuilder.Sha("c3")}", CommitCopy.Text(CopyKind.Ids, commits));
        Assert.AreEqual($"{Short("c2")} {Short("c3")}", CommitCopy.Text(CopyKind.ShortIds, commits));
        Assert.AreEqual("Second\nThird", CommitCopy.Text(CopyKind.Subjects, commits));
        Assert.AreEqual("Second\n\nThird", CommitCopy.Text(CopyKind.Messages, commits));
    }

    // The id as 'git log --oneline' abbreviates it, a character longer than the log shows
    [TestMethod]
    public async Task TestTheShortIdIsSevenCharacters()
    {
        var repo = await ThreeBranches().ViewRepoAsync(ShowBranches.AllActive);
        var commit = CommitCopy.Commits(repo, NoSelection, 4)[0];

        Assert.AreEqual(RepoBuilder.Sha("c3")[..7], CommitCopy.Text(CopyKind.ShortIds, [commit]));
        Assert.AreEqual(RepoBuilder.Sid("c3") + RepoBuilder.Sha("c3")[6], CommitCopy.ShortId(commit));
    }

    // From before the oldest to the newest, which includes the oldest, with full ids
    [TestMethod]
    public async Task TestTheRangeStartsAtTheParentOfTheOldest()
    {
        var repo = await ThreeBranches().ViewRepoAsync(ShowBranches.AllActive);
        var commits = CommitCopy.Commits(repo, Rows(4, 5), 5);

        Assert.AreEqual($"{RepoBuilder.Sha("c2")}^..{RepoBuilder.Sha("c3")}", CommitCopy.Text(CopyKind.Range, commits));
        Assert.AreEqual($"{RepoBuilder.Sid("c2")}^..{RepoBuilder.Sid("c3")}", CommitCopy.RangeName(commits));
        Assert.AreEqual("", CommitCopy.WhyNoRange(commits));
    }

    // The first commit has no parent, so there is no 'A^' to start the range at
    [TestMethod]
    public async Task TestNoRangeStartsAtTheFirstCommit()
    {
        var repo = await ThreeBranches().ViewRepoAsync(ShowBranches.AllActive);
        var commits = CommitCopy.Commits(repo, Rows(4, 6), 6);

        Assert.IsNull(CommitCopy.Text(CopyKind.Range, commits));
        Assert.AreEqual(
            "The oldest commit is the first one, with no parent to start a range at: copy the ids instead",
            CommitCopy.WhyNoRange(commits)
        );
    }

    // What the status line says: one commit by the id the log shows, several by how many
    [TestMethod]
    public async Task TestWhatACopySays()
    {
        var repo = await ThreeBranches().ViewRepoAsync(ShowBranches.AllActive);
        var one = CommitCopy.Commits(repo, NoSelection, 4);
        var two = CommitCopy.Commits(repo, Rows(4, 5), 5);

        Assert.AreEqual($"Copied the id of {RepoBuilder.Sid("c3")}", CommitCopy.Said(CopyKind.Ids, one));
        Assert.AreEqual(
            $"Copied the id and subject of {RepoBuilder.Sid("c3")}",
            CommitCopy.Said(CopyKind.IdsAndSubjects, one)
        );
        Assert.AreEqual("Copied the messages of 2 commits", CommitCopy.Said(CopyKind.Messages, two));
        Assert.AreEqual(
            $"Copied the range {RepoBuilder.Sid("c2")}^..{RepoBuilder.Sid("c3")}",
            CommitCopy.Said(CopyKind.Range, two)
        );
    }

    // Rows selected with ⇧↑↓, from the row the selection started on
    static Selection Rows(int i1, int i2, int? initial = null) => new(0, i1, int.MaxValue, i2, initial ?? i1);

    static string Short(string name) => RepoBuilder.Sha(name)[..7];

    // The commits by the names the builder gave them
    static string Names(IReadOnlyList<Commit> commits) =>
        string.Join(" ", commits.Select(c => Named.First(n => RepoBuilder.Sha(n) == c.Id)));

    static readonly string[] Named = ["c1", "c2", "c3", "c4", "c5", "d1", "f1"];

    // main with two branches, dev and feat, both branched out of 'Second' and merged back
    static RepoBuilder ThreeBranches() =>
        new RepoBuilder()
            .Commit("c5", "Merge branch 'feat' into main", "c4", "f1")
            .Commit("f1", "Feature work", "c2")
            .Commit("c4", "Merge branch 'dev' into main", "c3", "d1")
            .Commit("d1", "Dev work", "c2")
            .Commit("c3", "Third", "c2")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c5", isCurrent: true)
            .BranchWithRemote("dev", "d1")
            .LocalBranch("feat", "f1");
}
