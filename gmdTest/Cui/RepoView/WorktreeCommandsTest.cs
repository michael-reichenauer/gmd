using gmd.Cui;
using gmd.Cui.RepoView;

namespace gmdTest.Cui.RepoView;

// The order of the two writes that create a worktree inside the repository: the worktree, then
// the line in the main worktree's .gitignore that hides it from the status. Git first, so that a
// worktree git refuses leaves no line behind for a folder that never came to be; and a line that
// cannot be written once the worktree exists is reported as that, since the worktree is there to
// be used. The add is a delegate, which is what makes the order assertable without a server.
[TestClass]
public class WorktreeCommandsTest
{
    string root = "";

    [TestInitialize]
    public void Init()
    {
        root = Path.Join(Path.GetTempPath(), $"gmdTest-worktreecmds-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, true);
    }

    [TestMethod]
    public async Task TestAWorktreeGitRefusesLeavesNoIgnoreLineBehind()
    {
        var result = await WorktreeCommands.AddAndIgnoreAsync(Request(".worktrees"), root, Refused);

        Assert.IsFalse(Try(out var e, result));
        StringAssert.Contains(e.ErrorMessage, "Failed to create worktree");
        Assert.IsFalse(File.Exists(Path.Join(root, ".gitignore")), "No worktree was made, so nothing is ignored");
    }

    [TestMethod]
    public async Task TestTheIgnoreLineIsWrittenOnceTheWorktreeExists()
    {
        var isAdded = false;
        Task<R> Add()
        {
            isAdded = true;
            return Task.FromResult(R.Ok);
        }

        Assert.IsTrue(
            Try(out var e, await WorktreeCommands.AddAndIgnoreAsync(Request(".worktrees"), root, Add)),
            $"{e}"
        );

        Assert.IsTrue(isAdded);
        Assert.AreEqual(".worktrees/\n", File.ReadAllText(Path.Join(root, ".gitignore")));
    }

    [TestMethod]
    public async Task TestTheIgnoreLineGoesOnALineOfItsOwn()
    {
        File.WriteAllText(Path.Join(root, ".gitignore"), "bin"); // No final newline

        Assert.IsTrue(
            Try(out var e, await WorktreeCommands.AddAndIgnoreAsync(Request(".claude/worktrees"), root, Ok)),
            $"{e}"
        );

        Assert.AreEqual("bin\n.claude/worktrees/\n", File.ReadAllText(Path.Join(root, ".gitignore")));
    }

    [TestMethod]
    public async Task TestAWorktreeOutsideTheRepositoryNeedsNoIgnoreLine()
    {
        Assert.IsTrue(Try(out var e, await WorktreeCommands.AddAndIgnoreAsync(Request(""), root, Ok)), $"{e}");

        Assert.IsFalse(File.Exists(Path.Join(root, ".gitignore")));
    }

    [TestMethod]
    public async Task TestAnIgnoreLineThatCannotBeWrittenReportsTheWorktreeItLeft()
    {
        var missingRoot = Path.Join(root, "gone");

        var result = await WorktreeCommands.AddAndIgnoreAsync(Request(".worktrees"), missingRoot, Ok);

        Assert.IsFalse(Try(out var e, result));
        StringAssert.Contains(e.ErrorMessage, "Worktree created");
        StringAssert.Contains(e.ErrorMessage, ".gitignore");
    }

    // A request for a worktree inside the repository, with the folder to ignore for it (or none)
    static AddWorktreeResult Request(string ignoreFolder) =>
        new("/test/repo/.worktrees/dev", "dev", false, "", true, ignoreFolder, WorktreeLocation.Local);

    static Task<R> Ok() => Task.FromResult(R.Ok);

    static Task<R> Refused() => Task.FromResult<R>(R.Error("fatal: invalid reference: dev"));
}
