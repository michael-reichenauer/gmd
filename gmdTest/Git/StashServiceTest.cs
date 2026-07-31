using gmd.Git.Private;
using gmdTest.Utils;

namespace gmdTest.Git;

[TestClass]
public class StashServiceTest
{
    // Output of: git stash list -z --pretty="%H|%ai|%ci|%an|%P|%gd:%B", records NUL separated.
    // A stash commit has two parents, or three when untracked files were stashed too (-u). The
    // subject is '<stash ref>:<git's own message>', where git's message is either
    // 'WIP on <branch>: <sha> <subject>' or 'On <branch>: <the message the user gave>'.
    const string StashListOutput =
        "bf84c7d60c026c11e8c2b923b047a87eaefdcab5|2026-07-31 04:00:14 +0200|2026-07-31 04:00:14 +0200|Test|"
        + "134e1960d41fc44fb5ffffde38c2273f5e9910fc eba19185b85912777802e8a8575c41ed25e221d8 "
        + "e90a3aa63ebf08e3cbe4aa3ed9582eb4db64a6df|stash@{0}:WIP on main: 134e196 Second commit\x00"
        + "eb5ae0235ccd8103a6bbe1e0b149e6047ac6daa9|2026-07-31 04:00:14 +0200|2026-07-31 04:00:14 +0200|Test|"
        + "134e1960d41fc44fb5ffffde38c2273f5e9910fc eba19185b85912777802e8a8575c41ed25e221d8"
        + "|stash@{1}:On main: First stash\x00";

    static StashService NewService(ICmd cmd) => new StashService(cmd, new LogService(cmd), new DiffService(cmd));

    static async Task<IReadOnlyList<gmd.Git.Stash>> ListAsync(string output)
    {
        var result = await NewService(new FakeCmd(output)).ListAsync("/wd");
        Assert.IsTrue(Try(out var stashes, out var e, result), $"ListAsync failed: {e}");
        return stashes;
    }

    [TestMethod]
    public async Task TestParseStashList()
    {
        var stashes = await ListAsync(StashListOutput);

        Assert.AreEqual(2, stashes.Count);
        CollectionAssert.AreEqual(new[] { "stash@{0}", "stash@{1}" }, stashes.Select(s => s.Name).ToArray());
        Assert.AreEqual("bf84c7d60c026c11e8c2b923b047a87eaefdcab5", stashes[0].Id);
    }

    // The branch is the last word of git's 'WIP on <branch>' / 'On <branch>' text
    [TestMethod]
    public async Task TestParseStashBranch()
    {
        var stashes = await ListAsync(StashListOutput);

        Assert.AreEqual("main", stashes[0].Branch);
        Assert.AreEqual("main", stashes[1].Branch);
    }

    // The first parent is the commit the stash was made on, the second the stashed index
    [TestMethod]
    public async Task TestParseStashParentAndIndexIds()
    {
        var stashes = await ListAsync(StashListOutput);

        Assert.AreEqual("134e1960d41fc44fb5ffffde38c2273f5e9910fc", stashes[0].ParentId);
        Assert.AreEqual("eba19185b85912777802e8a8575c41ed25e221d8", stashes[0].IndexId);
    }

    [TestMethod]
    public async Task TestParseStashMessage()
    {
        var stashes = await ListAsync(StashListOutput);

        Assert.AreEqual("134e196 Second commit", stashes[0].Message, "An unnamed stash keeps git's own text");
        Assert.AreEqual("First stash", stashes[1].Message);
    }

    // The subject is split on ':' and only the third part is used, so a stash message containing a
    // colon is cut short. A branch name cannot contain one, so only the message is affected.
    [TestMethod]
    public async Task TestParseStashMessageWithColonIsTruncated()
    {
        var output =
            "1111111111111111111111111111111111111111|2026-07-31 04:00:14 +0200|2026-07-31 04:00:14 +0200|Test|"
            + "2222222222222222222222222222222222222222 3333333333333333333333333333333333333333"
            + "|stash@{0}:On main: fix: the thing\x00";

        var stashes = await ListAsync(output);

        Assert.AreEqual("fix", stashes[0].Message);
    }

    [TestMethod]
    public async Task TestNoStashesIsAnEmptyList()
    {
        var stashes = await ListAsync("");

        Assert.AreEqual(0, stashes.Count);
    }

    [TestMethod]
    public async Task TestGitCommandFailureIsPropagated()
    {
        var service = NewService(new FakeCmd((_, _, _) => FakeCmd.Fail("fatal: not a git repository")));

        var result = await service.ListAsync("/wd");

        Assert.IsFalse(Try(out var _, out var _, result), "Expected the git failure to propagate");
    }

    // A named stash uses 'save', an unnamed one does not. Both include untracked files (-u).
    [TestMethod]
    public async Task TestStashWithAndWithoutMessage()
    {
        var cmd = new FakeCmd("");
        var service = NewService(cmd);

        await service.StashAsync("My message", "/wd");
        await service.StashAsync("", "/wd");

        Assert.AreEqual("stash save \"My message\" -u", cmd.Calls[0].Args);
        Assert.AreEqual("stash -u", cmd.Calls[1].Args);
    }

    [TestMethod]
    public async Task TestPopAndDrop()
    {
        var cmd = new FakeCmd("");
        var service = NewService(cmd);

        await service.PopAsync("stash@{0}", "/wd");
        await service.DropAsync("stash@{1}", "/wd");

        Assert.AreEqual("stash pop stash@{0}", cmd.Calls[0].Args);
        Assert.AreEqual("stash drop stash@{1}", cmd.Calls[1].Args);
    }

    [TestMethod]
    public async Task TestGetDiffDelegatesToDiffService()
    {
        var cmd = new FakeCmd("");
        var service = NewService(cmd);

        await service.GetDiffAsync("stash@{0}", "/wd");

        StringAssert.StartsWith(cmd.Calls[0].Args, "stash show -u ");
        StringAssert.EndsWith(cmd.Calls[0].Args, " stash@{0}");
    }
}
