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
// wanted:  ./test --filter "TestCategory!=Integration"
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

    // 'Merge to' is the one write operation that moves HEAD twice, so what it leaves behind is
    // asserted against real git rather than against a fake: the target checked out, the merge
    // staged but not committed, and the commits the caller needs for the commit dialog.
    [TestMethod]
    public async Task TestMergeToBranchOfARealRepo()
    {
        var service = await BuildBranchAsync();

        Assert.IsTrue(Try(out var augRepo, out var e, await service.GetRepoAsync(repo.Path)), $"Augment failed: {e}");
        Assert.IsTrue(Try(out var commits, out e, await service.MergeToBranchAsync(augRepo, "main")), $"Merge: {e}");

        // Now on the target, with the merge staged for the caller to commit
        Assert.AreEqual("main", await CurrentBranchAsync());
        Assert.AreEqual("Dev work", string.Join(", ", commits.Select(c => c.Subject)));

        Assert.IsTrue(Try(out var status, out e, await repo.Git.GetStatusAsync(repo.Path)), $"Status: {e}");
        Assert.IsTrue(status.IsMerging, "The merge is left uncommitted");
        StringAssert.StartsWith(status.MergeMessage, "Merge branch 'dev'");
    }

    // The other outcome the command has to tell apart: nothing to merge. It cannot be read off the
    // exit code, since git is happy either way, so it is the empty commit list that says so.
    [TestMethod]
    public async Task TestMergeToBranchThatIsAlreadyUpToDate()
    {
        var service = await BuildBranchAsync();

        Assert.IsTrue(Try(out var augRepo, out var e, await service.GetRepoAsync(repo.Path)), $"Augment failed: {e}");
        Assert.IsTrue(Try(out _, out e, await service.MergeToBranchAsync(augRepo, "main")), $"Merge: {e}");
        Assert.IsTrue(Try(out var status, out e, await repo.Git.GetStatusAsync(repo.Path)), $"Status: {e}");
        await repo.CommitAsync(status.MergeMessage);
        Assert.IsTrue(Try(out e, await repo.Git.CheckoutAsync("dev", repo.Path)), $"Checkout: {e}");

        // 'dev' is now in 'main', so merging it again brings in nothing
        Assert.IsTrue(Try(out augRepo, out e, await service.GetRepoAsync(repo.Path)), $"Augment failed: {e}");
        Assert.IsTrue(Try(out var commits, out e, await service.MergeToBranchAsync(augRepo, "main")), $"Merge: {e}");

        Assert.AreEqual(0, commits.Count);
        Assert.AreEqual("main", await CurrentBranchAsync());
        Assert.IsTrue(Try(out status, out e, await repo.Git.GetStatusAsync(repo.Path)), $"Status: {e}");
        Assert.IsFalse(status.IsMerging, "Nothing was merged, so there is nothing to commit");
    }

    // Initial on main and one commit on a 'dev' branched out from it, with 'dev' current, i.e.
    // the smallest repo that has something to merge back to main
    async Task<IAugmentedService> BuildBranchAsync()
    {
        await repo.CommitFileAsync("file.txt", "one\n", "Initial");
        Assert.IsTrue(Try(out var e, await repo.Git.CreateBranchAsync("dev", true, repo.Path)), $"Git failed: {e}");
        await repo.CommitFileAsync("dev.txt", "dev\n", "Dev work");

        return RepoBuilder.NewAugmentedService(repo.Git, new FakeMetaDataService(new MetaData()));
    }

    async Task<string> CurrentBranchAsync() => (await repo.GitAsync("rev-parse --abbrev-ref HEAD")).Trim();

    // Initial on main, a 'dev' branch out and back in again, i.e. the smallest repo where the
    // inference has something to infer
    async Task BuildBranchAndMergeAsync()
    {
        await repo.CommitFileAsync("file.txt", "one\n", "Initial");
        Assert.IsTrue(Try(out var e, await repo.Git.CreateBranchAsync("dev", true, repo.Path)), $"Git failed: {e}");
        await repo.CommitFileAsync("dev.txt", "dev\n", "Dev work");
        Assert.IsTrue(Try(out e, await repo.Git.CheckoutAsync("main", repo.Path)), $"Git failed: {e}");
        await repo.CommitFileAsync("main.txt", "main\n", "Main work");
        Assert.IsTrue(Try(out e, await repo.Git.MergeBranchAsync("dev", repo.Path)), $"Git failed: {e}");
        Assert.IsTrue(Try(out var status, out e, await repo.Git.GetStatusAsync(repo.Path)), $"Git failed: {e}");
        await repo.CommitAsync(status.MergeMessage);
    }

    // The real augmented service over the real git services, with only the file monitor and the
    // shared meta data faked out
    async Task<Repo> AugmentedRepoAsync()
    {
        var service = RepoBuilder.NewAugmentedService(repo.Git, new FakeMetaDataService(new MetaData()));

        Assert.IsTrue(Try(out var augRepo, out var e, await service.GetRepoAsync(repo.Path)), $"Augment failed: {e}");
        return augRepo;
    }

    static Repo ViewRepo(Repo augRepo, ShowBranches show = ShowBranches.Specified) =>
        new ViewRepoCreater(new ViewRepoConverter(), new FakeRepoConfig()).GetViewRepoAsync(augRepo, [], show, 10);
}
