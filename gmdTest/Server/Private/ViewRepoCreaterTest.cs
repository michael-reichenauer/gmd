using gmd.Common;
using gmd.Server;
using gmd.Server.Private;
using gmdTest.Fixtures;

namespace gmdTest.Server;

// The view repo is the subset of the augmented repo the user chose to see: the branches shown,
// their ancestors and related branches, and the commits on them. It is also where the properties
// that depend on what is shown are set, i.e. which commits are ahead or behind and the virtual
// uncommitted commit on top.
//
// These are characterization tests, pinning down what the code does today.
[TestClass]
public class ViewRepoCreaterTest
{
    static string[] BranchNames(Repo repo) => repo.ViewBranches.Select(b => b.Name).ToArray();

    static string[] Subjects(Repo repo) => repo.ViewCommits.Select(c => c.Subject).ToArray();

    // Showing a branch also shows what it hangs off, i.e. its ancestors, otherwise it would have
    // nothing to be drawn next to. Local and remote branches are related, so they come as a pair.
    [TestMethod]
    public async Task TestShownBranchBringsItsAncestorsAndRelatedBranches()
    {
        var repo = await ThreeBranches().ViewRepoAsync("dev");

        CollectionAssert.AreEqual(new[] { "origin/main", "main", "origin/dev", "dev" }, BranchNames(repo));
        CollectionAssert.AreEqual(
            new[]
            {
                "Merge branch 'feat' into main",
                "Merge branch 'dev' into main",
                "Dev work",
                "Third",
                "Second",
                "Initial",
            },
            Subjects(repo),
            "Only the commits of the shown branches, i.e. 'Feature work' is left out"
        );
    }

    // With no branches specified, the current branch is shown. The main branch is always included,
    // since it is what everything else is drawn relative to.
    [TestMethod]
    public async Task TestNoSpecifiedBranchesShowsTheCurrentAndMainBranch()
    {
        var repo = await ThreeBranches().ViewRepoAsync();

        CollectionAssert.AreEqual(new[] { "origin/main", "main" }, BranchNames(repo));
    }

    // 'Show all active branches' shows every branch that still exists in git
    [TestMethod]
    public async Task TestAllActiveShowsEveryGitBranch()
    {
        var repo = await ThreeBranches().ViewRepoAsync(ShowBranches.AllActive);

        CollectionAssert.AreEqual(new[] { "origin/main", "main", "origin/dev", "dev", "feat" }, BranchNames(repo));
    }

    // 'Show all recent branches' shows the given number of branches with the newest tips. 'feat'
    // was committed after 'dev', so it is the one that comes along.
    [TestMethod]
    public async Task TestAllRecentShowsTheBranchesWithTheNewestTips()
    {
        var repo = await ThreeBranches().ViewRepoAsync(ShowBranches.AllRecent, 2);

        CollectionAssert.AreEqual(new[] { "origin/main", "main", "feat" }, BranchNames(repo));
    }

    // A branch deleted after being merged is not a git branch, so it only shows up in the mode
    // that asks for deleted branches too. It is named '<nice name>:<sid of its bottom commit>',
    // since several deleted branches can have had the same name.
    [TestMethod]
    public async Task TestDeletedBranchesAreOnlyShownWhenAskedFor()
    {
        var b = WithDeletedBranch();
        var deleted = $"gone:{RepoBuilder.Sid("d1")}";

        CollectionAssert.AreEqual(
            new[] { "origin/main", "main" },
            BranchNames(await b.ViewRepoAsync(ShowBranches.AllActive))
        );
        CollectionAssert.AreEqual(
            new[] { "origin/main", "main", deleted },
            BranchNames(await b.ViewRepoAsync(ShowBranches.AllActiveAndDeleted))
        );
    }

    // The commits a local branch has and its remote does not are 'ahead', and the other way around
    // 'behind'. Both are properties of the view, since they depend on which commits are shown.
    [TestMethod]
    public async Task TestAheadAndBehindCommitsAreMarked()
    {
        var repo = await Diverged().ViewRepoAsync();

        CollectionAssert.AreEqual(
            new[] { "Local 2", "Local 1" },
            repo.ViewCommits.Where(c => c.IsAhead).Select(c => c.Subject).ToArray()
        );
        CollectionAssert.AreEqual(
            new[] { "Remote 1" },
            repo.ViewCommits.Where(c => c.IsBehind).Select(c => c.Subject).ToArray()
        );
    }

    // A branch pair with commits on both sides has local only and remote only commits, which is
    // what enables the push and pull menu items. Both branches of the pair get both flags, so
    // 'Push all branches' (which filters on HasLocalOnly && !HasRemoteOnly) leaves a diverged
    // branch alone rather than trying a push git would reject as non-fast-forward.
    [TestMethod]
    public async Task TestDivergedBranchesHaveLocalOnlyAndRemoteOnlyCommits()
    {
        var repo = await Diverged().ViewRepoAsync();

        foreach (var name in new[] { "origin/main", "main" })
        {
            Assert.IsTrue(repo.BranchByName[name].HasLocalOnly, name);
            Assert.IsTrue(repo.BranchByName[name].HasRemoteOnly, name);
        }
    }

    // A local branch that is behind and points at the first commit of the repo has no commit it
    // branched out from, which SetBehindCommits used to read unconditionally and crash on.
    [TestMethod]
    public async Task TestBranchBehindAtTheRootCommitHasRemoteOnlyCommits()
    {
        var repo = await BehindAtRootCommit().ViewRepoAsync();

        Assert.IsTrue(repo.BranchByName["origin/main"].HasRemoteOnly);
        Assert.IsTrue(repo.BranchByName["main"].HasRemoteOnly);
        CollectionAssert.AreEqual(
            new[] { "Remote 1" },
            repo.ViewCommits.Where(c => c.IsBehind).Select(c => c.Subject).ToArray()
        );
    }

    // A branch in sync with its remote has neither
    [TestMethod]
    public async Task TestSyncedBranchHasNoLocalOrRemoteOnlyCommits()
    {
        var repo = await ThreeBranches().ViewRepoAsync();

        Assert.IsFalse(repo.BranchByName["origin/main"].HasLocalOnly);
        Assert.IsFalse(repo.BranchByName["origin/main"].HasRemoteOnly);
        Assert.IsFalse(repo.ViewCommits.Any(c => c.IsAhead || c.IsBehind));
    }

    // Uncommitted changes are shown as a virtual commit on top of the current branch, so they can
    // be selected and diffed like any other commit
    [TestMethod]
    public async Task TestUncommittedChangesBecomeAVirtualCommit()
    {
        var repo = await new RepoBuilder()
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true)
            .WithStatus(modified: 2, added: 1)
            .ViewRepoAsync();

        var uncommitted = repo.ViewCommits[0];
        Assert.AreEqual(Repo.UncommittedId, uncommitted.Id);
        Assert.AreEqual("3 uncommitted changes", uncommitted.Subject);
        Assert.IsTrue(uncommitted.IsUncommitted);
        Assert.AreEqual("main", uncommitted.BranchName, "On the current branch, i.e. the local one");
        CollectionAssert.AreEqual(new[] { RepoBuilder.Sha("c2") }, uncommitted.ParentIds.ToArray());
        Assert.AreEqual(Repo.UncommittedId, repo.BranchByName["main"].TipId, "It becomes the branch tip");
    }

    // With no changes there is no uncommitted commit
    [TestMethod]
    public async Task TestNoChangesGivesNoUncommittedCommit()
    {
        var repo = await new RepoBuilder()
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c1", isCurrent: true)
            .ViewRepoAsync();

        Assert.IsFalse(repo.CommitById.ContainsKey(Repo.UncommittedId));
        CollectionAssert.AreEqual(new[] { "Initial" }, Subjects(repo));
    }

    // During a merge the uncommitted commit gets the merge source as a second parent, so the merge
    // is drawn while it is still in progress, and the subject says what is going on.
    [TestMethod]
    public async Task TestUncommittedCommitWhileMerging()
    {
        var repo = await new RepoBuilder()
            .Commit("d1", "Dev work", "c1")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true)
            .LocalBranch("dev", "d1")
            .WithStatus(
                conflicted: 2,
                operation: gmd.Git.GitOperation.Merge,
                mergeMessage: "Merge branch 'dev'",
                mergeHeadCommit: "d1"
            )
            .ViewRepoAsync("dev");

        var uncommitted = repo.ViewCommits[0];
        Assert.AreEqual("CONFLICTS: 2, Merge branch 'dev', 2 uncommitted changes", uncommitted.Subject);
        Assert.IsTrue(uncommitted.IsConflicted);
        CollectionAssert.AreEqual(
            new[] { RepoBuilder.Sha("c2"), RepoBuilder.Sha("d1") },
            uncommitted.ParentIds.ToArray(),
            "The merge source is the second parent, as for a real merge commit"
        );
    }

    // The worktrees ride along into the view repo, and into the filtered one — both of which are
    // built from scratch rather than with 'with', so they would silently drop them
    [TestMethod]
    public async Task TestWorktreesAreKeptByTheViewRepoAndTheFilteredRepo()
    {
        var builder = ThreeBranches().Worktree("/test/repo-dev", "dev", changes: 3);

        var repo = await builder.ViewRepoAsync("dev");
        Assert.AreEqual(2, repo.Worktrees.Count);
        Assert.AreEqual(3, repo.Worktrees[1].ChangesCount);
        Assert.AreEqual("/test/repo-dev", repo.BranchByName["dev"].WorktreePath);
        Assert.AreEqual("/test/repo-dev", repo.WorktreePathOf(repo.BranchByName["origin/dev"]));

        var filtered = await builder.FilteredViewRepoAsync("Initial");
        Assert.AreEqual(2, filtered.Worktrees.Count);
    }

    // The shown branches are sorted after their ancestors, i.e. main first and a branch started from
    // dev after dev, and the branches that are not related are sorted as the user moved them, by
    // their primary names. Ancestry goes before the user's order, which cannot put 'extra' before
    // the dev it was started from, and of two orders of the same two branches the first holds.
    // What is left unordered is in the order the sort leaves it, which is what the graph's columns
    // are, so a faster sort must leave it the same.
    [TestMethod]
    public async Task TestBranchesAreSortedByAncestryAndThenByTheUsersOrder()
    {
        var builder = SiblingBranches();
        var shown = await builder.ViewRepoAsync(ShowBranches.AllActive);
        CollectionAssert.AreEqual(
            new[] { "origin/main", "main", "feat1", "feat2", "feat3", "dev", "extra" },
            BranchNames(shown)
        );

        builder.Config.Set(
            "/test/repo",
            c =>
                c.BranchOrders = [
                    new BranchOrder
                    {
                        Branch = "feat3",
                        Other = "feat1",
                        Order = -1,
                    },
                    new BranchOrder
                    {
                        Branch = "feat1",
                        Other = "feat2",
                        Order = 1,
                    },
                    new BranchOrder
                    {
                        Branch = "extra",
                        Other = "dev",
                        Order = -1,
                    },
                    new BranchOrder
                    {
                        Branch = "dev",
                        Other = "feat3",
                        Order = -1,
                    },
                    new BranchOrder
                    {
                        Branch = "dev",
                        Other = "feat3",
                        Order = 1,
                    },
                ]
        );
        var ordered = await builder.ViewRepoAsync(ShowBranches.AllActive);

        CollectionAssert.AreEqual(
            new[] { "origin/main", "main", "dev", "feat3", "feat2", "feat1", "extra" },
            BranchNames(ordered)
        );
    }

    // Orders that contradict each other, each branch after the other, as a rename could leave them,
    // have no order to reach, and the sort swapped the two forever, which hung gmd on every refresh.
    // Now the sort leaves the place it cannot settle as it is and goes on. Characterization: the
    // order below is where that stop leaves them, and the timeout is what fails if it does not end.
    [TestMethod]
    [Timeout(5000)]
    public async Task TestOrdersThatContradictEachOtherStillEnd()
    {
        var builder = SiblingBranches();
        builder.Config.Set(
            "/test/repo",
            c =>
                c.BranchOrders = [
                    new BranchOrder
                    {
                        Branch = "feat1",
                        Other = "feat2",
                        Order = 1,
                    },
                    new BranchOrder
                    {
                        Branch = "feat2",
                        Other = "feat1",
                        Order = 1,
                    },
                ]
        );

        var ordered = await builder.ViewRepoAsync(ShowBranches.AllActive);

        CollectionAssert.AreEqual(
            new[] { "origin/main", "main", "feat2", "feat1", "feat3", "dev", "extra" },
            BranchNames(ordered)
        );
    }

    // The branches are sorted by comparing each with only the few it can go after, which must come to
    // the very same order as comparing every pair, over random hierarchies and random user orders.
    // The user orders follow a random order the ancestry allows, some twice, some both ways, and some
    // name a branch that is not shown, as renames and deletes leave them.
    [TestMethod]
    public void TestSortingBranchesIsTheSameAsComparingEveryPair()
    {
        var random = new Random(42);
        for (var run = 0; run < 2000; run++)
        {
            var (branches, orders) = RandomBranches(random);

            var expected = branches.ToList();
            Sorter.Sort(expected, (b1, b2) => ViewRepoCreater.CompareBranches(b1, b2, orders));
            var actual = branches.ToList();
            ViewRepoCreater.SortPrimaryBranches(actual, orders);

            CollectionAssert.AreEqual(
                expected.Select(b => b.Name).ToList(),
                actual.Select(b => b.Name).ToList(),
                $"Run {run}"
            );
        }
    }

    static (List<Branch>, List<BranchOrder>) RandomBranches(Random random)
    {
        var count = random.Next(0, 30);
        var parents = Enumerable
            .Range(0, count)
            .Select(i => i > 0 && random.Next(5) > 0 ? random.Next(i) : -1)
            .ToList();
        List<int> Ancestors(int i) => parents[i] == -1 ? [] : [parents[i], .. Ancestors(parents[i])];
        bool IsRelated(int a, int b) => Ancestors(a).Contains(b) || Ancestors(b).Contains(a);

        // A random order that has every branch after its ancestors, which the user orders follow
        List<int> ranked = [];
        while (ranked.Count < count)
        {
            var ready = Enumerable
                .Range(0, count)
                .Where(i => !ranked.Contains(i) && (parents[i] == -1 || ranked.Contains(parents[i])))
                .ToList();
            ranked.Add(ready[random.Next(ready.Count)]);
        }

        List<BranchOrder> orders = [];
        var orderCount = count > 1 ? random.Next(0, 2 * count) : 0;
        for (var n = 0; n < orderCount; n++)
        {
            var (a, b) = (random.Next(count), random.Next(count));
            if (a == b || IsRelated(a, b))
                continue;
            var (first, second) = ranked.IndexOf(a) < ranked.IndexOf(b) ? ($"b{a}", $"b{b}") : ($"b{b}", $"b{a}");
            var order = random.Next(3) switch
            {
                0 => new BranchOrder
                {
                    Branch = first,
                    Other = second,
                    Order = -1,
                },
                1 => new BranchOrder
                {
                    Branch = second,
                    Other = first,
                    Order = 1,
                },
                _ => new BranchOrder
                {
                    Branch = "gone",
                    Other = first,
                    Order = 1,
                },
            };
            orders.Add(order);
            if (random.Next(5) == 0) // The same again the other way, which the first overrides
                orders.Add(
                    new BranchOrder
                    {
                        Branch = order.Branch,
                        Other = order.Other,
                        Order = -order.Order,
                    }
                );
            if (random.Next(5) == 0) // Each before the other, which orders neither
                orders.AddRange([
                    new BranchOrder
                    {
                        Branch = first,
                        Other = second,
                        Order = -1,
                    },
                    new BranchOrder
                    {
                        Branch = second,
                        Other = first,
                        Order = -1,
                    },
                ]);
        }

        var branches = Enumerable
            .Range(0, count)
            .Select(i =>
                NewBranch($"b{i}", parents[i] == -1 ? "" : $"b{parents[i]}", Ancestors(i).Select(a => $"b{a}"))
            )
            .OrderBy(_ => random.Next())
            .ToList();
        return (branches, orders);
    }

    static Branch NewBranch(string name, string parentName, IEnumerable<string> ancestorNames) =>
        new Branch(
            Name: name,
            PrimaryName: name,
            PrimaryBaseName: name,
            NiceName: name,
            NiceNameUnique: name,
            TipId: "",
            BottomId: "",
            IsCurrent: false,
            IsLocalCurrent: false,
            IsRemote: false,
            RemoteName: "",
            LocalName: "",
            WorktreePath: "",
            IsInView: false,
            IsGitBranch: true,
            IsDetached: false,
            IsPrimary: true,
            IsMainBranch: false,
            ParentBranchName: parentName,
            PullMergeParentBranchName: "",
            HasLocalOnly: false,
            HasRemoteOnly: false,
            AmbiguousTipId: "",
            AmbiguousBranchNames: [],
            PullMergeBranchNames: [],
            AncestorNames: ancestorNames.ToList(),
            RelatedBranchNames: [],
            IsCircularAncestors: false,
            X: 0,
            IsIn: false,
            IsOut: false
        );

    static RepoBuilder SiblingBranches() =>
        new RepoBuilder()
            .Commit("e1", "Extra work", "d1")
            .Commit("f3", "Feature 3 work", "c2")
            .Commit("d1", "Dev work", "c2")
            .Commit("f2", "Feature 2 work", "c2")
            .Commit("f1", "Feature 1 work", "c1")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true)
            .LocalBranch("feat1", "f1")
            .LocalBranch("feat2", "f2")
            .LocalBranch("feat3", "f3")
            .LocalBranch("dev", "d1")
            .LocalBranch("extra", "e1");

    static RepoBuilder ThreeBranches() =>
        new RepoBuilder()
            .Commit("c5", "Merge branch 'feat' into main", "c4", "f1")
            .Commit("f1", "Feature work", "c2")
            .Commit("c4", "Merge branch 'dev' into main", "c3", "d1")
            .Commit("d1", "Dev work", "c2")
            .Commit("c3", "Third", "c2")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c5", isCurrent: true)
            .BranchWithRemote("dev", "d1")
            .LocalBranch("feat", "f1");

    static RepoBuilder WithDeletedBranch() =>
        new RepoBuilder()
            .Commit("c3", "Merge branch 'gone' into main", "c2", "d1")
            .Commit("d1", "Work on gone", "c1")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c3", isCurrent: true);

    // main behind its remote, and pointing at the first commit of the repo, i.e. a local branch
    // whose bottom commit has no parent
    static RepoBuilder BehindAtRootCommit() =>
        new RepoBuilder()
            .Commit("r1", "Remote 1", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c1", isCurrent: true, remoteTipCommit: "r1", behind: 1);

    // main where the local branch has two commits the remote does not, and the remote one the
    // local does not
    static RepoBuilder Diverged() =>
        new RepoBuilder()
            .Commit("l2", "Local 2", "l1")
            .Commit("l1", "Local 1", "c2")
            .Commit("r1", "Remote 1", "c2")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "l2", isCurrent: true, remoteTipCommit: "r1", ahead: 2, behind: 1);
}
