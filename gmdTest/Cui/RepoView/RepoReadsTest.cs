using gmd.Cui.RepoView;

namespace gmdTest.Cui.RepoView;

// Which reads of the repo are still right to show when they are done, and which are running
[TestClass]
public class RepoReadsTest
{
    [TestMethod]
    public void TestAReadWithNothingShownMeanwhileIsShown()
    {
        var reads = new RepoReads();

        var read = reads.StartRepo();

        Assert.IsFalse(reads.IsOvertaken(read));
    }

    // Two refreshes at once, as a command's and one after a change to the repo: the one that started
    // first and finished last read the older repo, which it put back over the newer one
    [TestMethod]
    public void TestAReadThatAnotherOvertookIsNotShown()
    {
        var reads = new RepoReads();
        var older = reads.StartRepo();
        var newer = reads.StartRepo();

        Assert.IsFalse(reads.IsOvertaken(newer));
        reads.Shown();
        reads.End(newer);

        Assert.IsTrue(reads.IsOvertaken(older));
    }

    // A show or hide of branches while a refresh reads: the refresh, read with the branches shown
    // before it, undid it
    [TestMethod]
    public void TestAShowOrHideMeanwhileOvertakesARead()
    {
        var reads = new RepoReads();
        var refresh = reads.StartRepo();

        reads.Shown();

        Assert.IsTrue(reads.IsOvertaken(refresh));
    }

    // A status read applies the status to the repo shown when it started, so a repo read and shown
    // meanwhile overtakes it, whose commits it would put back
    [TestMethod]
    public void TestAStatusReadThatARepoReadOvertookIsNotShown()
    {
        var reads = new RepoReads();
        var status = reads.StartStatus();
        var repo = reads.StartRepo();
        reads.Shown();
        reads.End(repo);

        Assert.IsTrue(reads.IsOvertaken(status));
    }

    // What changes told of meanwhile wait for: a repo read, or any read for a change of the status.
    // Counted, since two flags were cleared by the first of two reads to finish while the other ran.
    [TestMethod]
    public void TestReadsAreCountedUntilTheLastEnds()
    {
        var reads = new RepoReads();
        var first = reads.StartRepo();
        var second = reads.StartRepo();
        var status = reads.StartStatus();

        reads.End(first);
        Assert.IsTrue(reads.IsReadingRepo, "One repo read still runs");

        reads.End(second);
        Assert.IsFalse(reads.IsReadingRepo);
        Assert.IsTrue(reads.IsReading, "The status read still runs");

        reads.End(status);
        Assert.IsFalse(reads.IsReading);
    }
}
