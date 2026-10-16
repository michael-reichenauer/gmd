using gmd.Cui.Conflict;
using gmd.Server;

namespace gmdTest.Cui.Conflict;

// What the resolver calls the two sides: HEAD by the branch it is on, each side with the subject of
// its commit, and a rebase's commit by its id, its subject from the marker
[TestClass]
public class ConflictNamesTest
{
    static ConflictFile FileLabelled(string ours, string theirs) =>
        new(
            "f.txt",
            ConflictKind.BothModified,
            false,
            [new ConflictSegment([], new ConflictHunk(0, ours, "", theirs, [new("o")], [], [new("t")]))],
            true,
            true
        );

    [TestMethod]
    public void TestAMergeNamesHeadByItsBranch()
    {
        var names = ConflictNames.Of(FileLabelled("HEAD", "dev"), new ConflictSides("main", "Main work", "Dev work"));

        Assert.AreEqual(("main", "dev"), (names.Ours, names.Theirs));
        Assert.AreEqual("main (HEAD) · Main work", names.OursTitle.ToString());
        Assert.AreEqual("dev · Dev work", names.TheirsTitle.ToString());
    }

    // During a rebase HEAD is on no branch, and the commit applied is named by git as 'id (subject)'
    [TestMethod]
    public void TestARebaseNamesTheCommitApplied()
    {
        var names = ConflictNames.Of(
            FileLabelled("HEAD", "1a2b3c4 (Dev work)"),
            new ConflictSides("", "Main work", "Dev work")
        );

        Assert.AreEqual(("HEAD", "1a2b3c4"), (names.Ours, names.Theirs));
        Assert.AreEqual("HEAD · Main work", names.OursTitle.ToString());
        Assert.AreEqual("1a2b3c4 · Dev work", names.TheirsTitle.ToString());
    }

    // A revert's other side is the parent of the commit reverted, which git writes as 'parent of id
    // (subject)', named so with the subject once: it was the whole label, and the subject again
    [TestMethod]
    public void TestARevertNamesTheParentOfTheCommitReverted()
    {
        var names = ConflictNames.Of(
            FileLabelled("HEAD", "parent of 1a2b3c4 (Dev work)"),
            new ConflictSides("main", "Main work", "Dev work")
        );

        Assert.AreEqual(("main", "parent of 1a2b3c4"), (names.Ours, names.Theirs));
        Assert.AreEqual("parent of 1a2b3c4 · Dev work", names.TheirsTitle.ToString());
    }

    // With nothing known of the sides, the names git wrote, as they were
    [TestMethod]
    public void TestWithNoSidesKnownTheLabelsAreTheNames()
    {
        var names = ConflictNames.Of(FileLabelled("HEAD", "dev"), ConflictSides.None);

        Assert.AreEqual("HEAD", names.OursTitle.ToString());
        Assert.AreEqual("dev", names.TheirsTitle.ToString());
    }
}
