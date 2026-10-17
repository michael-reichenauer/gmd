using gmd.Cui.RepoView;
using gmd.Server;
using gmdTest.Fixtures;
using GitOp = gmd.Git.GitOperation;

namespace gmdTest.Cui.RepoView;

// Which commits Amend, Drop and Squash can rewrite: the current branch's, pushed or not, with no merge
// after them for the rebase to flatten and nothing else on them to be left on the old ones; and when
// some are on origin, not while origin has commits the branch has not, and asked first when the
// branch is shared or some of them are someone else's
[TestClass]
public class CommitRewriteTest
{
    static Commit C(Repo repo, string name) => repo.CommitById[RepoBuilder.Sha(name)];

    // main is on origin up to c2, and has c3 and c4 of its own
    static RepoBuilder Ahead() =>
        new RepoBuilder()
            .Commit("c4", "Fourth", "c3")
            .Commit("c3", "Third", "c2")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c4", isCurrent: true, remoteTipCommit: "c2", ahead: 2);

    // A commit on origin is rewritten too, origin's copy of the branch being no other branch on it, and
    // a force push then replaces it there; Uncommit is for commits not pushed only
    [TestMethod]
    public async Task TestAPushedCommitIsRewrittenToo()
    {
        var repo = await Ahead().ViewRepoAsync();

        Assert.IsTrue(CommitRewrite.IsNotPushed(repo, C(repo, "c3")));
        Assert.IsFalse(CommitRewrite.IsNotPushed(repo, C(repo, "c2")));
        Assert.AreEqual("", CommitRewrite.WhyNotAmend(repo, C(repo, "c3")));
        Assert.AreEqual("", CommitRewrite.WhyNotDrop(repo, C(repo, "c3")));
        Assert.AreEqual("", CommitRewrite.WhyNotAmend(repo, C(repo, "c2")));
        Assert.AreEqual("", CommitRewrite.WhyNotDrop(repo, C(repo, "c2")));
        Assert.AreEqual("", CommitRewrite.WhyNotSquash(repo, C(repo, "c3"), C(repo, "c2")));
        Assert.AreEqual(
            "Only a commit not yet pushed can be uncommitted",
            CommitRewrite.WhyNotUncommit(repo, C(repo, "c2"))
        );
        CollectionAssert.AreEqual(
            new[] { "c4", "c3", "c2" },
            CommitRewrite.RewrittenBy(repo, C(repo, "c2")).Select(c => c.Sid[..2]).ToArray()
        );
    }

    // The force push after the rewrite would drop what origin has and the branch has not, so that is
    // pulled first; unless the branch was rewritten here already, when that is its old version
    [TestMethod]
    public async Task TestNotWhileOriginHasCommitsTheBranchHasNot()
    {
        var repo = await new RepoBuilder()
            .Commit("c4", "Mine", "c2")
            .Commit("c3", "Theirs", "c2")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("feature", "c4", isCurrent: true, remoteTipCommit: "c3", ahead: 1, behind: 1)
            .ViewRepoAsync();
        var pullFirst =
            "'origin/feature' has commits 'feature' has not, which the force push would drop: pull them first";

        Assert.AreEqual("", CommitRewrite.WhyNotAmend(repo, C(repo, "c4")), "Not on origin");
        Assert.AreEqual("", CommitRewrite.WhyNotAmendLast(repo));
        Assert.AreEqual(pullFirst, CommitRewrite.WhyNotAmend(repo, C(repo, "c2")));
        Assert.AreEqual(pullFirst, CommitRewrite.WhyNotDrop(repo, C(repo, "c2")));
        Assert.AreEqual(pullFirst, CommitRewrite.WhyNotSquash(repo, C(repo, "c4"), C(repo, "c2")));

        var rewritten = repo with
        {
            LocalRewrites = new Dictionary<string, LocalRewrite>
            {
                ["feature"] = new("feature", "origin/feature", "", "", StepKind.Amend, "", 1, [RepoBuilder.Sha("c3")]),
            },
        };
        Assert.AreEqual("", CommitRewrite.WhyNotAmend(rewritten, C(rewritten, "c2")));
    }

    // The last commit is amended alone, by 'git commit --amend', pushed or not
    [TestMethod]
    public async Task TestTheLastCommitIsAmendedPushedOrNot()
    {
        Assert.AreEqual("", CommitRewrite.WhyNotAmendLast(await Ahead().ViewRepoAsync()));
        Assert.AreEqual("", CommitRewrite.WhyNotAmendLast(await Ahead().BranchWithRemote("dev", "c4").ViewRepoAsync()));
        var behind = await new RepoBuilder()
            .Commit("c3", "Theirs", "c2")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true, remoteTipCommit: "c3", behind: 1)
            .ViewRepoAsync();
        Assert.AreEqual(
            "'origin/main' has commits 'main' has not, which the force push would drop: pull them first",
            CommitRewrite.WhyNotAmendLast(behind)
        );
    }

    // Asked first when the branch is one others build on, or some of the commits on origin are someone
    // else's: not for commits of one's own on a branch of one's own, nor for commits not pushed
    [TestMethod]
    public async Task TestWhatIsAskedBeforeCommitsOnOriginAreRewritten()
    {
        static RepoBuilder Feature(string branch = "feature") =>
            new RepoBuilder()
                .Commit("f3", "Third", "f2")
                .Commit("f2", "Second", "f1")
                .Commit("f1", "First", "c1")
                .Commit("c1", "Initial")
                .BranchWithRemote("main", "c1")
                .BranchWithRemote(branch, "f3", isCurrent: true, remoteTipCommit: "f2", ahead: 1)
                .CurrentAuthor("Test Author");

        var own = await Feature().ViewRepoAsync();
        Assert.AreEqual("", CommitRewrite.PushedWarning(own, CommitRewrite.RewrittenBy(own, C(own, "f1"))));
        Assert.AreEqual("", CommitRewrite.PushedWarning(own, [C(own, "f3")]), "Not on origin");

        var others = await Feature().AuthoredBy("f2", "Anna Lind").AuthoredBy("f1", "Oskar Lind").ViewRepoAsync();
        Assert.AreEqual(
            """
            2 of the commits it rewrites are on origin.
            Both are by Anna Lind and Oskar Lind.

            A force push of the rewrite replaces them on origin, for everyone,
            and whoever has them has to take the new version.

            Rewrite all the same?
            """,
            CommitRewrite.PushedWarning(others, CommitRewrite.RewrittenBy(others, C(others, "f1")))
        );
        StringAssert.StartsWith(
            CommitRewrite.PushedWarning(others, CommitRewrite.RewrittenBy(others, C(others, "f2"))),
            "1 of the commits it rewrites is on origin.\nIt is by Anna Lind.\n"
        );

        var develop = await Feature("develop").ViewRepoAsync();
        Assert.AreEqual(
            """
            2 of the commits it rewrites are on origin.
            'develop' is a shared branch, which others build on.

            A force push of the rewrite replaces them on origin, for everyone,
            and whoever has them has to take the new version.

            Rewrite all the same?
            """,
            CommitRewrite.PushedWarning(develop, CommitRewrite.RewrittenBy(develop, C(develop, "f1")))
        );

        var main = await Ahead().ViewRepoAsync();
        StringAssert.StartsWith(
            CommitRewrite.PushedWarning(main, [C(main, "c2")]),
            "The commit it rewrites is on origin.\n'main' is the main branch, which others build on.\n"
        );
    }

    // A branch never pushed has no commit ahead of a remote branch, and all of its commits are not
    // pushed yet. Amend used to go by 'ahead' alone, so it refused the last commit of such a branch,
    // which Uncommit took back.
    [TestMethod]
    public async Task TestTheCommitsOfABranchNeverPushedAreNotPushed()
    {
        var repo = await new RepoBuilder()
            .Commit("f2", "Feature two", "f1")
            .Commit("f1", "Feature one", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c1")
            .LocalBranch("feature", "f2", isCurrent: true)
            .ViewRepoAsync();

        Assert.IsFalse(C(repo, "f2").IsAhead);
        Assert.IsTrue(CommitRewrite.IsNotPushed(repo, C(repo, "f2")));
        Assert.AreEqual("", CommitRewrite.WhyNotAmend(repo, C(repo, "f1")));
        Assert.AreEqual(
            "Only a commit on the current branch can be amended",
            CommitRewrite.WhyNotAmend(repo, C(repo, "c1")),
            "main's"
        );
    }

    [TestMethod]
    public async Task TestOnlyACommitOnTheCurrentBranchIsRewritten()
    {
        var repo = await new RepoBuilder()
            .Commit("f1", "Feature", "c1")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true, remoteTipCommit: "c1", ahead: 1)
            .LocalBranch("feature", "f1")
            .ViewRepoAsync("feature");

        Assert.AreEqual(
            "Only a commit on the current branch can be amended",
            CommitRewrite.WhyNotAmend(repo, C(repo, "f1"))
        );
    }

    // The rebase replays the commits after it as plain commits, which would flatten a merge among them
    [TestMethod]
    public async Task TestNotPastAMerge()
    {
        var repo = await new RepoBuilder()
            .Commit("f3", "Merge branch 'main' into feature", "f2", "c2")
            .Commit("f2", "Feature two", "f1")
            .Commit("c2", "Second", "c1")
            .Commit("f1", "Feature one", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2")
            .LocalBranch("feature", "f3", isCurrent: true)
            .ViewRepoAsync();

        Assert.AreEqual(
            "There is a merge after this commit, which the rebase would flatten",
            CommitRewrite.WhyNotAmend(repo, C(repo, "f1"))
        );
        Assert.AreEqual(
            "A merge cannot be dropped: the rebase would flatten it",
            CommitRewrite.WhyNotDrop(repo, C(repo, "f3"))
        );
    }

    // Another branch on the commit or one after it would stay on the old commits: one built on it,
    // or one with no commits of its own at it
    [TestMethod]
    public async Task TestNotWithAnotherBranchOnIt()
    {
        static RepoBuilder Feature() =>
            new RepoBuilder()
                .Commit("s1", "Stacked", "f1")
                .Commit("f2", "Feature two", "f1")
                .Commit("f1", "Feature one", "c1")
                .Commit("c1", "Initial")
                .BranchWithRemote("main", "c1")
                .LocalBranch("feature", "f2", isCurrent: true);

        var stacked = await Feature().LocalBranch("stacked", "s1").ViewRepoAsync();
        Assert.AreEqual(
            "'stacked' is on this commit or one after it, and would keep the old ones",
            CommitRewrite.WhyNotAmend(stacked, C(stacked, "f1"))
        );
        Assert.AreEqual("", CommitRewrite.WhyNotAmend(stacked, C(stacked, "f2")), "Nothing is on f2 but feature");

        var copy = await new RepoBuilder()
            .Commit("f2", "Feature two", "f1")
            .Commit("f1", "Feature one", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c1")
            .LocalBranch("feature", "f2", isCurrent: true)
            .LocalBranch("copy", "f2")
            .ViewRepoAsync();
        Assert.AreEqual(
            "'copy' is on this commit or one after it, and would keep the old ones",
            CommitRewrite.WhyNotDrop(copy, C(copy, "f1"))
        );
    }

    [TestMethod]
    public async Task TestNotWithATagOnIt()
    {
        var repo = await new RepoBuilder()
            .Commit("f2", "Feature two", "f1")
            .Commit("f1", "Feature one", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c1")
            .LocalBranch("feature", "f2", isCurrent: true)
            .Tag("v1", "f2")
            .ViewRepoAsync();

        Assert.AreEqual(
            "The tag 'v1' is on this commit or one after it, and would keep the old ones",
            CommitRewrite.WhyNotAmend(repo, C(repo, "f1"))
        );
    }

    // The first commit can be amended, which rebases from the start, but there is nothing to drop it
    // onto. Changes in the working tree are put aside for an amend, but a drop moves the files.
    [TestMethod]
    public async Task TestTheFirstCommitAndChanges()
    {
        var repo = await new RepoBuilder()
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .LocalBranch("main", "c2", isCurrent: true)
            .WithStatus(modified: 1)
            .ViewRepoAsync();

        Assert.AreEqual("", CommitRewrite.WhyNotAmend(repo, C(repo, "c1")));
        Assert.AreEqual("The first commit cannot be dropped", CommitRewrite.WhyNotDrop(repo, C(repo, "c1")));
        Assert.AreEqual(Why.Changes, CommitRewrite.WhyNotDrop(repo, C(repo, "c2")));
        Assert.AreEqual(
            "A commit is dropped: move to one first",
            CommitRewrite.WhyNotDrop(repo, repo.CommitById[Repo.UncommittedId])
        );
    }

    // Uncommit resets the branch to the commit before this one and keeps the changes, so it follows
    // the rule of Amend and Drop: only commits not pushed yet, on the current branch. It was offered
    // on pushed commits, and on the first commit, which has nothing before it, and threw there.
    [TestMethod]
    public async Task TestUncommitTakesCommitsNotPushedThatHaveOneBefore()
    {
        var ahead = await Ahead().ViewRepoAsync();
        Assert.AreEqual("", CommitRewrite.WhyNotUncommit(ahead, C(ahead, "c3")));
        Assert.AreEqual(
            "Only a commit not yet pushed can be uncommitted",
            CommitRewrite.WhyNotUncommit(ahead, C(ahead, "c2"))
        );

        var neverPushed = await new RepoBuilder()
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .LocalBranch("main", "c2", isCurrent: true)
            .ViewRepoAsync();
        Assert.AreEqual("", CommitRewrite.WhyNotUncommit(neverPushed, C(neverPushed, "c2")));
        Assert.AreEqual(
            "The first commit cannot be uncommitted",
            CommitRewrite.WhyNotUncommit(neverPushed, C(neverPushed, "c1"))
        );
    }

    [TestMethod]
    public async Task TestUncommitNeedsACleanTreeAndTheCurrentBranch()
    {
        var changed = await Ahead().WithStatus(modified: 1).ViewRepoAsync();
        Assert.AreEqual(Why.Changes, CommitRewrite.WhyNotUncommit(changed, C(changed, "c3")));
        Assert.AreEqual(
            "Commits are uncommitted: move to one first",
            CommitRewrite.WhyNotUncommit(changed, changed.CommitById[Repo.UncommittedId])
        );

        var other = await new RepoBuilder()
            .Commit("f1", "Feature", "c1")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true, remoteTipCommit: "c1", ahead: 1)
            .LocalBranch("feature", "f1")
            .ViewRepoAsync("feature");
        Assert.AreEqual(
            "Only commits on the current branch can be uncommitted",
            CommitRewrite.WhyNotUncommit(other, C(other, "f1"))
        );
    }

    // Squash takes selected commits of the current branch. The menu offered it for any rows, and the
    // command refused the rest in an error box.
    [TestMethod]
    public async Task TestSquashTakesCommitsOfTheCurrentBranch()
    {
        var ahead = await Ahead().ViewRepoAsync();
        Assert.AreEqual("", CommitRewrite.WhyNotSquash(ahead, C(ahead, "c4"), C(ahead, "c3")));
        Assert.AreEqual(
            "The first commit cannot be squashed",
            CommitRewrite.WhyNotSquash(ahead, C(ahead, "c2"), C(ahead, "c1"))
        );

        var changed = await Ahead().WithStatus(modified: 1).ViewRepoAsync();
        Assert.AreEqual(Why.Changes, CommitRewrite.WhyNotSquash(changed, C(changed, "c4"), C(changed, "c3")));
    }

    [TestMethod]
    public async Task TestSquashRefusesRowsOfTwoBranchesOrAnother()
    {
        var repo = await new RepoBuilder()
            .Commit("f2", "Feature two", "f1")
            .Commit("c2", "Second", "c1")
            .Commit("f1", "Feature one", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true, remoteTipCommit: "c1", ahead: 1)
            .LocalBranch("feature", "f2")
            .ViewRepoAsync("feature");

        Assert.AreEqual(
            "The selected commits are on different branches: select commits of one branch",
            CommitRewrite.WhyNotSquash(repo, C(repo, "c2"), C(repo, "f1"))
        );
        Assert.AreEqual(
            "Only commits on the current branch can be squashed",
            CommitRewrite.WhyNotSquash(repo, C(repo, "f2"), C(repo, "f1"))
        );
    }

    [TestMethod]
    public async Task TestNotWhileAnOperationIsInProgress()
    {
        var repo = await Ahead().WithStatus(conflicted: 1, operation: GitOp.Rebase).ViewRepoAsync();

        Assert.AreEqual(Why.InProgress, CommitRewrite.WhyNotAmend(repo, C(repo, "c3")));
    }
}
