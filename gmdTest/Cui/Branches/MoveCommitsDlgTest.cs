using gmd.Cui.Branches;

namespace gmdTest.Cui.Branches;

// What the dialog that moves commits to a new branch says of them, and of the branch they leave
[TestClass]
public class MoveCommitsDlgTest
{
    [TestMethod]
    public void TestWhatItSays()
    {
        Assert.AreEqual("Move 2 commits of 'main' to a new branch:", MoveCommitsDlg.Heading("main", 2));
        Assert.AreEqual("Move 1 commit of 'main' to a new branch:", MoveCommitsDlg.Heading("main", 1));
        Assert.AreEqual(
            "It is checked out, and 'main' goes back to 2b4e2b8b.",
            MoveCommitsDlg.About("main", "2b4e2b8b")
        );
    }
}
