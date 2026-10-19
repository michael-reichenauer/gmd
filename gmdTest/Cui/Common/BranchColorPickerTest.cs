using gmd.Cui.Common;
using gmd.Server;
using gmdTest.Fixtures;

namespace gmdTest.Cui.Common;

// The color a branch is given the first time gmd sees it, which is then remembered, see
// BranchColorServiceTest. Picked among the twelve colors of RGB or the five of the 16 colors.
[TestClass]
public class BranchColorPickerTest
{
    static readonly Dictionary<string, int> None = [];

    // A branch takes the color its name hashes to while no branch it is drawn beside has it, which is
    // the color it had before the colors were remembered. Main has none, it is always magenta.
    [TestMethod]
    public async Task TestBranchTakesTheColorItsNameHashesTo()
    {
        var repo = await Siblings("feat").ViewRepoAsync();

        var colors = BranchColorPicker.Pick(repo, None, 12);

        CollectionAssert.AreEquivalent(new[] { "feat" }, colors.Keys.ToList());
        Assert.AreEqual(BranchColorPicker.FirstChoice(repo, repo.BranchByName["feat"], 12), colors["feat"]);
        Assert.AreEqual(3, colors["feat"]); // 'origin/feat' hashes to 3 of 12
    }

    // As many siblings as there are colors all get a color of their own, though their names hash to
    // the same ones
    [TestMethod]
    [DataRow(12)]
    [DataRow(5)]
    public async Task TestSiblingsAsManyAsTheColorsAllDiffer(int count)
    {
        var names = Names(count);
        var repo = await Siblings(names).ViewRepoAsync();

        var colors = BranchColorPicker.Pick(repo, None, count);

        Assert.AreEqual(count, names.Select(n => colors[n]).Distinct().Count());
    }

    // Once every color is used, a branch takes the one its siblings used least recently, i.e. that
    // of the sibling started longest ago, which is the one least likely to be drawn beside it
    [TestMethod]
    [DataRow(12)]
    [DataRow(5)]
    public async Task TestOnceEveryColorIsUsedTheOneUsedLeastRecentlyIsTaken(int count)
    {
        var names = Names(count + 2); // f0 is the oldest
        var repo = await Siblings(names).ViewRepoAsync();

        var colors = BranchColorPicker.Pick(repo, None, count);

        Assert.AreEqual(colors["f0"], colors[$"f{count}"]);
        Assert.AreEqual(colors["f1"], colors[$"f{count + 1}"]);
    }

    // A child branch whose name hashes to its parent's color takes the next one: 'dev' and 'feat'
    // both hash to 9 of 12 and to 1 of 5
    [TestMethod]
    [DataRow(12, 9, 10)]
    [DataRow(5, 1, 2)]
    public async Task TestChildBranchDoesNotTakeItsParentsColor(int count, int devColor, int featColor)
    {
        var repo = await new RepoBuilder()
            .Commit("f1", "Feature work", "d1")
            .Commit("d1", "Dev work", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c1", isCurrent: true)
            .LocalBranch("dev", "d1")
            .LocalBranch("feat", "f1")
            .ViewRepoAsync();

        var colors = BranchColorPicker.Pick(repo, None, count);

        Assert.AreEqual("dev", repo.BranchByName["feat"].ParentBranchName);
        Assert.AreEqual(devColor, colors["dev"]);
        Assert.AreEqual(featColor, colors["feat"]);
    }

    // A remembered color is kept as it is, though it is the same as a sibling's: it is what the
    // branch has been drawn in, or what the user chose
    [TestMethod]
    public async Task TestRememberedColorIsKept()
    {
        var repo = await Siblings("f0", "f1").ViewRepoAsync();

        var colors = BranchColorPicker.Pick(repo, new Dictionary<string, int> { ["f0"] = 7, ["f1"] = 7 }, 12);

        Assert.AreEqual(7, colors["f0"]);
        Assert.AreEqual(7, colors["f1"]);
    }

    // A new branch is picked against the remembered colors of its siblings
    [TestMethod]
    public async Task TestNewBranchAvoidsTheRememberedColorOfASibling()
    {
        var repo = await Siblings("f0", "feat").ViewRepoAsync();

        var colors = BranchColorPicker.Pick(repo, new Dictionary<string, int> { ["f0"] = 3 }, 12);

        Assert.AreEqual(3, colors["f0"]);
        Assert.AreEqual(4, colors["feat"]); // 'origin/feat' hashes to 3, which 'f0' has
    }

    // A color an older gmd remembered for a pushed branch, which was set with 'g', is keyed by the
    // remote branch's name, and is the branch's color still
    [TestMethod]
    public async Task TestColorKeyedByTheRemoteBranchNameIsKept()
    {
        var repo = await Siblings("feat").ViewRepoAsync();

        var colors = BranchColorPicker.Pick(repo, new Dictionary<string, int> { ["origin/feat"] = 7 }, 12);

        Assert.AreEqual(7, colors["feat"]);
        Assert.IsFalse(colors.ContainsKey("origin/feat"));
    }

    // Only live branches are given a color: a deleted branch is gray, and a detached head white
    [TestMethod]
    public async Task TestDeletedBranchAndDetachedHeadHaveNoColor()
    {
        var repo = await new RepoBuilder()
            .Commit("c3", "Merge branch 'feat' into main", "c2", "f1")
            .Commit("f1", "Feature work", "c1")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c3")
            .DetachedHead("c2")
            .ViewRepoAsync(ShowBranches.AllActiveAndDeleted);

        var colors = BranchColorPicker.Pick(repo, None, 12);

        Assert.IsTrue(repo.AllBranches.Any(b => !b.IsGitBranch && b.NiceName == "feat"));
        Assert.AreEqual(0, colors.Count);
    }

    static string[] Names(int count) => Enumerable.Range(0, count).Select(i => $"f{i}").ToArray();

    // Branches of main, each with a commit of its own and a remote branch, started in the order
    // given, the first one the oldest
    static RepoBuilder Siblings(params string[] names)
    {
        var b = new RepoBuilder();
        for (int i = names.Length - 1; i >= 0; i--)
            b.Commit($"a{i:x}", $"Work on {names[i]}", "c1");
        b.Commit("c1", "Initial").BranchWithRemote("main", "c1", isCurrent: true);
        for (int i = 0; i < names.Length; i++)
            b.BranchWithRemote(names[i], $"a{i:x}");
        return b;
    }
}
