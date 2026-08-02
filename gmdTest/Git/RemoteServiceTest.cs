using gmd.Git.Private;
using gmdTest.Utils;

namespace gmdTest.Git;

// RemoteService only builds git command lines, so what is worth pinning is the arguments — in
// particular where the 'origin/' prefix is trimmed, since a branch has to be named without it on
// the remote side.
[TestClass]
public class RemoteServiceTest
{
    static async Task<string> ArgsOf(Func<RemoteService, Task<R>> run)
    {
        var cmd = new FakeCmd("");
        await run(new RemoteService(cmd));
        Assert.AreEqual(1, cmd.Calls.Count);
        Assert.AreEqual("git", cmd.Calls[0].Path);
        return cmd.Calls[0].Args;
    }

    [TestMethod]
    public async Task TestFetchPrunesBranchesAndTags()
    {
        Assert.AreEqual("fetch --force --prune --tags --prune-tags origin", await ArgsOf(s => s.FetchAsync("/wd")));
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
        var service = new RemoteService(new FakeCmd((_, _, _) => FakeCmd.Fail("fatal: could not read from remote")));

        var result = await service.FetchAsync("/wd");

        Assert.IsFalse(Try(out var _, result), "Expected the git failure to propagate");
    }
}
