using gmd.Git;
using gmd.Git.Private;
using gmdTest.Utils;

namespace gmdTest.Git;

[TestClass]
public class ReflogServiceTest
{
    const string A = "a000000000000000000000000000000000000000";
    const string B = "b000000000000000000000000000000000000000";

    // Output of: git reflog show --all --format=%H%x00%gD%x00%gs
    //
    // The entries of each ref come together, the latest first, and every other worktree's HEAD has a
    // reflog of its own. Assembled rather than pasted, for the NULs.
    static readonly string Output = string.Join(
        "\n",
        [
            $"{B}\0refs/heads/feature@{{0}}\0commit: Feature work",
            $"{A}\0refs/heads/feature@{{1}}\0branch: Created from HEAD",
            $"{A}\0refs/heads/main@{{0}}\0commit (initial): Initial",
            $"{B}\0HEAD@{{0}}\0commit: Feature work",
            $"{A}\0HEAD@{{1}}\0checkout: moving from main to feature",
            $"{A}\0HEAD@{{2}}\0commit (initial): Initial",
            $"{A}\0worktrees/topic/HEAD@{{0}}\0",
        ]
    );

    [TestMethod]
    public async Task TestReflogEntriesAreParsed()
    {
        var cmd = new FakeCmd(Output);
        var entries = AssertOk(await new ReflogService(cmd).GetReflogAsync("/wd"));

        Assert.AreEqual("reflog show --all --format=%H%x00%gD%x00%gs", cmd.Calls[0].Args);
        CollectionAssert.AreEqual(
            new[]
            {
                new ReflogEntry(B, "refs/heads/feature", 0, "commit: Feature work"),
                new ReflogEntry(A, "refs/heads/feature", 1, "branch: Created from HEAD"),
                new ReflogEntry(A, "refs/heads/main", 0, "commit (initial): Initial"),
                new ReflogEntry(B, "HEAD", 0, "commit: Feature work"),
                new ReflogEntry(A, "HEAD", 1, "checkout: moving from main to feature"),
                new ReflogEntry(A, "HEAD", 2, "commit (initial): Initial"),
                new ReflogEntry(A, "worktrees/topic/HEAD", 0, ""),
            },
            entries.ToArray()
        );
    }

    // A ref name can hold '@' ('feature@2') and a message can hold anything but a newline, so the
    // selector is split at its last '@{' and the fields at the NULs
    [TestMethod]
    public void TestNamesAndMessagesWithSpecialCharacters()
    {
        var entries = ReflogService
            .Parse($"{A}\0refs/heads/feature@2@{{3}}\0commit: Fix @{{0}} and a \"quote\"")
            .ToList();

        Assert.AreEqual(new ReflogEntry(A, "refs/heads/feature@2", 3, "commit: Fix @{0} and a \"quote\""), entries[0]);
    }

    [TestMethod]
    public void TestAnEmptyOrMalformedReflogGivesNoEntries()
    {
        Assert.AreEqual(0, ReflogService.Parse("").Count());
        Assert.AreEqual(0, ReflogService.Parse($"{A}\0refs/heads/main\0commit: no index").Count());
        Assert.AreEqual(0, ReflogService.Parse("not a reflog line").Count());
    }
}
