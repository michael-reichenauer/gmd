using gmd.Cui.RepoView;
using gmd.Server;
using gmdTest.Fixtures;

namespace gmdTest.Cui.RepoView;

// What is said of a remote branch a force push rewrote
[TestClass]
public class ForcePushesTest
{
    static RemoteRewrite Rewrite(int own = 1, int dropped = 0, bool isByYou = false, string tip = "b2") =>
        new RemoteRewrite(
            "dev",
            "origin/dev",
            RepoBuilder.Sha("a3"),
            RepoBuilder.Sha("a3"),
            RepoBuilder.Sha(tip),
            own,
            2,
            Enumerable.Range(0, dropped).Select(i => RepoBuilder.Sha($"d{i}")).ToList(),
            isByYou,
            true
        );

    [TestMethod]
    public void TestWhatIsSaidWhenItIsFound()
    {
        Assert.AreEqual(
            "'origin/dev' was rewritten by a force push: Pull moves your 1 commit onto it",
            ForcePushes.Found(Rewrite())
        );
        Assert.AreEqual(
            "'origin/dev' was rewritten by a force push, dropping 2 commits: Pull takes the new version",
            ForcePushes.Found(Rewrite(own: 0, dropped: 2))
        );
    }

    // Each rewrite is told once, and one pushed from here not at all
    [TestMethod]
    public void TestEachRewriteIsToldOnce()
    {
        var notes = new ForcePushNotes();
        Repo RepoWith(params RemoteRewrite[] rewrites) =>
            Repo.Empty with
            {
                RemoteRewrites = rewrites.ToDictionary(r => r.BranchName),
            };

        Assert.IsNotNull(notes.Untold(RepoWith(Rewrite())));
        Assert.IsNull(notes.Untold(RepoWith(Rewrite())));
        Assert.IsNotNull(notes.Untold(RepoWith(Rewrite(tip: "b3"))), "Rewritten again");
        Assert.IsNull(notes.Untold(RepoWith(Rewrite(isByYou: true, tip: "b4"))));
    }
}
