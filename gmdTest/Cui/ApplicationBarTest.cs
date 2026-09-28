using gmd.Cui;
using gmd.Server;

namespace gmdTest.Cui;

// What the application bar says about an operation git stopped part way through
[TestClass]
public class ApplicationBarTest
{
    [TestMethod]
    public void TestNothingInProgressSaysNothing()
    {
        Assert.AreEqual("", ApplicationBar.GetOperation(Status.Empty).ToString());
    }

    // Red while there are conflicts, the count and how far a rebase has got
    [TestMethod]
    public void TestConflictsAreCounted()
    {
        var merge = Status.Empty with { Operation = GitOperation.Merge, Conflicted = 1, IsFinishedByCommit = true };
        var rebase = Status.Empty with
        {
            Operation = GitOperation.Rebase,
            Conflicted = 2,
            OperationStep = 3,
            OperationTotal = 7,
        };

        Assert.AreEqual("Merging: 1 conflict ", ApplicationBar.GetOperation(merge).ToString());
        Assert.AreEqual("Rebasing 3/7: 2 conflicts ", ApplicationBar.GetOperation(rebase).ToString());
        Assert.AreEqual(gmd.Cui.Common.Color.BrightRed, ApplicationBar.GetOperation(merge).Fragments[0].Color);
    }

    // Yellow once resolved, saying what finishes it: a merge is committed, a rebase continued
    [TestMethod]
    public void TestResolvedSaysWhatFinishesIt()
    {
        var merge = Status.Empty with { Operation = GitOperation.Merge, IsFinishedByCommit = true };
        var rebase = Status.Empty with { Operation = GitOperation.Rebase, IsFinishedByCommit = false };

        Assert.AreEqual("Merging: commit to finish ", ApplicationBar.GetOperation(merge).ToString());
        Assert.AreEqual("Rebasing: continue to finish ", ApplicationBar.GetOperation(rebase).ToString());
        Assert.AreEqual(gmd.Cui.Common.Color.Yellow, ApplicationBar.GetOperation(merge).Fragments[0].Color);
    }
}
