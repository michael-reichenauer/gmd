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

    // GetStatusAsync also reads .git/MERGE_MSG and .git/MERGE_HEAD, so it needs a working
    // directory. A plain temp folder with a .git in it is enough, no repository required.
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

    // The remaining conflict kinds, which are harder to reproduce but git documents them
    [DataTestMethod]
    [DataRow("DD both-deleted.txt")]
    [DataRow("AU added-by-us.txt")]
    [DataRow("UA added-by-them.txt")]
    public async Task TestParseRareConflictKinds(string line)
    {
        var status = await GetStatusAsync(line);

        Assert.AreEqual(1, status.Conflicted, $"'{line}' should be a conflict");
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
        Assert.IsFalse(StatusService.IsMergeInProgress(wd));
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
        Assert.IsTrue(StatusService.IsMergeInProgress(wd));
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
}
