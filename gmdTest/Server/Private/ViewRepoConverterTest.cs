using gmd.Server;
using gmd.Server.Private;
using GitBlame = gmd.Git.Blame;
using GitBlameCommit = gmd.Git.BlameCommit;
using GitBlameLine = gmd.Git.BlameLine;

namespace gmdTest.Server.Private;

[TestClass]
public class ViewRepoConverterTest
{
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

        var blame = new ViewRepoConverter().ToBlame(gitBlame);

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
