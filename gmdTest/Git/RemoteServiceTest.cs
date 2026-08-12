using gmd.Git.Private;
using gmdTest.Utils;

namespace gmdTest.Git;

// RemoteService only builds git command lines, so what is worth pinning is the arguments — in
// particular where the 'origin/' prefix is trimmed, since a branch has to be named without it on
// the remote side.
[TestClass]
public class RemoteServiceTest
{
    static RemoteService NewService(ICmd cmd) => new RemoteService(cmd, new TagService(cmd));

    static async Task<string> ArgsOf(Func<RemoteService, Task<R>> run)
    {
        var cmd = new FakeCmd("");
        await run(NewService(cmd));
        Assert.AreEqual(1, cmd.Calls.Count);
        Assert.AreEqual("git", cmd.Calls[0].Path);
        return cmd.Calls[0].Args;
    }

    // Deliberately no --prune-tags: it deletes local tags that were never pushed. The refspec is
    // what replaces it — it mirrors the remote's tags into gmd's own tracking namespace, which
    // --prune prunes, so TagService can tell a deleted tag from an unpushed one afterwards.
    [TestMethod]
    public async Task TestFetchPrunesBranchesAndMirrorsRemoteTags()
    {
        var cmd = new FakeCmd("");

        await NewService(cmd).FetchAsync("/wd");

        Assert.AreEqual(
            "fetch --force --prune --tags origin +refs/tags/*:refs/gmdtags/origin/*",
            cmd.Calls.Single(c => c.Args.StartsWith("fetch")).Args
        );
    }

    // The tracked remote tags have to be read before the fetch, since the fetch is what updates
    // them: read afterwards they would already be the new state and nothing would ever be pruned.
    [TestMethod]
    public async Task TestFetchReadsTheTrackedRemoteTagsBeforeFetching()
    {
        var cmd = new FakeCmd("");

        await NewService(cmd).FetchAsync("/wd");

        StringAssert.StartsWith(cmd.Calls[0].Args, "for-each-ref");
        StringAssert.Contains(cmd.Calls[0].Args, "refs/gmdtags/origin/");
        StringAssert.StartsWith(cmd.Calls[1].Args, "fetch");
    }

    // A failed fetch says nothing about what the remote has, so nothing may be pruned from it
    [TestMethod]
    public async Task TestFailedFetchPrunesNoTags()
    {
        var cmd = new FakeCmd(
            (_, args, _) =>
                args.StartsWith("fetch")
                    ? FakeCmd.Fail("fatal: could not read from remote")
                    : FakeCmd.Ok("134e1960d41fc44fb5ffffde38c2273f5e9910fc v1.0")
        );

        var result = await NewService(cmd).FetchAsync("/wd");

        Assert.IsFalse(Try(out var _, result), "Expected the git failure to propagate");
        Assert.IsFalse(cmd.Calls.Any(c => c.Args.StartsWith("tag -d")), "Expected no tag to be deleted");
    }

    // The remote name is given as a local ref pair, so the 'origin/' prefix has to go
    [TestMethod]
    public async Task TestPushBranchTrimsRemotePrefix()
    {
        Assert.AreEqual(
            "push --porcelain origin --set-upstream refs/heads/dev:refs/heads/dev",
            await ArgsOf(s => s.PushBranchAsync("origin/dev", "/wd"))
        );
    }

    [TestMethod]
    public async Task TestPushBranchWithoutRemotePrefix()
    {
        Assert.AreEqual(
            "push --porcelain origin --set-upstream refs/heads/dev:refs/heads/dev",
            await ArgsOf(s => s.PushBranchAsync("dev", "/wd"))
        );
    }

    // Only the 'origin/' prefix is trimmed, a branch merely named like it is left alone
    [TestMethod]
    public async Task TestPushBranchDoesNotTrimSimilarName()
    {
        Assert.AreEqual(
            "push --porcelain origin --set-upstream refs/heads/originals:refs/heads/originals",
            await ArgsOf(s => s.PushBranchAsync("originals", "/wd"))
        );
    }

    [TestMethod]
    public async Task TestPushCurrentBranch()
    {
        Assert.AreEqual("push", await ArgsOf(s => s.PushCurrentBranchAsync(false, "/wd")));
    }

    // A forced push uses --force-with-lease, so it still fails if the remote moved unexpectedly
    [TestMethod]
    public async Task TestPushCurrentBranchForced()
    {
        Assert.AreEqual("push --force-with-lease", await ArgsOf(s => s.PushCurrentBranchAsync(true, "/wd")));
    }

    [TestMethod]
    public async Task TestPullCurrentBranch()
    {
        Assert.AreEqual("pull", await ArgsOf(s => s.PullCurrentBranchAsync("/wd")));
    }

    // Pulling a branch that is not checked out is a fetch into the local ref
    [TestMethod]
    public async Task TestPullBranchTrimsRemotePrefix()
    {
        Assert.AreEqual("fetch origin dev:dev", await ArgsOf(s => s.PullBranchAsync("origin/dev", "/wd")));
    }

    [TestMethod]
    public async Task TestDeleteRemoteBranchTrimsRemotePrefix()
    {
        Assert.AreEqual(
            "push --porcelain origin --delete dev",
            await ArgsOf(s => s.DeleteRemoteBranchAsync("origin/dev", "/wd"))
        );
    }

    [TestMethod]
    public async Task TestPushRefForce()
    {
        Assert.AreEqual(
            "push --porcelain origin --set-upstream --force dev:dev",
            await ArgsOf(s => s.PushRefForceAsync("origin/dev", "/wd"))
        );
    }

    [TestMethod]
    public async Task TestPullRef()
    {
        Assert.AreEqual("fetch origin dev:dev", await ArgsOf(s => s.PullRefAsync("origin/dev", "/wd")));
    }

    // The target path is quoted, since it comes from the user and may contain spaces
    [TestMethod]
    public async Task TestCloneQuotesThePath()
    {
        Assert.AreEqual(
            "clone https://x/y.git \"/my path/y\"",
            await ArgsOf(s => s.CloneAsync("https://x/y.git", "/my path/y", "/wd"))
        );
    }

    [TestMethod]
    public async Task TestPushAndDeleteTag()
    {
        Assert.AreEqual("push --porcelain origin v1.0", await ArgsOf(s => s.PushTagAsync("v1.0", "/wd")));
        Assert.AreEqual(
            "push --porcelain origin --delete v1.0",
            await ArgsOf(s => s.DeleteRemoteTagAsync("v1.0", "/wd"))
        );
    }

    [TestMethod]
    public async Task TestGitCommandFailureIsPropagated()
    {
        var service = NewService(new FakeCmd((_, _, _) => FakeCmd.Fail("fatal: could not read from remote")));

        var result = await service.PushCurrentBranchAsync(false, "/wd");

        Assert.IsFalse(Try(out var _, result), "Expected the git failure to propagate");
    }
}
