using gmd.Cui;
using gmd.Server;

namespace gmdTest.Cui;

// The commit dialog's checklist: every changed file ticked at first, which commits everything as a
// commit always did, and the paths of what is left ticked once some are unticked
[TestClass]
public class CommitFilesTest
{
    [TestMethod]
    public void TestEveryFileIsTickedAtFirstAndThatIsAllOfIt()
    {
        var files = new CommitFiles(Status(modified: ["b.txt"], added: ["a.txt"], deleted: ["c.txt"]));

        Assert.AreEqual("A a.txt, M b.txt, D c.txt", string.Join(", ", files.Files.Select(f => $"{f.Kind} {f.Path}")));
        Assert.AreEqual(3, files.TickedCount);
        Assert.IsNull(files.PathsToCommit, "All of it, committed as a commit always was");
    }

    [TestMethod]
    public void TestTheTickedPathsAreCommitted()
    {
        var files = new CommitFiles(Status(modified: ["b.txt"], added: ["a.txt"], deleted: ["c.txt"]));

        files.Toggle(1);

        Assert.AreEqual(2, files.TickedCount);
        CollectionAssert.AreEqual(new[] { "a.txt", "c.txt" }, files.PathsToCommit!.ToArray());
    }

    // A rename is one file to tick, and both its paths to commit: the one goes, the other comes
    [TestMethod]
    public void TestARenameCommitsBothItsPaths()
    {
        var files = new CommitFiles(Status(modified: ["z.txt"], renamedFrom: ["old.txt"], renamedTo: ["new.txt"]));

        Assert.AreEqual("R old.txt → new.txt", $"{files.Files[0].Kind} {files.Files[0].Path}");
        files.Toggle(1);
        CollectionAssert.AreEqual(new[] { "old.txt", "new.txt" }, files.PathsToCommit!.ToArray());
    }

    // 'a' ticks all, or unticks all once all are ticked
    [TestMethod]
    public void TestToggleAllTicksAllOrNone()
    {
        var files = new CommitFiles(Status(modified: ["a.txt", "b.txt"]));

        files.ToggleAll();
        Assert.AreEqual(0, files.TickedCount);
        CollectionAssert.AreEqual(Array.Empty<string>(), files.PathsToCommit!.ToArray());

        files.Toggle(0);
        files.ToggleAll();
        Assert.AreEqual(2, files.TickedCount);
        Assert.IsNull(files.PathsToCommit);
    }

    static Status Status(
        string[]? modified = null,
        string[]? added = null,
        string[]? deleted = null,
        string[]? renamedFrom = null,
        string[]? renamedTo = null
    ) =>
        new(
            modified?.Length ?? 0,
            added?.Length ?? 0,
            deleted?.Length ?? 0,
            0,
            renamedFrom?.Length ?? 0,
            GitOperation.None,
            "",
            "",
            "",
            0,
            0,
            true,
            modified ?? [],
            added ?? [],
            deleted ?? [],
            [],
            renamedFrom ?? [],
            renamedTo ?? []
        );
}
