using gmd.Server;
using gmd.Server.Private;
using GitBlame = gmd.Git.Blame;
using GitBlameCommit = gmd.Git.BlameCommit;
using GitBlameLine = gmd.Git.BlameLine;

namespace gmdTest.Server.Private;

// The records read from git, as the layers above see them
[TestClass]
public class GitConverterTest
{
    // The enums are converted member for member by name, each way, and every member has its match:
    // a member added to one and not the other fails here rather than at the user
    [TestMethod]
    public void TestTheEnumsConvertMemberForMember()
    {
        foreach (var kind in Enum.GetValues<gmd.Git.ConflictKind>())
        {
            Assert.AreEqual(kind.ToString(), GitConverter.ToConflictKind(kind).ToString());
            Assert.AreEqual(kind, GitConverter.ToGitConflictKind(GitConverter.ToConflictKind(kind)));
        }
        Assert.AreEqual(Enum.GetValues<gmd.Git.ConflictKind>().Length, Enum.GetValues<ConflictKind>().Length);

        foreach (var operation in Enum.GetValues<gmd.Git.GitOperation>())
            Assert.AreEqual(operation.ToString(), GitConverter.ToOperation(operation).ToString());
        Assert.AreEqual(Enum.GetValues<gmd.Git.GitOperation>().Length, Enum.GetValues<GitOperation>().Length);

        foreach (var choice in Enum.GetValues<HunkChoice>())
            Assert.AreEqual(choice.ToString(), GitConverter.ToGitChoice(choice).ToString());
        Assert.AreEqual(Enum.GetValues<gmd.Git.HunkChoice>().Length, Enum.GetValues<HunkChoice>().Length);
    }

    // Which worktree is the current one, and its changes, are the server's to say
    [TestMethod]
    public void TestAWorktreeTakesWhetherItIsCurrentAndItsChanges()
    {
        var gitWorktree = new gmd.Git.Worktree(
            "/repo-dev",
            "abc",
            "dev",
            false,
            false,
            false,
            true,
            "in use",
            false,
            ""
        );

        var worktree = GitConverter.ToWorktree(gitWorktree, isCurrent: true, changesCount: 3);

        Assert.AreEqual(
            new Worktree("/repo-dev", "dev", "abc", false, true, false, true, "in use", false, "", 3),
            worktree
        );
    }

    // Git's id of the lines not committed yet is all zeros, 40 of them in a repository of SHA-1 ids
    // and 64 in one of SHA-256 ids. gmd knows them by Repo.UncommittedId in both, which is what routes
    // their diff to the uncommitted changes and finds them in the log.
    [TestMethod]
    [DataRow(40)]
    [DataRow(64)]
    public void TestTheLinesNotCommittedYetAreByGmdsUncommittedId(int idLength)
    {
        var id = new string('a', idLength);
        var zeros = new string('0', idLength);
        var gitBlame = new GitBlame(
            "f.txt",
            "",
            [new GitBlameLine(id, 1, 1, "one"), new GitBlameLine(zeros, 2, 2, "two")],
            new Dictionary<string, GitBlameCommit> { [id] = CommitOf(id, false), [zeros] = CommitOf(zeros, true) }
        );

        var blame = GitConverter.ToBlame(gitBlame);

        CollectionAssert.AreEqual(new[] { id, Repo.UncommittedId }, blame.Lines.Select(l => l.CommitId).ToArray());
        Assert.AreEqual(Repo.UncommittedId, blame.CommitById[Repo.UncommittedId].Id);
        Assert.IsTrue(blame.CommitById[Repo.UncommittedId].IsUncommitted);
        Assert.AreEqual(id, blame.CommitById[id].Id);
    }

    static GitBlameCommit CommitOf(string id, bool isUncommitted) =>
        new(
            id,
            id.Sid(),
            "Alice",
            "alice@example.com",
            DateTime.MinValue,
            "Subject",
            isUncommitted,
            false,
            "",
            "",
            "f.txt"
        );
}
