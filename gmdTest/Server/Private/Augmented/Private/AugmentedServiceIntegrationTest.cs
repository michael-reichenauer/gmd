using gmd.Git;
using gmd.Server;
using gmd.Server.Private;
using gmd.Server.Private.Augmented;
using gmd.Server.Private.Augmented.Private;
using gmdTest.Fixtures;

namespace gmdTest.Server.Private.Augmented.Private;

// The whole inference pipeline over a real repository: git → AugmentedService → ViewRepoCreater
// → the graph the UI draws. The other pipeline tests build the git facts by hand with
// RepoBuilder, so this is the one place where real git output reaches the augmenter.
//
// The tests are in their own category, so they can be excluded when only the fast tests are
// wanted:  scripts/test --filter "TestCategory!=Integration"
[TestClass]
[TestCategory("Integration")]
public class AugmentedServiceIntegrationTest
{
    TempRepo repo = null!;

    [TestInitialize]
    public async Task Init() => repo = await TempRepo.CreateAsync();

    [TestCleanup]
    public void Cleanup() => repo.Dispose();

    [TestMethod]
    public async Task TestAugmentedRepoOfARealRepo()
    {
        await BuildBranchAndMergeAsync();

        var augRepo = await AugmentedRepoAsync();

        // Which branch a commit belongs to is not recorded by git, it is inferred here. The
        // merged in commit is on 'dev' only because the merge subject still names the branch.
        Assert.AreEqual(
            "Merge branch 'dev' main, Main work main, Dev work dev, Initial main",
            string.Join(", ", augRepo.AllCommits.Select(c => $"{c.Subject} {c.BranchName}"))
        );
        Assert.IsFalse(augRepo.AllCommits.Any(c => c.IsAmbiguous));

        Assert.AreEqual(
            "dev parent:main, main parent:",
            string.Join(", ", augRepo.AllBranches.Select(b => $"{b.Name} parent:{b.ParentBranchName}"))
        );
        Assert.AreEqual("main", augRepo.AllBranches.First(b => b.IsCurrent).Name);
    }

    // Two branches stacked with 'checkout -b', on no remote: the commit the second started at could
    // be on either as far as the graph goes. The reflog says it was made on the first, and that is
    // kept in the metadata, for when the reflog is gone.
    [TestMethod]
    public async Task TestReflogOfARealRepoDecidesAndIsKept()
    {
        await repo.CommitFileAsync("file.txt", "one\n", "Initial");
        await repo.GitAsync("checkout -b feature1");
        var e1 = await repo.CommitFileAsync("feature1.txt", "one\n", "Feature 1 work");
        await repo.GitAsync("checkout -b feature2");
        await repo.CommitFileAsync("feature2.txt", "two\n", "Feature 2 work");
        await repo.GitAsync("checkout main");
        await repo.CommitFileAsync("main.txt", "main\n", "Main work");

        var metaDataService = new MetaDataService(repo.Git, new FakeRepoConfig(), new MetaDataFile());
        var service = RepoBuilder.NewAugmentedService(repo.Git, metaDataService);
        var augRepo = AssertOk(await service.GetRepoAsync(repo.Path));

        var commit = augRepo.CommitById[e1];
        Assert.AreEqual("feature1", commit.BranchName);
        Assert.IsFalse(commit.IsAmbiguous);

        // Kept in the background, after the repo is read
        var deadline = DateTime.UtcNow.AddSeconds(10);
        string kept = "";
        while (DateTime.UtcNow < deadline)
        {
            if (AssertOk(await metaDataService.GetMetaDataAsync(repo.Path)).TryGetWitnessedBranch(e1, out kept))
                break;
            await Task.Delay(50);
        }
        Assert.AreEqual("feature1", kept);
    }

    // A topic merged by a fast-forward into the branch it was started from, which then went on: the
    // topic's commit is on that branch's line now, as is the branch's own commit below it, though
    // the reflog says the topic's was made on the topic. Names with no seniority, so that it is the
    // reflog deciding.
    [TestMethod]
    public async Task TestReflogOfARealRepoKeepsAFastForwardedBranchsLine()
    {
        await repo.CommitFileAsync("file.txt", "one\n", "Initial");
        await repo.GitAsync("checkout -b base");
        var b1 = await repo.CommitFileAsync("base.txt", "one\n", "Base 1");
        await repo.GitAsync("checkout -b topic");
        var x1 = await repo.CommitFileAsync("topic.txt", "one\n", "Topic work");
        await repo.GitAsync("checkout base");
        await repo.GitAsync("merge topic");
        await repo.CommitFileAsync("base2.txt", "two\n", "Base 2");
        await repo.GitAsync("checkout main");
        await repo.CommitFileAsync("main.txt", "main\n", "Main work");

        var service = RepoBuilder.NewAugmentedService(repo.Git, new FakeMetaDataService(new MetaData()));
        var augRepo = AssertOk(await service.GetRepoAsync(repo.Path));

        Assert.AreEqual("base", augRepo.CommitById[x1].BranchName);
        Assert.AreEqual("base", augRepo.CommitById[b1].BranchName);
    }

    // The integration branches configured for the repo are read with it: staging, where a feature
    // started, goes on below the branch point
    [TestMethod]
    public async Task TestConfiguredIntegrationBranchesAreReadWithTheRepo()
    {
        await repo.CommitFileAsync("file.txt", "one\n", "Initial");
        await repo.GitAsync("branch staging");
        await repo.GitAsync("branch feature");
        await repo.CommitFileAsync("main.txt", "main\n", "Main work");
        await repo.GitAsync("checkout staging");
        var e1 = await repo.CommitFileAsync("staging.txt", "one\n", "Staging 1");
        await repo.CommitFileAsync("staging2.txt", "two\n", "Staging 2");
        await repo.GitAsync($"branch -f feature {e1}");
        await repo.GitAsync("checkout feature");
        await repo.CommitFileAsync("feature.txt", "f\n", "Feature work");
        await repo.GitAsync("reflog expire --expire=now --all"); // The reflog would say it otherwise

        var repoConfig = new FakeRepoConfig();
        repoConfig.Set(repo.Path, c => c.IntegrationBranches = ["staging"]);
        var service = RepoBuilder.NewAugmentedService(
            repo.Git,
            new FakeMetaDataService(new MetaData()),
            new FakeFileMonitor(),
            repoConfig
        );
        var augRepo = AssertOk(await service.GetRepoAsync(repo.Path));

        Assert.AreEqual("staging", augRepo.CommitById[e1].BranchName);
        Assert.IsFalse(augRepo.CommitById[e1].IsAmbiguous);
    }

    [TestMethod]
    public async Task TestGraphOfARealRepo()
    {
        await BuildBranchAndMergeAsync();
        var augRepo = await AugmentedRepoAsync();

        // The default view shows the current branch, so the merged in branch is only the line
        // the merge commit branches out on
        Assert.AreEqual(
            """
            ┣╮  Merge branch 'dev'
            ┣   Main work
            ┗╯  Initial
            """,
            GraphText.WithSubjects(ViewRepo(augRepo))
        );

        Assert.AreEqual(
            """
            ┣╮   Merge branch 'dev'
            ┣│   Main work
            ┃├┺  Dev work
            ┗╯   Initial
            """,
            GraphText.WithSubjects(ViewRepo(augRepo, ShowBranches.AllRecent))
        );
    }

    [TestMethod]
    public async Task TestUncommittedChangesOfARealRepo()
    {
        await repo.CommitFileAsync("file.txt", "one\n", "Initial");
        repo.WriteFile("file.txt", "one\nchanged\n");
        repo.WriteFile("new.txt", "new\n");

        var augRepo = await AugmentedRepoAsync();

        // The uncommitted commit is not a git commit, AugmentedService adds it from git status
        var uncommitted = augRepo.AllCommits[0];
        Assert.AreEqual(Repo.UncommittedId, uncommitted.Id);
        Assert.AreEqual("2 uncommitted changes", uncommitted.Subject);
        Assert.AreEqual("main", uncommitted.BranchName);

        Assert.AreEqual(
            """
            ┣  2 uncommitted changes
            ┗  Initial
            """,
            GraphText.WithSubjects(ViewRepo(augRepo))
        );
    }

    // A linked worktree, seen from the main worktree and from inside it: which one the repo was
    // read from, which branch the other holds, and its uncommitted changes
    [TestMethod]
    public async Task TestWorktreesOfARealRepo()
    {
        await repo.CommitFileAsync("file.txt", "one\n", "Initial");
        AssertOk(await repo.Git.CreateBranchAsync("dev", false, repo.Path));
        var worktree = await repo.AddWorktreeAsync("dev", "dev");
        File.WriteAllText(Path.Join(worktree, "new.txt"), "new\n");

        var augRepo = await AugmentedRepoAsync();

        Assert.AreEqual(2, augRepo.Worktrees.Count);
        var main = augRepo.Worktrees[0];
        Assert.IsTrue(main.IsMain);
        Assert.IsTrue(main.IsCurrent);
        Assert.AreEqual("main", main.Branch);
        var dev = augRepo.Worktrees[1];
        Assert.AreEqual(worktree, dev.Path);
        Assert.AreEqual("dev", dev.Branch);
        Assert.IsFalse(dev.IsCurrent);
        Assert.AreEqual(-1, dev.ChangesCount, "The other worktrees' changes are not read with the repo");
        Assert.AreEqual(worktree, augRepo.BranchByName["dev"].WorktreePath);
        Assert.AreEqual("", augRepo.BranchByName["main"].WorktreePath);

        // Their changes are read on their own, after the repo is shown, so a status run in a
        // large worktree never delays showing this one
        var service = RepoBuilder.NewAugmentedService(repo.Git, new FakeMetaDataService(new MetaData()));
        var updated = AssertOk(await service.GetUpdatedWorktreesRepoAsync(augRepo));
        Assert.AreEqual(1, updated.Worktrees[1].ChangesCount);
        Assert.AreEqual(0, updated.Worktrees[0].ChangesCount, "The current worktree's changes are the repo's status");

        // From inside the worktree it is the other way around, and the changes are its status
        var fromWorktree = AssertOk(await service.GetRepoAsync(worktree));
        Assert.AreEqual(worktree, fromWorktree.Path);
        Assert.IsFalse(fromWorktree.Worktrees[0].IsCurrent);
        Assert.IsTrue(fromWorktree.Worktrees[1].IsCurrent);
        Assert.AreEqual(repo.Path, fromWorktree.BranchByName["main"].WorktreePath);
        Assert.AreEqual("", fromWorktree.BranchByName["dev"].WorktreePath);
        Assert.AreEqual("dev", fromWorktree.CurrentBranch().Name);
        Assert.AreEqual("1 uncommitted change", fromWorktree.AllCommits[0].Subject);
    }

    // 'Merge to' is the one write operation that moves HEAD twice, so what it leaves behind is
    // asserted against real git rather than against a fake: the target checked out, the merge
    // staged but not committed, and the commits the caller needs for the commit dialog.
    [TestMethod]
    public async Task TestMergeToBranchOfARealRepo()
    {
        var service = await BuildBranchAsync();

        var augRepo = AssertOk(await service.GetRepoAsync(repo.Path));
        var commits = AssertOk(await BranchWrites().MergeToBranchAsync(augRepo, "main"));

        // Now on the target, with the merge staged for the caller to commit
        Assert.AreEqual("main", await CurrentBranchAsync());
        Assert.AreEqual("Dev work", string.Join(", ", commits.Select(c => c.Subject)));

        var status = AssertOk(await repo.Git.GetStatusAsync(repo.Path));
        Assert.IsTrue(status.IsMerging, "The merge is left uncommitted");
        StringAssert.StartsWith(status.MergeMessage, "Merge branch 'dev'");
    }

    // The other outcome the command has to tell apart: nothing to merge. It cannot be read off the
    // exit code, since git is happy either way, so it is the empty commit list that says so.
    [TestMethod]
    public async Task TestMergeToBranchThatIsAlreadyUpToDate()
    {
        var service = await BuildBranchAsync();

        var augRepo = AssertOk(await service.GetRepoAsync(repo.Path));
        AssertOk(await BranchWrites().MergeToBranchAsync(augRepo, "main"));
        var status = AssertOk(await repo.Git.GetStatusAsync(repo.Path));
        await repo.CommitAsync(status.MergeMessage);
        AssertOk(await repo.Git.CheckoutAsync("dev", repo.Path));

        // 'dev' is now in 'main', so merging it again brings in nothing
        augRepo = AssertOk(await service.GetRepoAsync(repo.Path));
        var commits = AssertOk(await BranchWrites().MergeToBranchAsync(augRepo, "main"));

        Assert.AreEqual(0, commits.Count);
        Assert.AreEqual("main", await CurrentBranchAsync());
        status = AssertOk(await repo.Git.GetStatusAsync(repo.Path));
        Assert.IsFalse(status.IsMerging, "Nothing was merged, so there is nothing to commit");
    }

    // Rebase onto, asked to push, force pushes over origin's branch as the repo had it when the user chose
    // it, not as a fetch may have moved it since, so a commit someone else pushed meanwhile is kept,
    // and the error says the rebase is done and the push is not
    [TestMethod]
    public async Task TestRebaseAndPushKeepsACommitPushedSinceTheRepoWasRead()
    {
        var service = await BuildBranchAsync();
        await repo.AddOriginAsync();
        await repo.GitAsync("push -q origin main");
        await repo.GitAsync("push -q --set-upstream origin dev");
        AssertOk(await repo.Git.CheckoutAsync("main", repo.Path));
        await repo.CommitFileAsync("main.txt", "main\n", "Main work");
        AssertOk(await repo.Git.CheckoutAsync("dev", repo.Path));
        var shown = AssertOk(await service.GetRepoAsync(repo.Path));
        var others = await repo.CommitFileAsync("other.txt", "other\n", "Someone else's");
        await repo.GitAsync("push -q origin dev"); // As pushed elsewhere and fetched here
        await repo.GitAsync("reset -q --hard HEAD~1");

        var error = AssertError(await BranchWrites().RebaseBranchAsync(shown, "main", isPush: true));

        StringAssert.Contains(error.AllMessages(), "Rebased 'dev' onto 'main', but did not push it");
        StringAssert.Contains(error.AllMessages(), "origin/dev has changed since gmd showed it");
        Assert.AreEqual(others, (await repo.GitAsync("ls-remote origin refs/heads/dev")).Split('\t')[0]);
        Assert.AreEqual("Main work", (await repo.GitAsync("log -1 --format=%s dev~1")).Trim(), "Rebased");
    }

    IBranchWriteService BranchWrites() =>
        new BranchWriteService(repo.Git, new FakeMetaDataService(new MetaData()), new FakeRepoConfig());

    // Initial on main and one commit on a 'dev' branched out from it, with 'dev' current, i.e.
    // the smallest repo that has something to merge back to main
    async Task<IAugmentedService> BuildBranchAsync()
    {
        await repo.CommitFileAsync("file.txt", "one\n", "Initial");
        AssertOk(await repo.Git.CreateBranchAsync("dev", true, repo.Path));
        await repo.CommitFileAsync("dev.txt", "dev\n", "Dev work");

        return RepoBuilder.NewAugmentedService(repo.Git, new FakeMetaDataService(new MetaData()));
    }

    async Task<string> CurrentBranchAsync() => (await repo.GitAsync("rev-parse --abbrev-ref HEAD")).Trim();

    // Initial on main, a 'dev' branch out and back in again, i.e. the smallest repo where the
    // inference has something to infer
    async Task BuildBranchAndMergeAsync()
    {
        await repo.CommitFileAsync("file.txt", "one\n", "Initial");
        AssertOk(await repo.Git.CreateBranchAsync("dev", true, repo.Path));
        await repo.CommitFileAsync("dev.txt", "dev\n", "Dev work");
        AssertOk(await repo.Git.CheckoutAsync("main", repo.Path));
        await repo.CommitFileAsync("main.txt", "main\n", "Main work");
        AssertOk(await repo.Git.MergeBranchAsync("dev", repo.Path));
        var status = AssertOk(await repo.Git.GetStatusAsync(repo.Path));
        await repo.CommitAsync(status.MergeMessage);
    }

    // Who the user is, for the branch menu's own branches, is read with the repo and kept with it in
    // the view repo. It was a side effect of reading the log, kept by the git layer for whatever repo
    // was read last. No user.name is no failure.
    [TestMethod]
    public async Task TestTheCurrentAuthorIsReadWithTheRepo()
    {
        await repo.CommitFileAsync("file.txt", "one\n", "Initial");

        Assert.AreEqual(TempRepo.AuthorName, ViewRepo(await AugmentedRepoAsync()).CurrentAuthor);

        await repo.GitAsync("config --unset user.name");
        Assert.AreEqual("", ViewRepo(await AugmentedRepoAsync()).CurrentAuthor);
    }

    // The real augmented service over the real git services, with only the file monitor and the
    // shared meta data faked out
    async Task<Repo> AugmentedRepoAsync()
    {
        var service = RepoBuilder.NewAugmentedService(repo.Git, new FakeMetaDataService(new MetaData()));

        var augRepo = AssertOk(await service.GetRepoAsync(repo.Path));
        return augRepo;
    }

    static Repo ViewRepo(Repo augRepo, ShowBranches show = ShowBranches.Specified) =>
        new ViewRepoCreater(new ViewRepoConverter(), new FakeRepoConfig()).GetViewRepoAsync(augRepo, [], show, 10);

    // Commits made on main by mistake, moved to a new branch: it is checked out where main was, and
    // main goes back to the commit before them, its last change a reset, which Undo takes back. The
    // reflog says the commits were made on main, but they are drawn on the new branch, since main
    // no longer has them.
    [TestMethod]
    public async Task TestCommitsMovedToANewBranch()
    {
        var service = RepoBuilder.NewAugmentedService(repo.Git, new FakeMetaDataService(new MetaData()));
        var initial = await repo.CommitFileAsync("a.txt", "a\n", "Initial");
        var first = await repo.CommitFileAsync("b.txt", "b\n", "Meant for a branch");
        var second = await repo.CommitFileAsync("c.txt", "c\n", "This too");
        repo.WriteFile("d.txt", "d\n");

        AssertOk(
            await BranchWrites()
                .MoveCommitsToNewBranchAsync(AssertOk(await service.GetRepoAsync(repo.Path)), "fix", first)
        );

        Assert.AreEqual("fix", (await repo.GitAsync("branch --show-current")).Trim());
        Assert.AreEqual(initial, (await repo.GitAsync("rev-parse main")).Trim());
        Assert.AreEqual(second, (await repo.GitAsync("rev-parse fix")).Trim());
        Assert.AreEqual("?? d.txt", (await repo.GitAsync("status --porcelain")).Trim(), "The change is left as it was");
        var moved = AssertOk(await service.GetRepoAsync(repo.Path));
        Assert.AreEqual(
            "fix, fix, main",
            string.Join(", ", new[] { second, first, initial }.Select(id => moved.CommitById[id].BranchName))
        );
        Assert.AreEqual(StepKind.Reset, moved.UndoSteps["main"].Kind);
    }

    // dev has two commits of Anna's, made from main, which has moved on since
    async Task<(string One, string Two)> DevOfAnnasAsync(string twoFile = "two.txt")
    {
        await repo.CommitFileAsync("a.txt", "a\n", "Initial");
        await repo.GitAsync("checkout -q -b dev");
        File.WriteAllText(Path.Join(repo.Path, "one.txt"), "one\n");
        await repo.GitAsync("add .");
        await repo.GitAsync("-c user.name=\"Anna Lind\" -c user.email=anna@x commit -qm \"Dev one\"");
        File.WriteAllText(Path.Join(repo.Path, twoFile), "two\n");
        await repo.GitAsync("add .");
        await repo.GitAsync("-c user.name=\"Anna Lind\" -c user.email=anna@x commit -qm \"Dev two\"");
        var two = (await repo.GitAsync("rev-parse HEAD")).Trim();
        var one = (await repo.GitAsync("rev-parse HEAD~1")).Trim();
        await repo.GitAsync("checkout -q main");
        await repo.CommitFileAsync("main.txt", "main\n", "Main work");
        return (one, two);
    }

    // Several commits are picked by git's own cherry pick, each committed as it was made, its author
    // kept, and Undo takes them all back as one change
    [TestMethod]
    public async Task TestCherryPickSeveralCommitsAndUndoThem()
    {
        var config = new FakeRepoConfig();
        var service = RepoBuilder.NewAugmentedService(
            repo.Git,
            new FakeMetaDataService(new MetaData()),
            new FakeFileMonitor(),
            config
        );
        var (one, two) = await DevOfAnnasAsync();
        var before = await repo.HeadIdAsync();
        var read = AssertOk(await service.GetRepoAsync(repo.Path));

        var writes = new BranchWriteService(repo.Git, new FakeMetaDataService(new MetaData()), config);
        AssertOk(await writes.CherryPickCommitsAsync(read, [read.CommitById[one], read.CommitById[two]]));

        Assert.AreEqual(
            "Dev two Anna Lind\nDev one Anna Lind\nMain work Test User",
            (await repo.GitAsync("log --format=\"%s %an\" -3")).Trim()
        );
        var picked = AssertOk(await service.GetRepoAsync(repo.Path));
        var step = picked.UndoSteps["main"];
        Assert.AreEqual((StepKind.CherryPick, "Dev one, and 1 more"), (step.Kind, step.Name));
        AssertOk(await new UndoService(repo.Git, config).UndoStepAsync(picked, step));
        Assert.AreEqual(before, await repo.HeadIdAsync());
    }

    // One that conflicts stops the pick there, git's own sequence keeping the ones left, which Continue
    // picks once the conflict is resolved
    [TestMethod]
    public async Task TestACherryPickThatConflictsIsContinued()
    {
        var service = RepoBuilder.NewAugmentedService(repo.Git, new FakeMetaDataService(new MetaData()));
        var (one, two) = await DevOfAnnasAsync(twoFile: "main.txt");
        var read = AssertOk(await service.GetRepoAsync(repo.Path));

        var error = AssertError(
            await BranchWrites().CherryPickCommitsAsync(read, [read.CommitById[two], read.CommitById[one]])
        );

        Assert.IsTrue(ConflictError.IsIn(error), error.AllMessages());
        File.WriteAllText(Path.Join(repo.Path, "main.txt"), "both\n");
        await repo.GitAsync("add main.txt");
        AssertOk(await repo.Git.ContinueOperationAsync(repo.Path));
        Assert.AreEqual("Dev one\nDev two\nMain work", (await repo.GitAsync("log --format=%s -3")).Trim());
    }
}
