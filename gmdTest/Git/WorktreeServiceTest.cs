using gmd.Git.Private;
using gmdTest.Utils;

namespace gmdTest.Git;

[TestClass]
public class WorktreeServiceTest
{
    const string Sha = "e7543463881cb81745a20158293985995668b919";

    // Output of: git worktree list --porcelain -z
    //
    // Every attribute ends with a NUL and every record with a second one, which is why the fixture
    // is assembled rather than pasted. Captured from a throwaway repo with, in this order: the main
    // worktree, a detached one, one on a branch, one whose folder was deleted, and a locked one.
    static readonly string ListOutput = string.Join(
        "\0",
        [
            $"worktree /home/me/repo",
            $"HEAD {Sha}",
            "branch refs/heads/main",
            "",
            "worktree /home/me/repo-detached",
            $"HEAD {Sha}",
            "detached",
            "",
            "worktree /home/me/repo-dev",
            $"HEAD {Sha}",
            "branch refs/heads/dev",
            "",
            "worktree /home/me/repo-gone",
            $"HEAD {Sha}",
            "branch refs/heads/gone",
            "prunable gitdir file points to non-existent location",
            "",
            "worktree /home/me/repo/.claude/worktrees/topic",
            $"HEAD {Sha}",
            "branch refs/heads/worktree-topic",
            "locked claude session 42 is using it",
            "",
            "",
        ]
    );

    static async Task<IReadOnlyList<gmd.Git.Worktree>> ListAsync(FakeCmd cmd)
    {
        var result = await new WorktreeService(cmd).ListAsync("/wd");
        Assert.IsTrue(Try(out var worktrees, out var e, result), $"ListAsync failed: {e}");
        return worktrees;
    }

    [TestMethod]
    public async Task TestListParsesEveryKindOfWorktree()
    {
        var cmd = new FakeCmd(ListOutput);

        var worktrees = await ListAsync(cmd);

        Assert.AreEqual("worktree list --porcelain -z", cmd.Calls[0].Args);
        Assert.AreEqual(5, worktrees.Count);

        var main = worktrees[0];
        Assert.AreEqual(Path.GetFullPath("/home/me/repo"), main.Path);
        Assert.AreEqual(Sha, main.HeadId);
        Assert.AreEqual("main", main.Branch, "The refs/heads/ prefix is trimmed");
        Assert.IsTrue(main.IsMain, "The first record is the main worktree");
        Assert.IsFalse(main.IsBare);
        Assert.IsFalse(main.IsDetached);
        Assert.IsFalse(main.IsLocked);
        Assert.IsFalse(main.IsPrunable);

        var detached = worktrees[1];
        Assert.IsTrue(detached.IsDetached);
        Assert.AreEqual("", detached.Branch);
        Assert.IsFalse(detached.IsMain);

        var dev = worktrees[2];
        Assert.AreEqual(Path.GetFullPath("/home/me/repo-dev"), dev.Path);
        Assert.AreEqual("dev", dev.Branch);
        Assert.IsFalse(dev.IsMain);

        var gone = worktrees[3];
        Assert.IsTrue(gone.IsPrunable);
        Assert.AreEqual("gitdir file points to non-existent location", gone.PruneReason);
        Assert.IsFalse(gone.IsLocked);

        var locked = worktrees[4];
        Assert.IsTrue(locked.IsLocked);
        Assert.AreEqual("claude session 42 is using it", locked.LockReason, "A reason with spaces is kept whole");
        Assert.AreEqual("worktree-topic", locked.Branch);
        Assert.IsFalse(locked.IsPrunable);
    }

    // A bare repository has a main worktree with no checkout at all
    [TestMethod]
    public async Task TestListOfABareRepository()
    {
        var output = string.Join("\0", ["worktree /home/me/repo.git", "bare", "", ""]);

        var worktrees = await ListAsync(new FakeCmd(output));

        Assert.AreEqual(1, worktrees.Count);
        Assert.IsTrue(worktrees[0].IsBare);
        Assert.IsTrue(worktrees[0].IsMain);
        Assert.AreEqual("", worktrees[0].Branch);
    }

    // A lock without a reason and an attribute this version does not know
    [TestMethod]
    public async Task TestListToleratesAnEmptyReasonAndUnknownAttributes()
    {
        var output = string.Join(
            "\0",
            [
                "worktree /home/me/repo",
                $"HEAD {Sha}",
                "branch refs/heads/main",
                "someday-attribute value",
                "locked",
                "",
                "",
            ]
        );

        var worktrees = await ListAsync(new FakeCmd(output));

        Assert.AreEqual(1, worktrees.Count);
        Assert.IsTrue(worktrees[0].IsLocked);
        Assert.AreEqual("", worktrees[0].LockReason);
    }

    [TestMethod]
    public async Task TestListOfNothingIsEmpty()
    {
        Assert.AreEqual(0, (await ListAsync(new FakeCmd(""))).Count);
    }

    [TestMethod]
    public async Task TestListFailurePropagates()
    {
        var cmd = new FakeCmd((_, _, _) => FakeCmd.Fail("fatal: not a git repository"));

        Assert.IsFalse(Try(out var _, out var _, await new WorktreeService(cmd).ListAsync("/wd")));
    }

    [TestMethod]
    public async Task TestAddChecksOutAnExistingBranch()
    {
        var cmd = new FakeCmd("");

        Assert.IsTrue(
            Try(out var e, await new WorktreeService(cmd).AddAsync("/home/me/repo-dev", "dev", "/wd")),
            $"{e}"
        );

        Assert.AreEqual("worktree add \"/home/me/repo-dev\" dev", cmd.Calls[0].Args);
        Assert.AreEqual("/wd", cmd.Calls[0].WorkingDirectory);
    }

    [TestMethod]
    public async Task TestAddNewBranchFromAStartPointOrFromHead()
    {
        var cmd = new FakeCmd("");
        var service = new WorktreeService(cmd);

        Assert.IsTrue(
            Try(out var e, await service.AddNewBranchAsync("/home/me/repo-dev", "dev", "main", "/wd")),
            $"{e}"
        );
        Assert.IsTrue(Try(out e, await service.AddNewBranchAsync("/home/me/repo-dev", "dev", "", "/wd")), $"{e}");

        Assert.AreEqual("worktree add -b dev \"/home/me/repo-dev\" main", cmd.Calls[0].Args);
        Assert.AreEqual("worktree add -b dev \"/home/me/repo-dev\"", cmd.Calls[1].Args, "No start point means HEAD");
    }

    [TestMethod]
    public async Task TestRemoveIsForcedOnlyWhenAsked()
    {
        var cmd = new FakeCmd("");
        var service = new WorktreeService(cmd);

        Assert.IsTrue(Try(out var e, await service.RemoveAsync("/home/me/repo-dev", false, "/wd")), $"{e}");
        Assert.IsTrue(Try(out e, await service.RemoveAsync("/home/me/repo-dev", true, "/wd")), $"{e}");

        Assert.AreEqual("worktree remove \"/home/me/repo-dev\"", cmd.Calls[0].Args);
        Assert.AreEqual("worktree remove --force \"/home/me/repo-dev\"", cmd.Calls[1].Args);
    }

    // Git refuses a worktree with changes unless forced, which is an error the caller shows
    [TestMethod]
    public async Task TestRemoveOfADirtyWorktreeFailsWithoutForce()
    {
        var cmd = new FakeCmd(
            (_, _, _) =>
                FakeCmd.Fail(
                    "fatal: '/home/me/repo-dev' contains modified or untracked files, use --force to delete it",
                    128
                )
        );

        var result = await new WorktreeService(cmd).RemoveAsync("/home/me/repo-dev", false, "/wd");

        Assert.IsFalse(Try(out var e, result));
        StringAssert.Contains(e.ErrorMessage, "use --force");
    }

    [TestMethod]
    public async Task TestPrune()
    {
        var cmd = new FakeCmd("");

        Assert.IsTrue(Try(out var e, await new WorktreeService(cmd).PruneAsync("/wd")), $"{e}");

        Assert.AreEqual("worktree prune", cmd.Calls[0].Args);
    }

    // Each folder is asked about with a trailing separator, since a folder-only ignore pattern
    // matches nothing for a folder that does not exist yet unless the path says it is one. Git
    // echoes the ignored ones as given.
    [TestMethod]
    public async Task TestIgnoredFoldersAreTheOnesGitEchoes()
    {
        var cmd = new FakeCmd(".worktrees/\n");

        var result = await new WorktreeService(cmd).GetIgnoredAsync([".claude/worktrees", ".worktrees"], "/wd");

        Assert.IsTrue(Try(out var ignored, out var e, result), $"{e}");
        Assert.AreEqual("check-ignore -- \".claude/worktrees/\" \".worktrees/\"", cmd.Calls[0].Args);
        CollectionAssert.AreEqual(new[] { ".worktrees" }, ignored.ToArray());
    }

    // Exit code 1 is git's way of saying none of them is ignored, not a failure
    [TestMethod]
    public async Task TestNoIgnoredFoldersIsAnEmptyListNotAnError()
    {
        var cmd = new FakeCmd((_, _, _) => FakeCmd.Fail("", 1));

        var result = await new WorktreeService(cmd).GetIgnoredAsync([".worktrees"], "/wd");

        Assert.IsTrue(Try(out var ignored, out var e, result), $"{e}");
        Assert.AreEqual(0, ignored.Count);
    }

    [TestMethod]
    public async Task TestIgnoredCheckFailurePropagates()
    {
        var cmd = new FakeCmd((_, _, _) => FakeCmd.Fail("fatal: not a git repository", 128));

        var result = await new WorktreeService(cmd).GetIgnoredAsync([".worktrees"], "/wd");

        Assert.IsFalse(Try(out var _, out var _, result));
    }

    [TestMethod]
    public async Task TestNothingToCheckAsksGitNothing()
    {
        var cmd = new FakeCmd("");

        var result = await new WorktreeService(cmd).GetIgnoredAsync([], "/wd");

        Assert.IsTrue(Try(out var ignored, out var e, result), $"{e}");
        Assert.AreEqual(0, ignored.Count);
        Assert.AreEqual(0, cmd.Calls.Count);
    }
}
