using gmd.Git;
using gmd.Git.Private;
using gmdTest.Utils;

namespace gmdTest.Git;

[TestClass]
public class StatusServiceTest
{
    // Output of: git status -s --porcelain --ahead-behind --untracked-files=all, captured from a
    // repo with one file of every kind: staged add, unstaged delete, unstaged modify, rename,
    // staged delete, staged modify, and two untracked files, one with a space in its name (which
    // git quotes).
    const string StatusOutput = """
        A  added.txt
         D del.txt
         M mod.txt
        R  ren.txt -> renamed.txt
        D  stagedel.txt
        M  stagemod.txt
        ?? "another file.txt"
        ?? untracked.txt
        """;

    // The four conflict kinds of a merge with conflicts: added by both, modified by both, deleted
    // by them, deleted by us
    const string ConflictOutput = """
        AA addboth.txt
        UU both.txt
        UD del-mod.txt
        DU mod-del.txt
        """;

    // GetStatusAsync probes the git dir for the files git leaves behind while an operation is in
    // progress, so it needs a working directory. A plain temp folder with a .git in it is enough,
    // no repository required — which is the whole reason these can be fast tests.
    string wd = "";

    [TestInitialize]
    public void Init()
    {
        wd = Path.Join(Path.GetTempPath(), $"gmdTest-status-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Join(wd, ".git"));
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(wd))
            Directory.Delete(wd, true);
    }

    // Writes the files git leaves behind while a merge is in progress
    void StartMerge(string message, string? mergeHeadId = null)
    {
        File.WriteAllText(Path.Join(wd, ".git", "MERGE_MSG"), message);
        if (mergeHeadId != null)
            File.WriteAllText(Path.Join(wd, ".git", "MERGE_HEAD"), mergeHeadId + "\n");
    }

    void WriteGitFile(string name, string content) => File.WriteAllText(Path.Join(wd, ".git", name), content);

    void WriteGitDirFile(string dir, string name, string content)
    {
        Directory.CreateDirectory(Path.Join(wd, ".git", dir));
        File.WriteAllText(Path.Join(wd, ".git", dir, name), content);
    }

    async Task<gmd.Git.Status> GetStatusAsync(string output)
    {
        var service = new StatusService(new FakeCmd(output));
        var result = await service.GetStatusAsync(wd);
        Assert.IsTrue(Try(out var status, out var e, result), $"GetStatusAsync failed: {e}");
        return status;
    }

    // Note 'added.txt': a staged add ('A  ') is counted as modified, not added, since the leading
    // status column is trimmed away before the prefixes are compared. Only untracked files ('?? ')
    // reach AddedFiles. This is invisible today — every consumer either concatenates the file lists
    // or uses Status.ChangesCount, which is their sum.
    [TestMethod]
    public async Task TestParseCounts()
    {
        var status = await GetStatusAsync(StatusOutput);

        Assert.AreEqual("M:3,A:2,D:2,C:0,R:1", status.ToString());
    }

    [TestMethod]
    public async Task TestParseModifiedFiles()
    {
        var status = await GetStatusAsync(StatusOutput);

        CollectionAssert.AreEqual(new[] { "added.txt", "mod.txt", "stagemod.txt" }, status.ModifiedFiles);
    }

    // Git quotes a path containing a space, the quotes are not part of the name
    [TestMethod]
    public async Task TestParseAddedFilesAreTheUntrackedOnesAndAreUnquoted()
    {
        var status = await GetStatusAsync(StatusOutput);

        CollectionAssert.AreEqual(new[] { "another file.txt", "untracked.txt" }, status.AddedFiles);
    }

    [TestMethod]
    public async Task TestParseDeletedFiles()
    {
        var status = await GetStatusAsync(StatusOutput);

        CollectionAssert.AreEqual(new[] { "del.txt", "stagedel.txt" }, status.DeletedFiles);
    }

    // A rename is one line with both paths, split on ' -> '
    [TestMethod]
    public async Task TestParseRenamedFiles()
    {
        var status = await GetStatusAsync(StatusOutput);

        Assert.AreEqual(1, status.Renamed);
        CollectionAssert.AreEqual(new[] { "ren.txt" }, status.RenamedSourceFiles);
        CollectionAssert.AreEqual(new[] { "renamed.txt" }, status.RenamedTargetFiles);
    }

    [TestMethod]
    public async Task TestParseRenamedFilesWithSpacesInNames()
    {
        var status = await GetStatusAsync("R  \"old name.txt\" -> \"new name.txt\"");

        CollectionAssert.AreEqual(new[] { "old name.txt" }, status.RenamedSourceFiles);
        CollectionAssert.AreEqual(new[] { "new name.txt" }, status.RenamedTargetFiles);
    }

    [TestMethod]
    public async Task TestParseConflictedFiles()
    {
        var status = await GetStatusAsync(ConflictOutput);

        Assert.AreEqual("M:0,A:0,D:0,C:4,R:0", status.ToString());
        CollectionAssert.AreEqual(
            new[] { "addboth.txt", "both.txt", "del-mod.txt", "mod-del.txt" },
            status.ConflictsFiles
        );
    }

    // The XY code is the only record of what git could not merge, and it decides what can be
    // offered for the path: a modify/delete has no text to merge, only a keep-or-delete choice
    [TestMethod]
    public async Task TestParseKeepsTheKindOfEachConflict()
    {
        var status = await GetStatusAsync(ConflictOutput);

        CollectionAssert.AreEqual(
            new[]
            {
                new ConflictedFile("addboth.txt", ConflictKind.BothAdded),
                new ConflictedFile("both.txt", ConflictKind.BothModified),
                new ConflictedFile("del-mod.txt", ConflictKind.DeletedByThem),
                new ConflictedFile("mod-del.txt", ConflictKind.DeletedByUs),
            },
            status.Conflicts
        );
    }

    // The remaining conflict kinds, which are harder to reproduce but git documents them
    [TestMethod]
    [DataRow("DD both-deleted.txt", ConflictKind.BothDeleted)]
    [DataRow("AU added-by-us.txt", ConflictKind.AddedByUs)]
    [DataRow("UA added-by-them.txt", ConflictKind.AddedByThem)]
    public async Task TestParseRareConflictKinds(string line, ConflictKind kind)
    {
        var status = await GetStatusAsync(line);

        Assert.AreEqual(1, status.Conflicted, $"'{line}' should be a conflict");
        Assert.AreEqual(kind, status.Conflicts[0].Kind);
    }

    // A quoted path is unquoted like any other, and the ' -> ' of a rename is not involved
    [TestMethod]
    public async Task TestParseConflictedFileWithSpacesInName()
    {
        var status = await GetStatusAsync("UU \"some file.txt\"");

        Assert.AreEqual(new ConflictedFile("some file.txt", ConflictKind.BothModified), status.Conflicts[0]);
    }

    [TestMethod]
    public async Task TestParseEmptyStatusIsNoChanges()
    {
        var status = await GetStatusAsync("");

        Assert.AreEqual("M:0,A:0,D:0,C:0,R:0", status.ToString());
        Assert.AreEqual(0, status.ModifiedFiles.Length);
    }

    // Real git output ends with a newline, the trailing empty line is not a file
    [TestMethod]
    public async Task TestParseIgnoresTrailingNewline()
    {
        var status = await GetStatusAsync(" M mod.txt\n");

        Assert.AreEqual("M:1,A:0,D:0,C:0,R:0", status.ToString());
    }

    [TestMethod]
    public async Task TestNotMergingWhenNoMergeMsgFile()
    {
        var status = await GetStatusAsync(StatusOutput);

        Assert.IsFalse(status.IsMerging);
        Assert.AreEqual("", status.MergeMessage);
        Assert.AreEqual("", status.MergeHeadId);
        Assert.IsFalse(StatusService.IsOperationInProgress(wd));
    }

    // While merging, the message is the first line of MERGE_MSG — the rest is git's '# Conflicts:'
    // comment block
    [TestMethod]
    public async Task TestMergingReadsMessageAndHeadId()
    {
        StartMerge(
            "Merge branch 'topic'\n\n# Conflicts:\n#\taddboth.txt\n",
            "388362014956bb2f054d1699d4853904194e3dab"
        );

        var status = await GetStatusAsync(ConflictOutput);

        Assert.IsTrue(status.IsMerging);
        Assert.AreEqual("Merge branch 'topic'", status.MergeMessage);
        Assert.AreEqual("388362014956bb2f054d1699d4853904194e3dab", status.MergeHeadId);
        Assert.IsTrue(StatusService.IsOperationInProgress(wd));
    }

    // A squash merge or a merge of an unrelated ref writes MERGE_MSG but no MERGE_HEAD
    [TestMethod]
    public async Task TestMergingWithoutMergeHeadFile()
    {
        StartMerge("Merge branch 'topic'\n");

        var status = await GetStatusAsync("");

        Assert.IsTrue(status.IsMerging);
        Assert.AreEqual("Merge branch 'topic'", status.MergeMessage);
        Assert.AreEqual("", status.MergeHeadId);
    }

    // Every operation git can stop part way through, and what it leaves behind to say so. There is
    // no porcelain command that reports this, so probing the git dir is the only way — it is what
    // git's own wt_status_get_state() does.
    [TestMethod]
    public async Task TestCherryPickIsDetected()
    {
        WriteGitFile("CHERRY_PICK_HEAD", "388362014956bb2f054d1699d4853904194e3dab\n");
        WriteGitFile("MERGE_MSG", "Add gamma\n");

        var status = await GetStatusAsync(ConflictOutput);

        Assert.AreEqual(GitOperation.CherryPick, status.Operation);
        Assert.AreEqual("388362014956bb2f054d1699d4853904194e3dab", status.MergeHeadId);
        Assert.AreEqual("Add gamma", status.MergeMessage);
        Assert.IsTrue(status.IsMerging, "IsMerging is any operation, not only a merge");
    }

    [TestMethod]
    public async Task TestRevertIsDetected()
    {
        WriteGitFile("REVERT_HEAD", "388362014956bb2f054d1699d4853904194e3dab\n");

        var status = await GetStatusAsync("");

        Assert.AreEqual(GitOperation.Revert, status.Operation);
        Assert.AreEqual("388362014956bb2f054d1699d4853904194e3dab", status.MergeHeadId);
    }

    // The merge backend, which git has used by default since 2.26
    [TestMethod]
    public async Task TestRebaseIsDetectedWithItsProgress()
    {
        WriteGitDirFile("rebase-merge", "head-name", "refs/heads/dev\n");
        WriteGitDirFile("rebase-merge", "msgnum", "3\n");
        WriteGitDirFile("rebase-merge", "end", "7\n");

        var status = await GetStatusAsync(ConflictOutput);

        Assert.AreEqual(GitOperation.Rebase, status.Operation);
        Assert.AreEqual("dev", status.OperationBranchName, "'refs/heads/' is trimmed off");
        Assert.AreEqual(3, status.OperationStep);
        Assert.AreEqual(7, status.OperationTotal);
    }

    // An interactive rebase is not a separate kind. Modern git runs plain and interactive rebases
    // alike through the sequencer and writes 'rebase-merge/interactive' for both, so that file no
    // longer tells them apart — and nothing needs the difference, since --continue, --skip and
    // --abort are the same commands either way.
    [TestMethod]
    public async Task TestInteractiveRebaseIsJustARebase()
    {
        WriteGitDirFile("rebase-merge", "head-name", "refs/heads/dev\n");
        WriteGitDirFile("rebase-merge", "interactive", "");

        var status = await GetStatusAsync("");

        Assert.AreEqual(GitOperation.Rebase, status.Operation);
    }

    // The --apply backend and 'git am' share a directory; only the 'applying' file separates them
    [TestMethod]
    public async Task TestRebaseWithApplyBackendIsDetected()
    {
        WriteGitDirFile("rebase-apply", "next", "2\n");
        WriteGitDirFile("rebase-apply", "last", "4\n");

        var status = await GetStatusAsync("");

        Assert.AreEqual(GitOperation.Rebase, status.Operation);
        Assert.AreEqual(2, status.OperationStep);
        Assert.AreEqual(4, status.OperationTotal);
    }

    [TestMethod]
    public async Task TestAmIsDetected()
    {
        WriteGitDirFile("rebase-apply", "applying", "");

        var status = await GetStatusAsync("");

        Assert.AreEqual(GitOperation.Am, status.Operation);
    }

    // The bug this detection exists for. A rebase with the --apply backend and 'git am' write no
    // MERGE_MSG, so the old .git/MERGE_MSG test reported "not merging" — and both 'git add .' in
    // GetUncommittedDiff and 'git commit -a' then staged the unmerged paths, resolving the conflict
    // with the markers as content and dropping the stages with no way back but --abort.
    [TestMethod]
    [DataRow("rebase-apply")]
    [DataRow("rebase-merge")]
    public async Task TestRebaseIsInProgressEvenWithNoMergeMsg(string dir)
    {
        Directory.CreateDirectory(Path.Join(wd, ".git", dir));

        var status = await GetStatusAsync(ConflictOutput);

        Assert.IsFalse(File.Exists(Path.Join(wd, ".git", "MERGE_MSG")), "The fixture writes none");
        Assert.IsTrue(status.IsMerging);
        Assert.IsTrue(StatusService.IsOperationInProgress(wd), "What guards the two 'git add .' sites");
    }

    // A stopped rebase and a stopped cherry-pick both write MERGE_MSG, so it never meant "a merge
    // is in progress" and has to be tested for last
    [TestMethod]
    public async Task TestRebaseWinsOverMergeMsg()
    {
        WriteGitDirFile("rebase-merge", "head-name", "refs/heads/dev\n");
        StartMerge("Add gamma\n");

        var status = await GetStatusAsync("");

        Assert.AreEqual(GitOperation.Rebase, status.Operation);
    }

    // In a linked worktree and in a submodule '.git' is a file pointing at the real git dir, which
    // is where the operation state lives — so joining '.git' blindly finds none of it
    [TestMethod]
    public async Task TestGitDirIsFollowedWhenDotGitIsAFile()
    {
        var realGitDir = Path.Join(wd, "real-git-dir");
        Directory.CreateDirectory(realGitDir);
        Directory.Delete(Path.Join(wd, ".git"), true);
        File.WriteAllText(Path.Join(wd, ".git"), $"gitdir: {realGitDir}\n");
        File.WriteAllText(Path.Join(realGitDir, "CHERRY_PICK_HEAD"), "3883620149\n");

        var status = await GetStatusAsync("");

        Assert.AreEqual(GitOperation.CherryPick, status.Operation);
    }

    // A submodule's pointer is relative to the folder holding the '.git' file
    [TestMethod]
    public async Task TestRelativeGitDirIsResolvedAgainstTheWorkingFolder()
    {
        var realGitDir = Path.Join(wd, "modules", "sub");
        Directory.CreateDirectory(realGitDir);
        Directory.Delete(Path.Join(wd, ".git"), true);
        File.WriteAllText(Path.Join(wd, ".git"), "gitdir: modules/sub\n");
        File.WriteAllText(Path.Join(realGitDir, "REVERT_HEAD"), "3883620149\n");

        var status = await GetStatusAsync("");

        Assert.AreEqual(GitOperation.Revert, status.Operation);
    }

    [TestMethod]
    public async Task TestNoOperationWhenThereIsNoGitDir()
    {
        Directory.Delete(Path.Join(wd, ".git"), true);

        var status = await GetStatusAsync("");

        Assert.AreEqual(GitOperation.None, status.Operation);
        Assert.IsFalse(status.IsMerging);
    }

    [TestMethod]
    public async Task TestGitCommandFailureIsPropagated()
    {
        var service = new StatusService(new FakeCmd((_, _, _) => FakeCmd.Fail("fatal: not a git repository")));

        var result = await service.GetStatusAsync(wd);

        Assert.IsFalse(Try(out var _, out var _, result), "Expected the git failure to propagate");
    }

    [TestMethod]
    public async Task TestGetStatusPassesArgsAndWorkingDirectoryToGit()
    {
        var cmd = new FakeCmd("");
        var service = new StatusService(cmd);

        await service.GetStatusAsync(wd);

        Assert.AreEqual(1, cmd.Calls.Count);
        Assert.AreEqual("git", cmd.Calls[0].Path);
        Assert.AreEqual(wd, cmd.Calls[0].WorkingDirectory);
        Assert.AreEqual("status -s --porcelain --ahead-behind --untracked-files=all", cmd.Calls[0].Args);
    }

    // gmd's own Undo/Revert Commit runs 'revert --no-commit', which stages one change for the
    // commit dialog and queues nothing behind it — but records REVERT_HEAD all the same, and does
    // so even when the revert applies cleanly. Testing the operation alone therefore said "a revert
    // is in progress, continue it" about an ordinary staged revert, and its commit dialog could not
    // be opened at all.
    [TestMethod]
    public async Task TestASingleRevertIsFinishedByCommitting()
    {
        WriteGitFile("REVERT_HEAD", "388362014956bb2f054d1699d4853904194e3dab\n");

        var status = await GetStatusAsync("");

        Assert.IsTrue(status.IsMerging, "It is still an operation in progress, so 'git add .' stays guarded");
        Assert.IsTrue(status.IsFinishedByCommit, "but a commit is the whole of what is left of it");
        Assert.IsTrue(StatusService.IsFinishedByCommit(wd));
    }

    // A revert of several commits does have to be continued: a commit would make the one git
    // stopped on and leave the rest unapplied. Git writes a sequencer todo only for that form,
    // which is what tells it apart from the single '--no-commit' one above.
    [TestMethod]
    public async Task TestARevertOfSeveralCommitsIsNotFinishedByCommitting()
    {
        WriteGitFile("REVERT_HEAD", "388362014956bb2f054d1699d4853904194e3dab\n");
        WriteGitDirFile("sequencer", "todo", "revert 3883620 Add gamma\n");

        var status = await GetStatusAsync(ConflictOutput);

        Assert.IsFalse(status.IsFinishedByCommit);
        Assert.IsFalse(StatusService.IsFinishedByCommit(wd));
    }

    // A cherry pick needs no such test: gmd's own runs '--no-commit', which writes no
    // CHERRY_PICK_HEAD at all (see TestOperationOfGmdsOwnCherryPickIsAMerge), so anything that has
    // one was started outside gmd and is git driving a sequence to continue.
    [TestMethod]
    public async Task TestACherryPickIsAlwaysContinued()
    {
        WriteGitFile("CHERRY_PICK_HEAD", "388362014956bb2f054d1699d4853904194e3dab\n");

        Assert.IsFalse((await GetStatusAsync(ConflictOutput)).IsFinishedByCommit);
        Assert.IsFalse(StatusService.IsFinishedByCommit(wd));
    }

    // A merge has no '--continue' at all, and a rebase or an 'am' is never finished by the commit
    // it stopped on, however few commits are left
    [TestMethod]
    public async Task TestAMergeIsFinishedByCommittingButARebaseIsNot()
    {
        StartMerge("Merge branch 'topic'\n", "388362014956bb2f054d1699d4853904194e3dab");
        Assert.IsTrue((await GetStatusAsync("")).IsFinishedByCommit);

        WriteGitDirFile("rebase-merge", "head-name", "refs/heads/dev\n");
        Assert.IsFalse((await GetStatusAsync("")).IsFinishedByCommit);
    }

    // Nothing in progress: there is no operation a commit could fail to finish
    [TestMethod]
    public async Task TestNothingInProgressIsFinishedByCommitting()
    {
        Assert.IsTrue((await GetStatusAsync("")).IsFinishedByCommit);
        Assert.IsTrue(StatusService.IsFinishedByCommit(wd));
    }

    // 'merge --abort' needs MERGE_HEAD. gmd's own Cherry Pick leaves a conflict with neither that
    // nor CHERRY_PICK_HEAD — 'cherry-pick --no-commit' writes only MERGE_MSG — so the abort has to
    // be told apart from one git can undo with a '--abort' verb. See ConflictService.
    [TestMethod]
    public async Task TestAMergeWithNoMergeHeadIsRecognised()
    {
        StartMerge("topic\n");

        Assert.AreEqual(GitOperation.Merge, (await GetStatusAsync(ConflictOutput)).Operation);
        Assert.IsTrue(StatusService.IsMergeWithoutHead(wd));
    }

    [TestMethod]
    public async Task TestARealMergeIsNotAMergeWithoutHead()
    {
        StartMerge("Merge branch 'topic'\n", "388362014956bb2f054d1699d4853904194e3dab");

        Assert.AreEqual(GitOperation.Merge, (await GetStatusAsync(ConflictOutput)).Operation);
        Assert.IsFalse(StatusService.IsMergeWithoutHead(wd));
    }
}
