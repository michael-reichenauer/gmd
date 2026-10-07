using gmd.Git;
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

    // The reset of the current branch is made only while HEAD is on that branch, where it was read
    [TestMethod]
    [DataRow("a1\nrefs/heads/main\n", null, DisplayName = "Where it was read")]
    [DataRow("a2\nrefs/heads/main\n", "'main' has moved since it was read", DisplayName = "Moved")]
    [DataRow("a1\nrefs/heads/dev\n", "'main' is no longer checked out", DisplayName = "Switched")]
    [DataRow("a1\nHEAD\n", "'main' is no longer checked out", DisplayName = "Detached")]
    public async Task TestResetBranchOnlyFromWhereItWasRead(string head, string? error)
    {
        var cmd = new FakeCmd((_, args, _) => FakeCmd.Ok(args.StartsWith("rev-parse") ? head : ""));

        var result = await new CommitService(cmd).ResetBranchAsync("main", "a0", "a1", true, wd);

        if (error == null)
        {
            AssertOk(result);
            CollectionAssert.AreEqual(
                new[] { "rev-parse HEAD --symbolic-full-name HEAD", "reset --keep a0" },
                ArgsOf(cmd)
            );
        }
        else
        {
            StringAssert.StartsWith(AssertError(result).Message, error);
            Assert.AreEqual(1, cmd.Calls.Count, "Not reset");
        }
    }

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

    // While an operation is in progress nothing is staged — and that means '-a' as well as
    // 'git add .', which is the half this used to miss. Both stage an unmerged path with whatever
    // the working tree holds, so 'git commit -am' on a conflicted merge succeeds and commits the
    // '<<<<<<<' markers into history (verified against real git; see ConflictIntegrationTest).
    // The operation staged its own result, and a resolved file is staged explicitly, so a plain
    // 'commit -m' commits exactly what is meant to be committed.
    [TestMethod]
    [DataRow("MERGE_MSG", "Merge branch 'topic'\n")]
    [DataRow("CHERRY_PICK_HEAD", "3883620149\n")]
    [DataRow("REVERT_HEAD", "3883620149\n")]
    public async Task TestCommitStagesNothingWhileAnOperationIsInProgress(string name, string content)
    {
        File.WriteAllText(Path.Join(wd, ".git", name), content);
        var cmd = new FakeCmd("");

        await new CommitService(cmd).CommitAllChangesAsync("The message", false, wd);

        CollectionAssert.AreEqual(new[] { "commit -m \"The message\"" }, ArgsOf(cmd));
    }

    // A rebase with the --apply backend, and 'git am', write no MERGE_MSG at all. That is what made
    // them invisible to the old check and let 'git add .' resolve their conflicts irrecoverably.
    [TestMethod]
    [DataRow("rebase-merge")]
    [DataRow("rebase-apply")]
    public async Task TestCommitStagesNothingWhileRebasing(string dir)
    {
        Directory.CreateDirectory(Path.Join(wd, ".git", dir));
        var cmd = new FakeCmd("");

        await new CommitService(cmd).CommitAllChangesAsync("The message", false, wd);

        CollectionAssert.AreEqual(new[] { "commit -m \"The message\"" }, ArgsOf(cmd));
    }

    [TestMethod]
    public async Task TestCommitStopsIfStagingFails()
    {
        var cmd = new FakeCmd((_, _, _) => FakeCmd.Fail("fatal: not a git repository"));

        var result = await new CommitService(cmd).CommitAllChangesAsync("The message", false, wd);

        AssertError(result, "Expected the git failure to propagate");
        Assert.AreEqual(1, cmd.Calls.Count, "The commit is not attempted");
    }

    // No paths is no files: an empty commit, which carries the new message of an older commit amended
    // with no file. Never a pathspec file with nothing in it, which git takes as no pathspec at all,
    // i.e. everything.
    [TestMethod]
    public async Task TestCommitOfNoFilesCommitsNothing()
    {
        var cmd = new FakeCmd("");

        await new CommitService(cmd).CommitFilesAsync("The message", false, [], wd);

        CollectionAssert.AreEqual(new[] { "commit --only --allow-empty -m \"The message\"" }, ArgsOf(cmd));
    }

    // The amend of an older commit folds the 'amend!' commit into it with git's own --autosquash,
    // from its parent, or from the start for the first commit, which has none
    [TestMethod]
    [DataRow("a1^", "rebase -i --autosquash --autostash --empty=drop a1^")]
    [DataRow("", "rebase -i --autosquash --autostash --empty=drop --root")]
    public async Task TestAutosquash(string baseId, string args)
    {
        var cmd = new FakeCmd("");

        AssertOk(await new CommitService(cmd).AutosquashAsync(baseId, wd));

        CollectionAssert.AreEqual(new[] { args }, ArgsOf(cmd));
    }

    [TestMethod]
    public async Task TestDropCommitReplaysTheNewerOnesOntoItsParent()
    {
        var cmd = new FakeCmd("");

        AssertOk(await new CommitService(cmd).DropCommitAsync("a1", wd));

        CollectionAssert.AreEqual(new[] { "rebase --empty=drop --onto a1^ a1" }, ArgsOf(cmd));
    }

    // A rewrite that conflicts stops part way, which the command runner shows as the conflicts, with
    // the way on, rather than as an error
    [TestMethod]
    public async Task TestARewriteThatConflictsIsAConflict()
    {
        var cmd = new FakeCmd((_, _, _) => FakeCmd.Problems("CONFLICT (content): Merge conflict in a.txt", 1));
        var service = new CommitService(cmd);

        Assert.IsInstanceOfType<ConflictError>(AssertError(await service.AutosquashAsync("a1^", wd)));
        Assert.IsInstanceOfType<ConflictError>(AssertError(await service.DropCommitAsync("a1", wd)));
    }

    [TestMethod]
    public async Task TestTheRefsContainingACommit()
    {
        var cmd = new FakeCmd("refs/heads/main\nrefs/remotes/origin/main\nrefs/tags/v1\n");

        var refs = AssertOk(await new CommitService(cmd).GetRefsContainingAsync("a1", wd));

        CollectionAssert.AreEqual(
            new[] { "refs/heads/main", "refs/remotes/origin/main", "refs/tags/v1" },
            refs.ToArray()
        );
        Assert.AreEqual(
            "for-each-ref --contains a1 --format=%(refname) refs/heads refs/remotes refs/tags",
            cmd.Calls[0].Args
        );
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

    // What the clean would delete is git's dry run of the same clean, a folder once rather than its files
    [TestMethod]
    public async Task TestTheFilesToCleanAreTheDryRunOfTheClean()
    {
        var cmd = new FakeCmd("Would remove .env\nWould remove bin/\nWould remove notes.txt\n");

        var files = AssertOk(await new CommitService(cmd).GetFilesToCleanAsync(wd));

        CollectionAssert.AreEqual(new[] { ".env", "bin/", "notes.txt" }, files.ToArray());
        CollectionAssert.AreEqual(new[] { "clean -n -x -d" }, ArgsOf(cmd));
    }

    [TestMethod]
    public async Task TestUndoUncommittedFile()
    {
        var cmd = new FakeCmd("");

        await new CommitService(cmd).UndoUncommittedFileAsync("src/a.txt", wd);

        // From the last commit, so that what is staged is discarded too
        Assert.AreEqual("checkout --force HEAD -- \"src/a.txt\"", cmd.Calls[0].Args);
    }

    // A new file is not in the last commit, so it cannot be checked out from it: it is taken out of
    // the index, in case it was staged, and removed
    [TestMethod]
    public async Task TestUndoUncommittedFileRemovesANewFile()
    {
        File.WriteAllText(Path.Join(wd, "new.txt"), "new");
        var cmd = new FakeCmd(
            (_, args, _) =>
                args.StartsWith("checkout")
                    ? FakeCmd.Fail("error: pathspec 'new.txt' did not match any file(s) known to git")
                    : FakeCmd.Ok("")
        );

        var result = await new CommitService(cmd).UndoUncommittedFileAsync("new.txt", wd);

        AssertOk(result);
        Assert.AreEqual("rm --cached --ignore-unmatch -q -- \"new.txt\"", cmd.Calls[1].Args);
        Assert.IsFalse(File.Exists(Path.Join(wd, "new.txt")));
    }

    // Any other failure is an error, the file is not touched
    [TestMethod]
    public async Task TestUndoUncommittedFileOtherFailureIsAnError()
    {
        File.WriteAllText(Path.Join(wd, "a.txt"), "a");
        var cmd = new FakeCmd((_, _, _) => FakeCmd.Fail("fatal: not a git repository"));

        var result = await new CommitService(cmd).UndoUncommittedFileAsync("a.txt", wd);

        AssertError(result, "Expected the git failure to propagate");
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
