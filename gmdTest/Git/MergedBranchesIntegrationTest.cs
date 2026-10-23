using gmdTest.Fixtures;

namespace gmdTest.Git;

// Which branches are merged, over a real repository: as git counts it ('git branch --merged'), and by
// content, as GitHub's squash merge and rebase merge leave it ('git merge-tree'), which canned output
// cannot tell
//
// The tests are in their own category, so they can be excluded when only the fast tests are
// wanted:  scripts/test --filter "TestCategory!=Integration"
[TestClass]
[TestCategory("Integration")]
public class MergedBranchesIntegrationTest
{
    TempRepo repo = null!;
    int minute = 0;

    [TestInitialize]
    public async Task Init()
    {
        repo = await TempRepo.CreateAsync();
        await CommitAsync("a.txt", "a\n", "Initial");
    }

    [TestCleanup]
    public void Cleanup() => repo.Dispose();

    Task<string> CommitAsync(string name, string text, string message) =>
        repo.CommitFileAtAsync(name, text, message, TempRepo.BaseTime.AddMinutes(minute++));

    // feature: two commits on login.txt, made from main, which moves on meanwhile with a file of its own
    async Task<string> FeatureAsync()
    {
        await repo.GitAsync("checkout -q -b feature");
        await CommitAsync("login.txt", "form\n", "Add login form");
        var tip = await CommitAsync("login.txt", "form\nvalidate\n", "Validate login");
        await repo.GitAsync("checkout -q main");
        await CommitAsync("b.txt", "b\n", "Main work");
        return tip;
    }

    async Task<string> MainAsync() => (await repo.GitAsync("rev-parse main")).Trim();

    [TestMethod]
    public async Task TestASquashMergeIsMergedByContent()
    {
        var feature = await FeatureAsync();
        await repo.GitAsync("merge -q --squash feature");
        repo.GitAt(["commit", "-q", "-m", "Login (#12)"], TempRepo.BaseTime.AddMinutes(minute++));
        await CommitAsync("c.txt", "c\n", "Later work");

        Assert.IsTrue(AssertOk(await repo.Git.IsMergedByContentAsync(await MainAsync(), feature, repo.Path)));
    }

    [TestMethod]
    public async Task TestARebaseMergeIsMergedByContent()
    {
        var feature = await FeatureAsync();
        await repo.GitAsync("cherry-pick feature~1 feature");

        Assert.IsTrue(AssertOk(await repo.Git.IsMergedByContentAsync(await MainAsync(), feature, repo.Path)));
    }

    [TestMethod]
    public async Task TestABranchNotMergedIsNot()
    {
        var feature = await FeatureAsync();

        Assert.IsFalse(AssertOk(await repo.Git.IsMergedByContentAsync(await MainAsync(), feature, repo.Path)));
    }

    // A part of the branch that went in is not all of it
    [TestMethod]
    public async Task TestABranchPartlyMergedIsNot()
    {
        var feature = await FeatureAsync();
        await repo.GitAsync("cherry-pick feature~1");

        Assert.IsFalse(AssertOk(await repo.Git.IsMergedByContentAsync(await MainAsync(), feature, repo.Path)));
    }

    // Main changed the lines again after the squash: the merge conflicts, which is no merge told
    [TestMethod]
    public async Task TestAConflictIsNotMerged()
    {
        var feature = await FeatureAsync();
        await repo.GitAsync("merge -q --squash feature");
        repo.GitAt(["commit", "-q", "-m", "Login (#12)"], TempRepo.BaseTime.AddMinutes(minute++));
        await CommitAsync("login.txt", "form\nvalidated\n", "Reword login");

        Assert.IsFalse(AssertOk(await repo.Git.IsMergedByContentAsync(await MainAsync(), feature, repo.Path)));
    }

    // The branches whose tips main has, main among them, and not one merged by a squash: the remote
    // ones too, as e.g. 'origin/merged', but not origin's HEAD, which only points at one of them
    [TestMethod]
    public async Task TestTheBranchesMergedAsGitCountsIt()
    {
        await FeatureAsync();
        await repo.GitAsync("branch -q merged feature~1");
        await repo.GitAsync("merge -q --no-ff -m \"Merge branch 'merged'\" merged");
        await repo.AddOriginAsync();
        await repo.GitAsync("push -q origin main merged feature");
        await repo.GitAsync("remote set-head origin main");

        var names = AssertOk(await repo.Git.GetMergedBranchNamesAsync(await MainAsync(), repo.Path));

        CollectionAssert.AreEquivalent(new[] { "main", "merged", "origin/main", "origin/merged" }, names.ToArray());
    }
}
