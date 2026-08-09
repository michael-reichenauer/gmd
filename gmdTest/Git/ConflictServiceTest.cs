using gmd.Git.Private;
using gmdTest.Utils;

namespace gmdTest.Git;

[TestClass]
public class ConflictServiceTest
{
    // ConflictService probes the git dir to know what '--abort' attaches to, so it needs a working
    // directory. A plain temp folder with a .git in it is enough, no repository required.
    string wd = "";

    [TestInitialize]
    public void Init()
    {
        wd = Path.Join(Path.GetTempPath(), $"gmdTest-conflict-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Join(wd, ".git"));
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(wd))
            Directory.Delete(wd, true);
    }

    void WriteGitFile(string name, string content) => File.WriteAllText(Path.Join(wd, ".git", name), content);

    void MakeGitDir(string name) => Directory.CreateDirectory(Path.Join(wd, ".git", name));

    [TestMethod]
    [DataRow("MERGE_HEAD", "merge --abort")]
    [DataRow("CHERRY_PICK_HEAD", "cherry-pick --abort")]
    [DataRow("REVERT_HEAD", "revert --abort")]
    public async Task TestAbortUsesTheVerbOfTheOperationInProgress(string stateFile, string expected)
    {
        WriteGitFile(stateFile, "3883620149\n");
        WriteGitFile("MERGE_MSG", "Some message\n");
        var cmd = new FakeCmd("");

        Assert.IsTrue(Try(out var e, await new ConflictService(cmd).AbortOperationAsync(wd)), $"{e}");

        Assert.AreEqual(expected, cmd.Calls[0].Args);
        Assert.AreEqual(wd, cmd.Calls[0].WorkingDirectory);
    }

    [TestMethod]
    [DataRow("rebase-merge", "rebase --abort")]
    [DataRow("rebase-apply", "rebase --abort")]
    public async Task TestAbortOfARebase(string dir, string expected)
    {
        MakeGitDir(dir);
        var cmd = new FakeCmd("");

        Assert.IsTrue(Try(out var e, await new ConflictService(cmd).AbortOperationAsync(wd)), $"{e}");

        Assert.AreEqual(expected, cmd.Calls[0].Args);
    }

    [TestMethod]
    public async Task TestAbortOfAnAm()
    {
        MakeGitDir("rebase-apply");
        WriteGitFile(Path.Join("rebase-apply", "applying"), "");
        var cmd = new FakeCmd("");

        await new ConflictService(cmd).AbortOperationAsync(wd);

        Assert.AreEqual("am --abort", cmd.Calls[0].Args);
    }

    [TestMethod]
    public async Task TestAbortWithNothingInProgressIsAnErrorAndRunsNoGit()
    {
        var cmd = new FakeCmd("");

        var result = await new ConflictService(cmd).AbortOperationAsync(wd);

        Assert.IsFalse(Try(out var _, result));
        Assert.AreEqual(0, cmd.Calls.Count, "Nothing to abort, so git is not run at all");
    }

    // '--continue' opens an editor on the commit message by default, which would hang gmd behind
    // the terminal it owns. ICmd cannot pass environment variables, so GIT_EDITOR is out and the
    // same thing is said as config instead.
    [TestMethod]
    public async Task TestContinueDisablesTheEditor()
    {
        MakeGitDir("rebase-merge");
        var cmd = new FakeCmd("");

        Assert.IsTrue(Try(out var e, await new ConflictService(cmd).ContinueOperationAsync(wd)), $"{e}");

        Assert.AreEqual("-c core.editor=true rebase --continue", cmd.Calls[0].Args);
    }

    [TestMethod]
    public async Task TestContinueOfACherryPick()
    {
        WriteGitFile("CHERRY_PICK_HEAD", "3883620149\n");
        var cmd = new FakeCmd("");

        await new ConflictService(cmd).ContinueOperationAsync(wd);

        Assert.AreEqual("-c core.editor=true cherry-pick --continue", cmd.Calls[0].Args);
    }

    // A merge is finished by committing it, which is a different command with a dialog behind it
    [TestMethod]
    public async Task TestContinueOfAMergeIsRefused()
    {
        WriteGitFile("MERGE_HEAD", "3883620149\n");
        WriteGitFile("MERGE_MSG", "Merge branch 'topic'\n");
        var cmd = new FakeCmd("");

        var result = await new ConflictService(cmd).ContinueOperationAsync(wd);

        Assert.IsFalse(Try(out var e, result));
        StringAssert.Contains(e.ErrorMessage, "finished by committing");
        Assert.AreEqual(0, cmd.Calls.Count);
    }

    [TestMethod]
    public async Task TestSkipIsOnlyForTheOperationsThatReplaySeveralCommits()
    {
        MakeGitDir("rebase-merge");
        var cmd = new FakeCmd("");

        await new ConflictService(cmd).SkipOperationAsync(wd);

        Assert.AreEqual("-c core.editor=true rebase --skip", cmd.Calls[0].Args);
    }

    [TestMethod]
    public async Task TestSkipOfAMergeIsRefused()
    {
        WriteGitFile("MERGE_HEAD", "3883620149\n");
        WriteGitFile("MERGE_MSG", "Merge branch 'topic'\n");
        var cmd = new FakeCmd("");

        var result = await new ConflictService(cmd).SkipOperationAsync(wd);

        Assert.IsFalse(Try(out var e, result));
        StringAssert.Contains(e.ErrorMessage, "no commit to skip");
        Assert.AreEqual(0, cmd.Calls.Count);
    }

    // Stopping again on the next commit is the normal shape of a rebase over several commits, not
    // a failure of the command, so it has to be told apart from a refusal — both exit non-zero
    [TestMethod]
    public async Task TestContinueThatStopsOnMoreConflictsSaysSo()
    {
        MakeGitDir("rebase-merge");
        var cmd = new FakeCmd((_, _, _) => FakeCmd.Fail("CONFLICT (content): Merge conflict in f.txt"));

        var result = await new ConflictService(cmd).ContinueOperationAsync(wd);

        Assert.IsFalse(Try(out var e, result));
        StringAssert.Contains(e.ErrorMessage, "stopped on more conflicts");
    }

    // 'git diff --cached --check' reports whitespace problems as well as conflict markers, and
    // exits non-zero for either. Gating on the exit code would therefore refuse a commit over a
    // trailing space, so the lines are filtered instead. This is the test that says so.
    [TestMethod]
    public async Task TestWhitespaceProblemsAreNotConflictMarkers()
    {
        var cmd = new FakeCmd(
            (_, _, _) => FakeCmd.Problems("f.txt:2: trailing whitespace.\n+b   \ng.txt:9: space before tab in indent.")
        );

        Assert.IsTrue(Try(out var paths, out var e, await new ConflictService(cmd).GetLeftoverMarkerPathsAsync(wd)));
        Assert.AreEqual(0, paths.Count, $"Whitespace is not a conflict marker: {string.Join(", ", paths)}");
    }

    [TestMethod]
    public async Task TestLeftoverMarkerPathsAreNamedOncePerFile()
    {
        var cmd = new FakeCmd(
            (_, _, _) =>
                FakeCmd.Problems(
                    "f.txt:1: leftover conflict marker\n"
                        + "f.txt:3: leftover conflict marker\n"
                        + "sub/g.txt:7: leftover conflict marker"
                )
        );

        Assert.IsTrue(Try(out var paths, out var e, await new ConflictService(cmd).GetLeftoverMarkerPathsAsync(wd)));
        CollectionAssert.AreEqual(new[] { "f.txt", "sub/g.txt" }, paths.ToArray());
        Assert.AreEqual("diff --cached --check", cmd.Calls[0].Args);
    }

    // Whitespace and markers together, which is what a half resolved file usually looks like
    [TestMethod]
    public async Task TestMarkersAreFoundAmongWhitespaceProblems()
    {
        var cmd = new FakeCmd(
            (_, _, _) => FakeCmd.Problems("f.txt:2: trailing whitespace.\n+trail   \nf.txt:3: leftover conflict marker")
        );

        Assert.IsTrue(Try(out var paths, out var _, await new ConflictService(cmd).GetLeftoverMarkerPathsAsync(wd)));
        CollectionAssert.AreEqual(new[] { "f.txt" }, paths.ToArray());
    }

    // A path may itself contain a colon, so the line number is taken from the right
    [TestMethod]
    public async Task TestPathWithAColonInIt()
    {
        var cmd = new FakeCmd((_, _, _) => FakeCmd.Problems("od:d/f:1.txt:12: leftover conflict marker"));

        Assert.IsTrue(Try(out var paths, out var _, await new ConflictService(cmd).GetLeftoverMarkerPathsAsync(wd)));
        CollectionAssert.AreEqual(new[] { "od:d/f:1.txt" }, paths.ToArray());
    }

    [TestMethod]
    public async Task TestNothingStagedIsNoMarkers()
    {
        var cmd = new FakeCmd("");

        Assert.IsTrue(Try(out var paths, out var _, await new ConflictService(cmd).GetLeftoverMarkerPathsAsync(wd)));
        Assert.AreEqual(0, paths.Count);
    }

    // Findings go to stdout; anything on stderr means the command itself did not run
    [TestMethod]
    public async Task TestARealFailureIsPropagated()
    {
        var cmd = new FakeCmd((_, _, _) => FakeCmd.Fail("fatal: not a git repository"));

        var result = await new ConflictService(cmd).GetLeftoverMarkerPathsAsync(wd);

        Assert.IsFalse(Try(out var _, out var _, result));
    }

    [TestMethod]
    public async Task TestContinueWithUnresolvedConflictsSaysWhatIsMissing()
    {
        MakeGitDir("rebase-merge");
        var cmd = new FakeCmd(
            (_, _, _) => FakeCmd.Fail("f.txt: needs merge\nYou must edit all merge conflicts and then\nmark them")
        );

        var result = await new ConflictService(cmd).ContinueOperationAsync(wd);

        Assert.IsFalse(Try(out var e, result));
        StringAssert.Contains(e.ErrorMessage, "unresolved conflicts");
    }
}
