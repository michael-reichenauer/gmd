using gmd.Cui.RepoView;
using gmd.Server;
using gmdTest.Fixtures;
using gmdTest.Server.Private.Augmented.Private;

namespace gmdTest.Cui.RepoView;

// What is said of a branch rewritten here after it was pushed, whose push is a force push
[TestClass]
public class RewrittenHereTest
{
    static LocalRewrite Rewrite(
        StepKind kind = StepKind.Squash,
        string name = "",
        int old = 3,
        string branch = "feature",
        string oldTip = "a3"
    ) =>
        new LocalRewrite(
            branch,
            $"origin/{branch}",
            RepoBuilder.Sha("s1"),
            RepoBuilder.Sha(oldTip),
            kind,
            name,
            1,
            Enumerable.Range(0, old).Select(i => RepoBuilder.Sha($"o{i}")).ToList()
        );

    static Repo RepoWith(params LocalRewrite[] rewrites) =>
        Repo.Empty with
        {
            LocalRewrites = rewrites.ToDictionary(r => r.BranchName),
        };

    // Named after what rewrote it, when that is known, as Undo names it
    [TestMethod]
    public void TestWhatIsSaidWhenItIsFound()
    {
        Assert.AreEqual(
            "'feature' was rewritten here by the squash: p pushes it with force, over origin's old version",
            RewrittenHere.Found(Rewrite())
        );
        Assert.AreEqual(
            "'feature' was rewritten here by the amend: p pushes it with force, over origin's old version",
            RewrittenHere.Found(Rewrite(StepKind.Amend, "Fix"))
        );
        Assert.AreEqual(
            "'feature' was rewritten here: p pushes it with force, over origin's old version",
            RewrittenHere.Found(Rewrite(StepKind.Other))
        );
        Assert.AreEqual(
            "'dev', 'feature' were rewritten here: p pushes each with force, over origin's old version",
            RewrittenHere.Found([Rewrite(branch: "dev"), Rewrite()])
        );
    }

    // The question names the commits the force push replaces on origin, newest first, with their
    // subjects, and where they can be found afterwards
    [TestMethod]
    public async Task TestThePushQuestion()
    {
        var repo = await LocalRewritesTest.Squashed(new RepoBuilder()).ViewRepoAsync();

        Assert.AreEqual(
            """
            'feature' was rewritten here by the rebase,
            and origin still has the 3 commits it replaced:

              a3000000 Validate
              a2000000 Fix typo
              a1000000 Add login

            Force Push replaces them on origin with the 1 commit here,
            for everyone who has the branch. Recover Lost Commits
            finds them afterwards.
            """,
            RewrittenHere.PushQuestion(repo, repo.LocalRewrites["feature"])
        );
    }

    [TestMethod]
    public void TestThePushQuestionCountsTheOldCommitsItDoesNotList()
    {
        var question = RewrittenHere.PushQuestion(Repo.Empty, Rewrite(old: 7));

        StringAssert.Contains(question, "  o4000000 \n  and 2 more\n");
        StringAssert.Contains(RewrittenHere.PushQuestion(Repo.Empty, Rewrite(old: 1)), "replaces it on origin");
        StringAssert.Contains(
            RewrittenHere.PushQuestion(Repo.Empty, Rewrite(StepKind.Amend, "Fix")),
            "'feature' was rewritten here by the amend of 'Fix',\n",
            "Named in the question, which has the room the status line has not"
        );
    }

    // Each is told once, when a refresh first finds it, however it was made: gmd's squash says nothing
    // itself, so this is what says it right after. A rewrite of a branch already told is told again
    // once it is of another old version, i.e. it was pushed and rewritten again.
    [TestMethod]
    public void TestEachRewriteIsToldOnce()
    {
        var notes = new ForcePushNotes();

        Assert.AreEqual(RewrittenHere.Found(Rewrite()), notes.Untold(RepoWith(Rewrite())));
        Assert.IsNull(notes.Untold(RepoWith(Rewrite())));
        Assert.IsNull(notes.Untold(RepoWith(Rewrite(StepKind.Rebase))), "The same old version, rewritten further");
        Assert.IsNotNull(notes.Untold(RepoWith(Rewrite(oldTip: "b3"))), "Pushed and rewritten again");
    }

    // A force push on origin and a rewrite here found by one refresh are both told
    [TestMethod]
    public void TestARewriteHereIsToldWithOneOnOrigin()
    {
        var remote = new RemoteRewrite("dev", "origin/dev", "", "", "", "b2", 1, 2, 3, [], false, true);
        var repo = RepoWith(Rewrite()) with
        {
            RemoteRewrites = new Dictionary<string, RemoteRewrite> { ["dev"] = remote },
        };

        Assert.AreEqual(
            $"{ForcePushes.Found(remote)}; {RewrittenHere.Found(Rewrite())}",
            new ForcePushNotes().Untold(repo)
        );
    }

    [TestMethod]
    public void TestWhatIsSaidOncePushed()
    {
        Assert.AreEqual(
            "Pushed 'feature' with force, replacing the old version on origin",
            RewrittenHere.Pushed(Rewrite())
        );
    }

    // Push All pushes none of them, each being a force push, and says so
    [TestMethod]
    public void TestWhatPushAllSaysOfTheOnesItLeaves()
    {
        Assert.AreEqual(
            "'feature' was rewritten here: push it on its own (p), which asks to force it",
            RewrittenHere.LeftOut([Rewrite()])
        );
        Assert.AreEqual(
            "'dev', 'feature' were rewritten here: push each on its own (p), which asks to force it",
            RewrittenHere.LeftOut([Rewrite(branch: "dev"), Rewrite()])
        );
    }
}
