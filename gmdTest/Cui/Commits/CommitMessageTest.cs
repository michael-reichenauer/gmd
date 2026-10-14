using gmd.Cui.Commits;

namespace gmdTest.Cui.Commits;

// The subject and the body of a commit message, which the commit and squash dialogs edit apart
[TestClass]
public class CommitMessageTest
{
    [TestMethod]
    public void TestTheSubjectIsTheFirstLineAndTheBodyWhatFollowsTheEmptyLine()
    {
        Assert.AreEqual(("Subject", "Body one\nBody two"), CommitMessage.Split("Subject\n\nBody one\nBody two"));
        Assert.AreEqual(("Subject", ""), CommitMessage.Split("Subject"));
        Assert.AreEqual(("", ""), CommitMessage.Split("  \n "));
    }

    // Git writes the empty line, but a message made elsewhere may not have one
    [TestMethod]
    public void TestABodyRightBelowTheSubjectIsTheBodyAllTheSame()
    {
        Assert.AreEqual(("Subject", "Body"), CommitMessage.Split("Subject\nBody"));
    }

    [TestMethod]
    public void TestJoinPutsAnEmptyLineBetweenThemWhenThereAreBoth()
    {
        Assert.AreEqual("Subject\n\nBody", CommitMessage.Join("Subject", "Body\n\n"));
        Assert.AreEqual("Subject", CommitMessage.Join("Subject", "  \n"));
        Assert.AreEqual("Body", CommitMessage.Join("", "Body"));
    }

    [TestMethod]
    public void TestSplitAndJoinGiveTheMessageBack()
    {
        var message = "Fix the thing\n\nIt was broken.\n\nNow it is not.";

        var (subject, body) = CommitMessage.Split(message);

        Assert.AreEqual(message, CommitMessage.Join(subject, body));
    }
}
