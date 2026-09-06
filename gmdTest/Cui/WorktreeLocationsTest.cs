using gmd.Cui;

namespace gmdTest.Cui;

// Where a new worktree goes for each place the create dialog offers, and the folder to ignore
[TestClass]
public class WorktreeLocationsTest
{
    static string Join(params string[] parts) => Path.Join(parts);

    [TestMethod]
    public void TestBesideTheRepoIsNamedAfterTheRepoAndTheBranch()
    {
        Assert.AreEqual(
            Join("/home/me", "repo-feature-login"),
            WorktreeLocations.PathFor(WorktreeLocation.Sibling, "/home/me/repo", "feature/login")
        );
    }

    // Claude Code names the branch of a worktree 'worktree-<name>' and its folder '<name>', so a
    // branch named that way lands where Claude would have put it
    [TestMethod]
    public void TestClaudesFolderIsInsideTheRepoWithoutTheWorktreePrefix()
    {
        Assert.AreEqual(
            Join("/home/me/repo", ".claude", "worktrees", "login"),
            WorktreeLocations.PathFor(WorktreeLocation.Claude, "/home/me/repo", "worktree-login")
        );
        Assert.AreEqual(
            Join("/home/me/repo", ".claude", "worktrees", "feature-login"),
            WorktreeLocations.PathFor(WorktreeLocation.Claude, "/home/me/repo", "feature/login")
        );
    }

    [TestMethod]
    public void TestTheLocalFolderIsInsideTheRepo()
    {
        Assert.AreEqual(
            Join("/home/me/repo", ".worktrees", "dev"),
            WorktreeLocations.PathFor(WorktreeLocation.Local, "/home/me/repo", "dev")
        );
    }

    [TestMethod]
    public void TestATrailingSeparatorOnTheRootChangesNothing()
    {
        Assert.AreEqual(
            Join("/home/me", "repo-dev"),
            WorktreeLocations.PathFor(WorktreeLocation.Sibling, "/home/me/repo/", "dev")
        );
    }

    // The two places inside the repo have to be ignored, or the main worktree shows the whole
    // checkout as untracked files; beside the repo there is nothing to ignore
    [TestMethod]
    public void TestOnlyTheFoldersInsideTheRepoAreIgnored()
    {
        Assert.AreEqual("", WorktreeLocations.IgnoreFolder(WorktreeLocation.Sibling));
        Assert.AreEqual(".claude/worktrees", WorktreeLocations.IgnoreFolder(WorktreeLocation.Claude));
        Assert.AreEqual(".worktrees", WorktreeLocations.IgnoreFolder(WorktreeLocation.Local));
    }

    [TestMethod]
    public void TestAFolderNameHasNoSeparatorsOrInvalidCharacters()
    {
        Assert.AreEqual("feature-login", WorktreeLocations.FolderName("feature/login"));
        Assert.AreEqual("a-b", WorktreeLocations.FolderName(" a\\b "));
    }

    // The pick is remembered by name, and an unknown or empty name is the default
    [TestMethod]
    public void TestTheRememberedPickIsParsedByName()
    {
        Assert.AreEqual(WorktreeLocation.Claude, WorktreeLocations.Parse("Claude"));
        Assert.AreEqual(WorktreeLocation.Sibling, WorktreeLocations.Parse(""));
        Assert.AreEqual(WorktreeLocation.Sibling, WorktreeLocations.Parse("someday"));
    }
}
