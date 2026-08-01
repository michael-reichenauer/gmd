using gmd.Server;
using gmd.Server.Private;
using gmd.Server.Private.Augmented.Private;
using gmdTest.Fixtures;
using AugConverter = gmd.Server.Private.Augmented.Private.Converter;
using ViewConverter = gmd.Server.Private.Converter;

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
        var service = new AugmentedService(
            repo.Git,
            new Augmenter(new BranchStructureService(new BranchNameService())),
            new AugConverter(),
            new FakeFileMonitor(),
            new FakeMetaDataService(new MetaData())
        );

        Assert.IsTrue(Try(out var augRepo, out var e, await service.GetRepoAsync(repo.Path)), $"Augment failed: {e}");
        return augRepo;
    }

    static Repo ViewRepo(Repo augRepo, ShowBranches show = ShowBranches.Specified) =>
        new ViewRepoCreater(new ViewConverter(), new FakeRepoConfig()).GetViewRepoAsync(augRepo, [], show, 10);
}
