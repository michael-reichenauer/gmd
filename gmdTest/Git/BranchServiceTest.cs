using gmd.Git.Private;
using gmdTest.Utils;

namespace gmdTest.Git;

[TestClass]
public class BranchServiceTest
{
    // Output of: git branch -vv --no-color --no-abbrev --all
    //
    // Captured from a throwaway repo with a local 'origin' remote, so it covers the shapes that
    // matter: ahead, behind, both, a 'gone' upstream, no upstream, the
    // 'remotes/origin/HEAD -> origin/main' pointer line, and a branch name with '/' and '.'.
    const string BranchesOutput = """
          ahead-branch                ee18e116dcf7bcda232caccfa82262c5d9bcad56 [origin/ahead-branch: ahead 1] Ahead two
          behind-branch               8ec7ceea6c4e13464c9cee83fccda637a66a1023 [origin/behind-branch: behind 1] Initial
          both-branch                 0702c083a309e3060742f7d8b7bede7bb8018620 [origin/both-branch: ahead 1, behind 1] Both local
          dev                         b6bb1a5f0b1fc17a9ca2bdb6a932c84124e61b1f [origin/dev] Dev work
          gone-branch                 3604a50fd87878922336ff894001de092dba8f3b [origin/gone-branch: gone] Gone commit
        * main                        8ec7ceea6c4e13464c9cee83fccda637a66a1023 [origin/main] Initial
          release/1.0                 c0b1ba016f58887a5fb4cec08a5f85459403dbd9 Release commit
          remotes/origin/HEAD         -> origin/main
          remotes/origin/ahead-branch d41f7d81865515a5baf18e8e002d43e78d8e2118 Ahead one
          remotes/origin/both-branch  1e63fce66dccd969de1ced70a86c374028cfcb3d Both remote
          remotes/origin/dev          b6bb1a5f0b1fc17a9ca2bdb6a932c84124e61b1f Dev work
          remotes/origin/main         8ec7ceea6c4e13464c9cee83fccda637a66a1023 Initial
        """;

    // Detached HEAD, here after 'git checkout v1.0'. Git also writes '(HEAD detached from <ref>)'.
    const string DetachedOutput = """
        * (HEAD detached at v1.0) 1cc81b2b82bfc31a090f6f13df8468ad042cfe9a Topic change
          main                    633fda8d3ca1082206e0264d757c9f29818310c1 Main change
          topic                   1cc81b2b82bfc31a090f6f13df8468ad042cfe9a Topic change
        """;

    // While a rebase is stopped on a conflict git names the current pseudo branch
    // '(no branch, rebasing <branch>)'. A bisect gives '(no branch, bisect started on <ref>)'.
    const string RebasingOutput = """
        * (no branch, rebasing topic) 633fda8d3ca1082206e0264d757c9f29818310c1 Main change
          main                        633fda8d3ca1082206e0264d757c9f29818310c1 Main change
          topic                       1cc81b2b82bfc31a090f6f13df8468ad042cfe9a Topic change
        """;

    static async Task<IReadOnlyList<gmd.Git.Branch>> GetBranchesAsync(string output)
    {
        var service = new BranchService(new FakeCmd(output));
        var result = await service.GetBranchesAsync("/wd");
        Assert.IsTrue(Try(out var branches, out var e, result), $"GetBranchesAsync failed: {e}");
        return branches;
    }

    static async Task<gmd.Git.Branch> GetBranchAsync(string output, string name)
    {
        var branches = await GetBranchesAsync(output);
        var branch = branches.FirstOrDefault(b => b.Name == name);
        Assert.IsNotNull(branch, $"No branch '{name}' in [{string.Join(", ", branches.Select(b => b.Name))}]");
        return branch;
    }

    // The 'remotes/' prefix is trimmed and the pointer line is not a branch, so 12 lines give
    // 11 branches
    [TestMethod]
    public async Task TestParseAllBranchNames()
    {
        var branches = await GetBranchesAsync(BranchesOutput);

        CollectionAssert.AreEqual(
            new[]
            {
                "ahead-branch",
                "behind-branch",
                "both-branch",
                "dev",
                "gone-branch",
                "main",
                "release/1.0",
                "origin/ahead-branch",
                "origin/both-branch",
                "origin/dev",
                "origin/main",
            },
            branches.Select(b => b.Name).ToArray()
        );
    }

    // 'remotes/origin/HEAD -> origin/main' is a symbolic ref, not a branch. It is recognized by
    // its tip being '->' rather than a commit id.
    [TestMethod]
    public async Task TestParseSkipsRemoteHeadPointerLine()
    {
        var branches = await GetBranchesAsync(BranchesOutput);

        Assert.IsFalse(branches.Any(b => b.Name == "origin/HEAD"), "The pointer line is not a branch");
        Assert.IsFalse(branches.Any(b => b.TipID == "->"), "No branch has '->' as tip id");
    }

    [TestMethod]
    public async Task TestParseCurrentBranch()
    {
        var branches = await GetBranchesAsync(BranchesOutput);

        var current = branches.Where(b => b.IsCurrent).ToList();
        Assert.AreEqual(1, current.Count);
        Assert.AreEqual("main", current[0].Name);
        Assert.AreEqual("8ec7ceea6c4e13464c9cee83fccda637a66a1023", current[0].TipID);
    }

    [TestMethod]
    public async Task TestParseLocalBranchWithUpstream()
    {
        var branch = await GetBranchAsync(BranchesOutput, "dev");

        Assert.IsFalse(branch.IsRemote);
        Assert.AreEqual("origin/dev", branch.RemoteName);
        Assert.AreEqual("b6bb1a5f0b1fc17a9ca2bdb6a932c84124e61b1f", branch.TipID);
        Assert.AreEqual(0, branch.AheadCount);
        Assert.AreEqual(0, branch.BehindCount);
    }

    [TestMethod]
    public async Task TestParseRemoteBranch()
    {
        var branch = await GetBranchAsync(BranchesOutput, "origin/dev");

        Assert.IsTrue(branch.IsRemote);
        Assert.AreEqual("", branch.RemoteName, "A remote branch has no upstream of its own");
        Assert.IsFalse(branch.IsCurrent);
    }

    // A branch with no upstream has no '[…]' part at all
    [TestMethod]
    public async Task TestParseBranchWithoutUpstream()
    {
        var branch = await GetBranchAsync(BranchesOutput, "release/1.0");

        Assert.AreEqual("", branch.RemoteName);
        Assert.AreEqual("c0b1ba016f58887a5fb4cec08a5f85459403dbd9", branch.TipID);
    }

    [TestMethod]
    public async Task TestParseAheadCount()
    {
        var branch = await GetBranchAsync(BranchesOutput, "ahead-branch");

        Assert.AreEqual("origin/ahead-branch", branch.RemoteName);
        Assert.AreEqual(1, branch.AheadCount);
        Assert.AreEqual(0, branch.BehindCount);
    }

    [TestMethod]
    public async Task TestParseBehindCount()
    {
        var branch = await GetBranchAsync(BranchesOutput, "behind-branch");

        Assert.AreEqual("origin/behind-branch", branch.RemoteName);
        Assert.AreEqual(0, branch.AheadCount);
        Assert.AreEqual(1, branch.BehindCount);
    }

    [TestMethod]
    public async Task TestParseAheadAndBehindCounts()
    {
        var branch = await GetBranchAsync(BranchesOutput, "both-branch");

        Assert.AreEqual("origin/both-branch", branch.RemoteName);
        Assert.AreEqual(1, branch.AheadCount);
        Assert.AreEqual(1, branch.BehindCount);
    }

    // A 'gone' upstream keeps its remote name here. Augmenter.AddAugBranches is what clears it,
    // by looking for a matching remote branch in the list rather than trusting this name.
    [TestMethod]
    public async Task TestParseGoneUpstreamKeepsRemoteName()
    {
        var branches = await GetBranchesAsync(BranchesOutput);
        var branch = branches.First(b => b.Name == "gone-branch");

        Assert.AreEqual("origin/gone-branch", branch.RemoteName);
        Assert.IsFalse(branches.Any(b => b.Name == "origin/gone-branch"), "The remote branch is gone");
        Assert.AreEqual(0, branch.AheadCount);
        Assert.AreEqual(0, branch.BehindCount);
    }

    // Counts larger than one digit, and a two digit ahead with a two digit behind
    [TestMethod]
    public async Task TestParseMultiDigitAheadBehindCounts()
    {
        var output = "  b 1111111111111111111111111111111111111111 [origin/b: ahead 12, behind 345] Subject";

        var branch = await GetBranchAsync(output, "b");

        Assert.AreEqual(12, branch.AheadCount);
        Assert.AreEqual(345, branch.BehindCount);
    }

    // A detached HEAD is reported under the fixed name 'DETACHED', the ref it was detached at is
    // not kept
    [TestMethod]
    public async Task TestParseDetachedHead()
    {
        var branches = await GetBranchesAsync(DetachedOutput);

        var detached = branches.First();
        Assert.AreEqual("DETACHED", detached.Name);
        Assert.IsTrue(detached.IsDetached);
        Assert.IsTrue(detached.IsCurrent);
        Assert.AreEqual("1cc81b2b82bfc31a090f6f13df8468ad042cfe9a", detached.TipID);
        Assert.AreEqual(3, branches.Count, "The other two branches are still parsed");
    }

    // Git writes '(no branch, rebasing <branch>)' while a rebase is stopped on a conflict, which
    // gmd itself can cause via RebaseBranchAsync. It is a detached HEAD like any other, so it must
    // parse as one — it used to become a branch named '(no' with the tip id 'branch,', which no
    // commit has, so the whole current branch was lost until the rebase finished.
    [TestMethod]
    public async Task TestParseNoBranchWhileRebasing()
    {
        var branches = await GetBranchesAsync(RebasingOutput);

        var detached = branches.First();
        Assert.AreEqual("DETACHED", detached.Name);
        Assert.IsTrue(detached.IsDetached);
        Assert.IsTrue(detached.IsCurrent);
        Assert.AreEqual("633fda8d3ca1082206e0264d757c9f29818310c1", detached.TipID);
    }

    // The other '(no branch, …)' forms git writes, plus the bare '(no branch)'
    [TestMethod]
    [DataRow("(HEAD detached at v1.0)")]
    [DataRow("(HEAD detached from 1cc81b2)")]
    [DataRow("(no branch, rebasing topic)")]
    [DataRow("(no branch, rebasing detached HEAD 1cc81b2)")]
    [DataRow("(no branch, bisect started on topic)")]
    [DataRow("(no branch)")]
    public async Task TestParseDetachedHeadForms(string pseudoName)
    {
        var output = $"* {pseudoName} 1111111111111111111111111111111111111111 Subject";

        var branches = await GetBranchesAsync(output);

        Assert.AreEqual(1, branches.Count);
        Assert.AreEqual("DETACHED", branches[0].Name);
        Assert.IsTrue(branches[0].IsDetached);
        Assert.AreEqual("1111111111111111111111111111111111111111", branches[0].TipID);
    }

    // Git ref names may contain '(' and ')', so a parenthesized name is only a detached HEAD when
    // it is one of the forms git writes
    [TestMethod]
    public async Task TestParseBranchNameWithParenthesis()
    {
        var output = "  (work) 1111111111111111111111111111111111111111 Subject";

        var branch = await GetBranchAsync(output, "(work)");

        Assert.IsFalse(branch.IsDetached);
        Assert.AreEqual("1111111111111111111111111111111111111111", branch.TipID);
    }

    [TestMethod]
    public async Task TestParseEmptyOutputIsNoBranches()
    {
        var branches = await GetBranchesAsync("");

        Assert.AreEqual(0, branches.Count);
    }

    [TestMethod]
    public async Task TestGitCommandFailureIsPropagated()
    {
        var service = new BranchService(new FakeCmd((_, _, _) => FakeCmd.Fail("fatal: not a git repository")));

        var result = await service.GetBranchesAsync("/wd");

        Assert.IsFalse(Try(out var _, out var _, result), "Expected the git failure to propagate");
    }

    [TestMethod]
    public async Task TestGetBranchesPassesArgsAndWorkingDirectoryToGit()
    {
        var cmd = new FakeCmd(BranchesOutput);
        var service = new BranchService(cmd);

        await service.GetBranchesAsync("/some/wd");

        Assert.AreEqual(1, cmd.Calls.Count);
        Assert.AreEqual("git", cmd.Calls[0].Path);
        Assert.AreEqual("/some/wd", cmd.Calls[0].WorkingDirectory);
        Assert.AreEqual("branch -vv --no-color --no-abbrev --all", cmd.Calls[0].Args);
    }

    // The remote name of a branch to check out is trimmed, since a remote branch cannot be
    // checked out by its 'origin/' name
    [TestMethod]
    public async Task TestCheckoutTrimsRemotePrefix()
    {
        var cmd = new FakeCmd("");
        var service = new BranchService(cmd);

        await service.CheckoutAsync("origin/dev", "/wd");

        Assert.AreEqual("checkout dev", cmd.Calls[0].Args);
    }

    // A rename is '-m' and not '-M', so that git refuses rather than overwrites if the new name is
    // already taken. The old name is trimmed the same way a checkout is, since only a local branch
    // can be renamed.
    [TestMethod]
    public async Task TestRenameBranch()
    {
        var cmd = new FakeCmd("");
        var service = new BranchService(cmd);

        await service.RenameBranchAsync("origin/dev", "dev2", "/wd");

        Assert.AreEqual("branch -m dev dev2", cmd.Calls[0].Args);
        Assert.AreEqual("/wd", cmd.Calls[0].WorkingDirectory);
    }

    // Merge, rebase, rebase onto and cherry pick all turn a conflict into the same error, since
    // git reports conflicts as a failed command with 'CONFLICT' in the output
    [TestMethod]
    public async Task TestConflictIsReportedAsAnError()
    {
        var conflict = new FakeCmd(
            (_, _, _) => new CmdResult("git", 1, "CONFLICT (content): Merge conflict in a.txt", "error")
        );
        var service = new BranchService(conflict);

        foreach (
            var result in new[]
            {
                await service.MergeBranchAsync("dev", "/wd"),
                await service.RebaseBranchAsync("dev", "/wd"),
                await service.RebaseOntoAsync("main", "dev", "/wd"),
                await service.CherryPickAsync("1111111", "/wd"),
            }
        )
        {
            Assert.IsFalse(Try(out var e, result));
            StringAssert.Contains(e.ErrorMessage, "Merge Conflicts!");
        }
    }
}
