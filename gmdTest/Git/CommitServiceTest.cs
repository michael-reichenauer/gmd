using gmd.Git.Private;
using gmdTest.Utils;

namespace gmdTest.Git;

[TestClass]
public class CommitServiceTest
{
    // CommitAllChangesAsync reads .git/MERGE_MSG and UndoUncommittedFileAsync may delete a file, so
    // these need a working directory. A plain temp folder with a .git in it is enough.
    string wd = "";

    [TestInitialize]
    public void Init()
    {
        wd = Path.Join(Path.GetTempPath(), $"gmdTest-commit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Join(wd, ".git"));
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(wd))
            Directory.Delete(wd, true);
    }

    static string[] ArgsOf(FakeCmd cmd) => cmd.Calls.Select(c => c.Args).ToArray();

    // Everything is staged first, so untracked files are committed too
    [TestMethod]
    public async Task TestCommitStagesEverythingFirst()
    {
        var cmd = new FakeCmd("");

        await new CommitService(cmd).CommitAllChangesAsync("The message", false, wd);

        CollectionAssert.AreEqual(new[] { "add .", "commit -am \"The message\"" }, ArgsOf(cmd));
    }

    [TestMethod]
    public async Task TestAmendCommit()
    {
        var cmd = new FakeCmd("");

        await new CommitService(cmd).CommitAllChangesAsync("The message", true, wd);

        Assert.AreEqual("commit --amend -am \"The message\"", cmd.Calls[1].Args);
    }

    // The message goes into a quoted command line argument, so its own quotes have to be escaped
    [TestMethod]
    public async Task TestCommitMessageQuotesAreEscaped()
    {
        var cmd = new FakeCmd("");

        await new CommitService(cmd).CommitAllChangesAsync("Fix \"the\" bug", false, wd);

        Assert.AreEqual("commit -am \"Fix \\\"the\\\" bug\"", cmd.Calls[1].Args);
    }

    // While a merge is in progress the staging must be left alone, since staging is what marks a
    // conflict as resolved
    [TestMethod]
    public async Task TestCommitDoesNotStageWhileMerging()
    {
        File.WriteAllText(Path.Join(wd, ".git", "MERGE_MSG"), "Merge branch 'topic'\n");
        var cmd = new FakeCmd("");

        await new CommitService(cmd).CommitAllChangesAsync("The message", false, wd);

        CollectionAssert.AreEqual(new[] { "commit -am \"The message\"" }, ArgsOf(cmd));
    }

    [TestMethod]
    public async Task TestCommitStopsIfStagingFails()
    {
        var cmd = new FakeCmd((_, _, _) => FakeCmd.Fail("fatal: not a git repository"));

        var result = await new CommitService(cmd).CommitAllChangesAsync("The message", false, wd);

        Assert.IsFalse(Try(out var _, result), "Expected the git failure to propagate");
        Assert.AreEqual(1, cmd.Calls.Count, "The commit is not attempted");
    }

    // Undo of all changes also removes untracked files, but not ignored ones
    [TestMethod]
    public async Task TestUndoAllUncommittedChanges()
    {
        var cmd = new FakeCmd("");

        await new CommitService(cmd).UndoAllUncommittedChangesAsync(wd);

        CollectionAssert.AreEqual(new[] { "reset --hard", "clean -fd" }, ArgsOf(cmd));
    }

    // Cleaning the working folder also removes ignored files, i.e. build output
    [TestMethod]
    public async Task TestCleanWorkingFolder()
    {
        var cmd = new FakeCmd("");

        await new CommitService(cmd).CleanWorkingFolderAsync(wd);

        CollectionAssert.AreEqual(new[] { "reset --hard", "clean -fxd" }, ArgsOf(cmd));
    }

    [TestMethod]
    public async Task TestUndoUncommittedFile()
    {
        var cmd = new FakeCmd("");

        await new CommitService(cmd).UndoUncommittedFileAsync("src/a.txt", wd);

        Assert.AreEqual("checkout --force \"src/a.txt\"", cmd.Calls[0].Args);
    }

    // A new file is not known to git, so it cannot be checked out — it is removed instead
    [TestMethod]
    public async Task TestUndoUncommittedFileRemovesANewFile()
    {
        File.WriteAllText(Path.Join(wd, "new.txt"), "new");
        var cmd = new FakeCmd(
            (_, _, _) => FakeCmd.Fail("error: pathspec 'new.txt' did not match any file(s) known to git")
        );

        var result = await new CommitService(cmd).UndoUncommittedFileAsync("new.txt", wd);

        Assert.IsTrue(Try(out var e, result), $"Expected the new file to just be removed: {e}");
        Assert.IsFalse(File.Exists(Path.Join(wd, "new.txt")));
    }

    // Any other failure is an error, the file is not touched
    [TestMethod]
    public async Task TestUndoUncommittedFileOtherFailureIsAnError()
    {
        File.WriteAllText(Path.Join(wd, "a.txt"), "a");
        var cmd = new FakeCmd((_, _, _) => FakeCmd.Fail("fatal: not a git repository"));

        var result = await new CommitService(cmd).UndoUncommittedFileAsync("a.txt", wd);

        Assert.IsFalse(Try(out var _, result), "Expected the git failure to propagate");
        Assert.IsTrue(File.Exists(Path.Join(wd, "a.txt")), "The file is left alone");
    }

    // Undoing a merge commit needs the parent to revert against, an ordinary commit does not
    [TestMethod]
    public async Task TestUndoCommit()
    {
        var cmd = new FakeCmd("");
        var service = new CommitService(cmd);

        await service.UndoCommitAsync("abc123", 0, wd);
        await service.UndoCommitAsync("abc123", 1, wd);

        Assert.AreEqual("revert  --no-commit abc123", cmd.Calls[0].Args);
        Assert.AreEqual("revert -m 1 --no-commit abc123", cmd.Calls[1].Args);
    }

    // Uncommit keeps the changes, reset hard throws them away
    [TestMethod]
    public async Task TestUncommitAndReset()
    {
        var cmd = new FakeCmd("");
        var service = new CommitService(cmd);

        await service.UncommitLastCommitAsync(wd);
        await service.UncommitUntilCommitAsync("abc123", wd);
        await service.ResetHardUntilCommitAsync("abc123", wd);

        CollectionAssert.AreEqual(new[] { "reset HEAD~1", "reset --soft abc123", "reset --hard abc123" }, ArgsOf(cmd));
    }
}
