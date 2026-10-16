using gmd.Cui.Conflict;
using gmd.Server;

namespace gmdTest.Cui.Conflict;

// The resolver of every conflicted file of an operation, as far as it is decided with no view: what
// finishes the operation once none is left, which the user is then asked about
[TestClass]
public class ConflictWorkspaceTest
{
    [TestMethod]
    [DataRow(GitOperation.Merge, true, "Commit")]
    [DataRow(GitOperation.Revert, true, "Commit")]
    [DataRow(GitOperation.Revert, false, "Continue")]
    [DataRow(GitOperation.CherryPick, false, "Continue")]
    [DataRow(GitOperation.Rebase, false, "Continue")]
    [DataRow(GitOperation.Am, false, "Continue")]
    public void TestACommitOrAContinueFinishesTheOperation(
        GitOperation operation,
        bool isFinishedByCommit,
        string expected
    )
    {
        Assert.AreEqual(
            expected,
            ConflictWorkspace.FinishOf(new ConflictState(operation, [], isFinishedByCommit)).ToString()
        );
    }

    // Conflicts with no operation in progress, e.g. of a stash popped, have nothing to finish: there
    // was a box saying a commit finished it
    [TestMethod]
    public void TestNothingFinishesConflictsOfNoOperation()
    {
        Assert.AreEqual(ResolveNext.None, ConflictWorkspace.FinishOf(new ConflictState(GitOperation.None, [], true)));
    }
}
